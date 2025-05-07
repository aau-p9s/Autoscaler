using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Entities;
using Autoscaler.Runner.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner;

public class Runner
{
    private readonly ILogger logger;
    private List<DeploymentEntity> _deployments;
    private readonly ForecasterService _forecaster;
    private readonly PrometheusService _prometheus;
    private readonly KubernetesService _kubernetes;
    private readonly IServicesRepository _servicesRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IForecastRepository _forecastRepository;
    private readonly IHistoricRepository _historicRepository;
    private readonly List<Thread> _runningThreads;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly bool _developmentMode;
    private static readonly string[] _collection = new[] { "kubernetes", "prometheus", "autoscaler-deployment" };
    private readonly Dictionary<Guid, List<double>> _forecastErrorHistory = new Dictionary<Guid, List<double>>();


    public Runner(AppSettings appSettings, ILogger logger, KubernetesService kubernetes, PrometheusService prometheus, ForecasterService forecaster, IServicesRepository servicesRepository, ISettingsRepository settingsRepository, IForecastRepository forecastRepository, IHistoricRepository historicRepository)
    {
        _kubernetes = kubernetes;
        _prometheus = prometheus;
        _forecaster = forecaster;
        _settingsRepository = settingsRepository;
        _servicesRepository = servicesRepository;
        _forecastRepository = forecastRepository;
        _historicRepository = historicRepository;
        this.logger = logger;
        _deployments = new List<DeploymentEntity>();
        _runningThreads = new List<Thread>();
        _cancellationTokenSource = new CancellationTokenSource();
        _developmentMode = appSettings.Autoscaler.DevelopmentMode;
        logger.LogDebug("TESTER");
        logger.LogInformation(appSettings.Logging.LogLevel.Autoscaler);
        Environment.Exit(0);
    }

    public async Task MainLoop()
    {
        var services = await _servicesRepository.GetAllServicesAsync();
        foreach (var service in services)
        {
            var settings = await _settingsRepository.GetSettingsForServiceAsync(service.Id);
            _deployments.Add(new DeploymentEntity(service, settings));
        }


        var getServicesFromKubernetes = await _kubernetes.Get("/apis/apps/v1/deployments");
        if (getServicesFromKubernetes != null)
        {
            logger.LogDebug(getServicesFromKubernetes.ToString());
            var deployments = ExtractNonSystemDeployments(getServicesFromKubernetes,
                new[] { "autoscaler", "mysql", "generator" });
            foreach (var deployment in deployments)
            {
                var serviceId = Guid.NewGuid();

                if (_developmentMode)
                {
                    var forecast = await File.ReadAllTextAsync(
                        "./DevelopmentData/forecast.json");
                    await _forecastRepository.InsertForecast(new ForecastEntity(Guid.NewGuid(), serviceId,
                        DateTime.Now, Guid.NewGuid(), forecast, false));
                }

                if (_deployments.All(d => d.Service.Name != deployment))
                {
                    await _servicesRepository.UpsertServiceAsync(new ServiceEntity
                    {
                        Id = serviceId,
                        Name = deployment,
                        AutoscalingEnabled = false
                    });

                    await _settingsRepository.UpsertSettingsAsync(new SettingsEntity
                    {
                        Id = Guid.NewGuid(),
                        ServiceId = serviceId,
                        ScalePeriod = 60000,
                        ScaleUp = 80,
                        ScaleDown = 20,
                        MinReplicas = 1,
                        MaxReplicas = 10,
                        TrainInterval = 600000,
                        ModelHyperParams = "",
                        OptunaConfig = ""
                    });
                }
            }
        }

        foreach (var deployment in _deployments)
        {
            var thread = new Thread(() => DeploymentMonitorLoop(deployment, _cancellationTokenSource.Token))
            {
                Name = $"Monitor-{deployment.Service.Name}",
                IsBackground = true
            };
            _runningThreads.Add(thread);
            thread.Start();
            logger.LogInformation($"Started monitoring thread for {deployment.Service.Name}");
        }
    }

