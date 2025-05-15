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
    private static Dictionary<Guid, List<double>> ForecastErrorHistory => new();

    private async Task Run()
    {
        logger.LogInformation("Started monitoring loop...");
        try
        {
            var clock = new Stopwatch();
            clock.Start();
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!deployment.Service.AutoscalingEnabled)
                {
                    logger.LogDebug("Autoscaling is disabled, waiting...");
                    Thread.Sleep(deployment.Settings.ScalePeriod);
                    continue;
                }
                await UpdateSettings();

                // Retrain periodically based on TrainInterval.
                if (clock.ElapsedMilliseconds >= deployment.Settings.TrainInterval)
                {
                    await forecaster.Retrain(deployment.Service.Id, deployment.Settings.ScalePeriod);
                    clock.Restart();
                }

                var startTime = DateTime.Now;
                try
                {
                    logger.LogInformation($"Checking deployment {deployment.Service.Name}");

                    var forecastEntity =
                        await forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id);
                    if (forecastEntity == null)
                    {
                        await forecaster.Forecast(deployment.Service.Id, deployment.Settings.ScalePeriod);
                        forecastEntity =
                            await forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id) ?? throw new ArgumentNullException(nameof(ForecastRepository), "Error, there's probably no forecasts in the database");
                    }
                    
                    logger.LogDebug(forecastEntity.Forecast);

                    var forecast = JObject.Parse(forecastEntity.Forecast);
                    var replicas = await kubernetes.GetReplicas(deployment.Service.Name);
                    var actualCpu = await GetCpuUsage(historicRepository);
                    var timestamps = forecast["index"]?.ToObject<List<string>>() ??
                                     throw new ArgumentNullException(nameof(forecast),
                                         "Failed to get timestamps from forecast");
                    var cpuValues = forecast["data"]?.ToObject<List<List<double>>>() ??
                                    throw new ArgumentNullException(nameof(forecast),
                                        "Failed to get value forecast");

                    var forecastHorizon =
                        await kubernetes.GetPodStartupTimePercentileAsync(deployment.Service.Name);
                    logger.LogInformation(
                        $"Forecast horizon for {deployment.Service.Name}: {forecastHorizon.TotalSeconds} seconds");

                    // Instead of a fixed 1 minute, use the forecast horizon from pod startup time
                    var nextTime = DateTime.Now.Add(forecastHorizon)
                        .ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
                    logger.LogInformation(nextTime);
                    var formattedTimestamps = timestamps.Select(item => DateTime.Parse(item).ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture)).ToList();
                    var forecastIndex = formattedTimestamps.FindIndex(timestamp => timestamp.Contains(nextTime));

                    if (forecastIndex < 0 || forecastIndex >= cpuValues.Count)
                    {
                        await forecaster.Forecast(deployment.Service.Id, deployment.Settings.ScalePeriod);
                    }
                    if (forecastIndex < 0)
                    {
                        continue;
                    }

                    var nextForecast = cpuValues[forecastIndex][0];
                    var zScore = GetZScore(nextForecast*100, actualCpu);

                    if (Math.Abs(zScore) > 3)
                    {
                        logger.LogInformation("Forecast error exceeds threshold, retraining model.");
                        await forecaster.Retrain(deployment.Service.Id, deployment.Settings.ScalePeriod);
                        clock.Restart();
                        continue;
                    }

                    logger.LogDebug("Not retraining model, continuing");

                    if (timestamps.Count == 0 || cpuValues.Count == 0)
                    {
                        logger.LogError("Forecast data format is invalid");
                        continue;
                    }

                    // Kubernetes HPA scaling logic.
                    await SetReplicas(nextForecast, replicas);

                    // Calculate delay based on processing time.
                    var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                    var delay = Math.Max(0, deployment.Settings.ScalePeriod - processingTime);
                    logger.LogInformation($"Thread {Thread.CurrentThread.Name} sleeping for {delay}ms");
                    await Task.Delay((int)delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error monitoring {deployment.Service.Name}: {ex.Message}");
                    logger.LogDebug(ex.StackTrace);
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogError($"Thread {Thread.CurrentThread.Name} was canceled");
        }
        catch (ArgumentNullException ex)
        {
            logger.LogError($"something was null somewhere where it shouldn't be null, restarting monitor: {ex}");
            await Run();
        }
        catch (Exception ex)
        {
            logger.LogError($"Unhandled exception in thread {Thread.CurrentThread.Name}: {ex}");
        }
    }

    public void Start()
    {
        logger.LogDebug("Starting Monitor");
        Thread.Start();
    }

    private async Task UpdateSettings()
    {
        var settings = await settingsRepository.GetSettingsForServiceAsync(deployment.Service.Id);

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
        logger.LogInformation($"Actual CPU: {actualCpu}, Forecast: {nextForecast}, Error: {forecastError}");

        if (!ForecastErrorHistory.TryGetValue(deployment.Service.Id, out var value))
        {
            value = new();
            ForecastErrorHistory[deployment.Service.Id] = new();
        }
        var errorHistory = value;
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
        logger.LogInformation($"Mean error: {meanError:F2}, Std: {stdError:F2}, z-score: {zScore:F2}");

        return zScore;
    }

    private async Task SetReplicas(double nextForecast, int currentReplicas)
    { 
        logger.LogDebug("Setting replica count");
        int desiredReplicas;
        logger.LogDebug($"Next forecast value: {nextForecast} scaleup: {deployment.Settings.ScaleUp}");
        if (nextForecast > deployment.Settings.ScaleUp)
        {
            logger.LogDebug("Scaling up...");
            desiredReplicas =
                (int)Math.Ceiling(currentReplicas * (nextForecast / deployment.Settings.ScaleUp));
            if (desiredReplicas > deployment.Settings.MaxReplicas)
            {
                desiredReplicas = deployment.Settings.MaxReplicas;
            }
        }
        else if (nextForecast < deployment.Settings.ScaleDown)
        {
            logger.LogDebug("Scaling down...");
            desiredReplicas =
                (int)Math.Ceiling(currentReplicas * (nextForecast / deployment.Settings.ScaleDown));
            if (desiredReplicas < deployment.Settings.MinReplicas)
            {
                desiredReplicas = deployment.Settings.MinReplicas;
            }
        }
        else
        {
            desiredReplicas = currentReplicas;
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
        
        await kubernetes.Update(
            $"/apis/apps/v1/namespaces/default/deployments/{deployment.Service.Name}/scale",
            jsonObject);
    }

    private async Task<double> GetCpuUsage(IHistoricRepository repository)
    {
        var data = await prometheus.QueryRange(
            deployment.Service.Id,
            deployment.Service.Name,
            DateTime.Now.AddHours(-12),
            DateTime.Now,
            deployment.Settings.ScalePeriod);
        await repository.UpsertHistoricDataAsync(data);
        
        var historic = JObject.Parse(data.HistoricData);
        var newestHistorical = historic["data"]?["result"]?[0]?["values"]?.Last ?? throw new ArgumentNullException(nameof(historic), "Error, historic data is null, failed to get newest historical");

        return (double?)newestHistorical[1] ?? throw new ArgumentNullException(nameof(newestHistorical), "Error, newest historical is null");
    }
}