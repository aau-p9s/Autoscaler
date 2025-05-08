using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Entities;
using Autoscaler.Runner.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner;

public class Monitor(
    DeploymentEntity deployment,
    CancellationToken cancellationToken,
    ILogger logger,
    ForecasterService forecaster,
    PrometheusService prometheus,
    KubernetesService kubernetes,
    IHistoricRepository historicRepository,
    IForecastRepository forecastRepository,
    ISettingsRepository settingsRepository)
{
    private Thread Thread => new(async () => await Run());
    private ForecasterService Forecaster => forecaster;
    private PrometheusService Prometheus => prometheus;
    private KubernetesService Kubernetes => kubernetes;
    private ILogger Logger => logger;
    private static Dictionary<Guid, List<double>> ForecastErrorHistory => new();
    private DeploymentEntity Deployment => deployment;
    private CancellationToken CancellationToken => cancellationToken;
    private IHistoricRepository HistoricRepository => historicRepository;
    private IForecastRepository ForecastRepository => forecastRepository;
    private ISettingsRepository SettingsRepository => settingsRepository;
    

    private async Task Run()
    {
        logger.LogInformation("Started monitoring loop...");
        try
        {
            var clock = new Stopwatch();
            clock.Start();
            while (!CancellationToken.IsCancellationRequested)
            {
                await UpdateSettings();

                // Retrain periodically based on TrainInterval.
                if (clock.ElapsedMilliseconds >= Deployment.Settings.TrainInterval)
                {
                    await Forecaster.Retrain(Deployment.Service.Id);
                    clock.Restart();
                }

                var startTime = DateTime.Now;
                try
                {
                    Logger.LogInformation($"Checking deployment {Deployment.Service.Name}");

                    var forecastEntity =
                        await ForecastRepository.GetForecastsByServiceIdAsync(Deployment.Service.Id);
                    if (forecastEntity == null)
                    {
                        await Forecaster.Forecast(Deployment.Service.Id, ForecastRepository);
                        forecastEntity =
                            await ForecastRepository.GetForecastsByServiceIdAsync(Deployment.Service.Id) ?? throw new ArgumentNullException(nameof(ForecastRepository), "Error, there's probably no forecasts in the database");
                    }
                    
                    Logger.LogDebug(forecastEntity.Forecast);

                    var forecast = JObject.Parse(forecastEntity.Forecast);
                    var replicas = await Kubernetes.GetReplicas(Deployment.Service.Name);
                    var actualCpu = await GetCpuUsage(HistoricRepository);
                    var timestamps = forecast["timestamp"]?.ToObject<List<string>>() ??
                                     throw new ArgumentNullException(nameof(forecast),
                                         "Failed to get timestamps from forecast");
                    var cpuValues = forecast["value"]?.ToObject<List<List<double>>>() ??
                                    throw new ArgumentNullException(nameof(forecast),
                                        "Failed to get value forecast");

                    var forecastHorizon =
                        await Kubernetes.GetPodStartupTimePercentileAsync(Deployment.Service.Name);
                    Logger.LogInformation(
                        $"Forecast horizon for {Deployment.Service.Name}: {forecastHorizon.TotalSeconds} seconds");

                    // Instead of a fixed 1 minute, use the forecast horizon from pod startup time
                    var nextTime = DateTime.Now.Add(forecastHorizon)
                        .ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
                    Logger.LogInformation(nextTime);
                    var forecastIndex = timestamps.FindIndex(timestamp => timestamp.Contains(nextTime));

                    if (forecastIndex < 0 || forecastIndex >= cpuValues.Count)
                    {
                        await Forecaster.Forecast(Deployment.Service.Id, ForecastRepository);
                        continue;
                    }

                    var nextForecast = cpuValues[forecastIndex][0];
                    var zScore = GetZScore(nextForecast, actualCpu);

                    if (Math.Abs(zScore) > 3)
                    {
                        Logger.LogInformation("Forecast error exceeds threshold, retraining model.");
                        await Forecaster.Retrain(Deployment.Service.Id);
                        clock.Restart();
                        continue;
                    }

                    Logger.LogDebug("Not retraining model, continuing");

                    if (timestamps.Count == 0 || cpuValues.Count == 0)
                    {
                        Logger.LogError("Forecast data format is invalid");
                        continue;
                    }

                    // Kubernetes HPA scaling logic.
                    await SetReplicas(nextForecast, replicas);

                    // Calculate delay based on processing time.
                    var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                    var delay = Math.Max(0, Deployment.Settings.ScalePeriod - processingTime);
                    Logger.LogInformation($"Thread {Thread.CurrentThread.Name} sleeping for {delay}ms");
                    await Task.Delay((int)delay, CancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error monitoring {Deployment.Service.Name}: {ex.Message}");
                    Logger.LogDebug(ex.StackTrace);
                    await Task.Delay(5000, CancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogError($"Thread {Thread.CurrentThread.Name} was canceled");
        }
        catch (ArgumentNullException ex)
        {
            Logger.LogError($"something was null somewhere where it shouldn't be null, restarting monitor: {ex}");
            await Run();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unhandled exception in thread {Thread.CurrentThread.Name}: {ex}");
        }
    }

    public void Start()
    {
        Logger.LogDebug("Starting Monitor");
        Thread.Start();
    }

    private async Task UpdateSettings()
    {
        var settings = await SettingsRepository.GetSettingsForServiceAsync(deployment.Service.Id);

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

    private double GetZScore(double nextForecast, double actualCpu)
    {
        var forecastError = Math.Abs(nextForecast - actualCpu);
        Logger.LogInformation($"Actual CPU: {actualCpu}, Forecast: {nextForecast}, Error: {forecastError}");

        if (!ForecastErrorHistory.ContainsKey(Deployment.Service.Id))
        {
            ForecastErrorHistory[Deployment.Service.Id] = new List<double>();
        }
        var errorHistory = ForecastErrorHistory[Deployment.Service.Id];
        errorHistory.Add(forecastError);

        // Maintain a rolling window (e.g., last 100 error measurements)
        if (errorHistory.Count > 100)
        {
            errorHistory.RemoveAt(0);
        }

        var meanError = errorHistory.Average();
        var stdError = Math.Sqrt(errorHistory.Average(e => Math.Pow(e - meanError, 2)));

        // Compute the z-score for the current forecast error.
        var zScore = stdError == 0 ? 0 : (forecastError - meanError) / stdError;
        Logger.LogInformation($"Mean error: {meanError:F2}, Std: {stdError:F2}, z-score: {zScore:F2}");

        return zScore;
    }

    private async Task SetReplicas(double nextForecast, int currentReplicas)
    { 
        Logger.LogDebug("Setting replica count");
        int desiredReplicas;
        if (nextForecast > Deployment.Settings.ScaleUp)
        {
            desiredReplicas =
                (int)Math.Ceiling(currentReplicas * (nextForecast / Deployment.Settings.ScaleUp));
            if (desiredReplicas > Deployment.Settings.MaxReplicas)
            {
                desiredReplicas = Deployment.Settings.MaxReplicas;
            }
        }
        else if (nextForecast < Deployment.Settings.ScaleDown)
        {
            desiredReplicas =
                (int)Math.Ceiling(currentReplicas * (nextForecast / Deployment.Settings.ScaleDown));
            if (desiredReplicas < Deployment.Settings.MinReplicas)
            {
                desiredReplicas = Deployment.Settings.MinReplicas;
            }
        }
        else
        {
            desiredReplicas = currentReplicas;
        }
        
        Logger.LogInformation($"Updating {Deployment.Service.Name} to {desiredReplicas} replicas");
        
        // Create the JSON object for scaling.
        var jsonObject = new
        {
            spec = new
            {
                replicas = desiredReplicas
            }
        };
        
        await Kubernetes.Update(
            $"/apis/apps/v1/namespaces/default/deployments/{Deployment.Service.Name}/scale",
            jsonObject);
    }

    private async Task<double> GetCpuUsage(IHistoricRepository repository)
    {
        var data = await Prometheus.QueryRange(
            Deployment.Service.Id,
            Deployment.Service.Name,
            DateTime.Now.AddHours(-12),
            DateTime.Now,
            Deployment.Settings.ScalePeriod);
        await repository.UpsertHistoricDataAsync(data);
        
        var historic = JObject.Parse(data.HistoricData);
        var newestHistorical = historic["data"]?["result"]?[0]?["values"]?.Last ?? throw new ArgumentNullException(nameof(historic), "Error, historic data is null, failed to get newest historical");

        return (double?)newestHistorical[1] ?? throw new ArgumentNullException(nameof(newestHistorical), "Error, newest historical is null");
    }
}