    private async void DeploymentMonitorLoop(DeploymentEntity deployment, CancellationToken cancellationToken)
    {
        try
        {
            var clock = new Stopwatch();
            clock.Start();
            while (!cancellationToken.IsCancellationRequested)
            {
                await UpdateSettings(deployment);

                // Retrain periodically based on TrainInterval.
                if (clock.ElapsedMilliseconds >= deployment.Settings.TrainInterval)
                {
                    await _forecaster.Retrain(deployment.Service.Id);
                    clock.Restart();
                }

                var startTime = DateTime.Now;
                try
                {
                    logger.LogInformation($"Checking deployment {deployment.Service.Name}");

                    // Create a new scope for each iteration to get fresh repository instances
                    var data = await _prometheus.QueryRange(
                        deployment.Service.Id,
                        deployment.Service.Name,
                        DateTime.Now.AddHours(-12),
                        DateTime.Now,
                        deployment.Settings.ScalePeriod);
                    await _historicRepository.UpsertHistoricDataAsync(data);

                    var forecastEntity =
                        await _forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id);
                    if (forecastEntity == null)
                    {
                        await _forecaster.Forecast(deployment.Service.Id);
                        forecastEntity =
                            await _forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id);
                    }

                    var forecast = JObject.Parse(forecastEntity.Forecast);
                    var historic = JObject.Parse(data.HistoricData);

                    var replicas = await _kubernetes.GetReplicas(deployment.Service.Name);

                    var newestHistorical = historic["data"]?["result"]?[0]?["values"]?.Last;
                    if (newestHistorical == null)
                    {
                        logger.LogError("Unable to parse newest historical data.");
                        continue;
                    }

                    double actualCpu;
                    try
                    {
                        actualCpu = (newestHistorical[1] ?? throw new InvalidOperationException()).Value<double>();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Error parsing historical CPU value: {ex.Message}");
                        continue;
                    }


                    var timestamps = forecast["timestamp"]?.ToObject<List<string>>();
                    var cpuValues = forecast["value"]?.ToObject<List<List<double>>>();

                    if (timestamps == null || cpuValues == null)
                    {
                        logger.LogError("Forecast data format is invalid");
                        continue;
                    }

                    var forecastHorizon =
                        await _kubernetes.GetPodStartupTimePercentileAsync(deployment.Service.Name);
                    logger.LogInformation(
                        $"Forecast horizon for {deployment.Service.Name}: {forecastHorizon.TotalSeconds} seconds");
                    
                    // Instead of a fixed 1 minute, use the forecast horizon from pod startup time
                    var nextTime = DateTime.Now.Add(forecastHorizon).ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
                    logger.LogInformation(nextTime);
                    int forecastIndex = 0;
                    for (int i = 0; i < timestamps.Count; i++)
                    {
                        if (timestamps[i].Contains(nextTime))
                        {
                            forecastIndex = i;
                            break;
                        }
                    }
                    
                    double? nextForecast = null;
                    if (forecastIndex >= 0 && forecastIndex < cpuValues.Count)
                    {
                        nextForecast = cpuValues[forecastIndex][0];
                    }

                    if (nextForecast == null)
                    {
                        await _forecaster.Forecast(deployment.Service.Id);
                        continue;
                    }
                    

                    double forecastError = Math.Abs(nextForecast.Value - actualCpu);
                    logger.LogInformation(
                        $"Actual CPU: {actualCpu}, Forecast: {nextForecast.Value}, Error: {forecastError}");

                    if (!_forecastErrorHistory.ContainsKey(deployment.Service.Id))
                    {
                        _forecastErrorHistory[deployment.Service.Id] = new List<double>();
                    }

                    var errorHistory = _forecastErrorHistory[deployment.Service.Id];
                    errorHistory.Add(forecastError);

                    // Maintain a rolling window (e.g., last 100 error measurements)
                    if (errorHistory.Count > 100)
                    {
                        errorHistory.RemoveAt(0);
                    }

                    double meanError = errorHistory.Average();
                    double stdError = Math.Sqrt(errorHistory.Average(e => Math.Pow(e - meanError, 2)));

                    // Compute the z-score for the current forecast error.
                    double zScore = stdError == 0 ? 0 : (forecastError - meanError) / stdError;
                    logger.LogInformation($"Mean error: {meanError:F2}, Std: {stdError:F2}, z-score: {zScore:F2}");

                    if (Math.Abs(zScore) > 3)
                    {
                        logger.LogInformation("Forecast error exceeds threshold, retraining model.");
                        await _forecaster.Retrain(deployment.Service.Id);
                        errorHistory.Clear();
                        clock.Restart();
                        continue;
                    }
                    logger.LogDebug("Not retraining model, continuing");

                    if (timestamps.Count == 0 || cpuValues.Count == 0)
                    {
                        logger.LogError("Forecast data format is invalid");
                        continue;
                    }
                    logger.LogDebug("Continuing to Scaling logic");

                    // Kubernetes HPA scaling logic.
                    int desiredReplicas;
                    if (nextForecast > deployment.Settings.ScaleUp)
                    {
                        desiredReplicas =
                            (int)Math.Ceiling(replicas * (nextForecast.Value / deployment.Settings.ScaleUp));
                        if (desiredReplicas > deployment.Settings.MaxReplicas)
                        {
                            desiredReplicas = deployment.Settings.MaxReplicas;
                        }
                    }
                    else if (nextForecast < deployment.Settings.ScaleDown)
                    {
                        desiredReplicas =
                            (int)Math.Ceiling(replicas * (nextForecast.Value / deployment.Settings.ScaleDown));
                        if (desiredReplicas < deployment.Settings.MinReplicas)
                        {
                            desiredReplicas = deployment.Settings.MinReplicas;
                        }
                    }
                    else
                    {
                        desiredReplicas = replicas;
                    }

                    logger.LogInformation($"Updating {deployment.Service.Name} to {desiredReplicas} replicas");

                    // Create the JSON object for scaling.
                    var jsonObject = new
                    {
                        spec = new
                        {
                            replicas = desiredReplicas
                        }
                    };

                    await _kubernetes.Update(
                        $"/apis/apps/v1/namespaces/default/deployments/{deployment.Service.Name}/scale",
                        jsonObject);

                    // Calculate delay based on processing time.
                    var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                    var delay = Math.Max(0, deployment.Settings.ScalePeriod - processingTime);
                    Console.WriteLine($"Thread {Thread.CurrentThread.Name} sleeping for {delay}ms");
                    await Task.Delay((int)delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring {deployment.Service.Name}: {ex.Message}");
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.Name} was canceled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception in thread {Thread.CurrentThread.Name}: {ex}");
        }
    }

    private async Task UpdateSettings(DeploymentEntity deployment)
    {
        var settings = await _settingsRepository.GetSettingsForServiceAsync(deployment.Service.Id);

        if (settings.TrainInterval != deployment.Settings.TrainInterval)
        {
            deployment.Settings.TrainInterval = settings.TrainInterval;
        }

        if (settings.ScaleDown != deployment.Settings.ScaleDown)
        {
            deployment.Settings.ScaleDown = settings.ScaleDown;
        }

        if (settings.ScaleUp != deployment.Settings.ScaleUp)
        {
            deployment.Settings.ScaleUp = settings.ScaleUp;
        }

        if (settings.ScalePeriod != deployment.Settings.ScalePeriod)
        {
            deployment.Settings.ScalePeriod = settings.ScalePeriod;
        }

        if (settings.MaxReplicas != deployment.Settings.MaxReplicas)
        {
            deployment.Settings.MaxReplicas = settings.MaxReplicas;
        }

        if (settings.MinReplicas != deployment.Settings.MinReplicas)
        {
            deployment.Settings.MinReplicas = settings.MinReplicas;
        }
    }


    public void Stop()
    {
        Console.WriteLine("Stopping all monitoring threads");
        _cancellationTokenSource.Cancel();
    }

    private static List<string> ExtractNonSystemDeployments(JObject kubeApiResponse, string[]? excludePatterns)
    {
        // Initialize result list
        List<string> deploymentNames = new List<string>();

        // Use default exclusion patterns if none provided
        HashSet<string> patternsToExclude = new HashSet<string>(
            excludePatterns ?? _collection,
            StringComparer.OrdinalIgnoreCase);

        // Get the "items" array which contains all deployments
        JArray items = (JArray)kubeApiResponse["items"];

        // Check if items exist
        if (items == null)
        {
            return deploymentNames;
        }

        // Iterate through each deployment
        deploymentNames.AddRange(from item in items
            select item["metadata"]?["name"]?.ToString() ?? throw new InvalidOperationException()
            into name
            where !string.IsNullOrEmpty(name)
            let shouldExclude =
                patternsToExclude.Any(pattern => name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            where !shouldExclude
            select name);

        return deploymentNames;
    }
}
