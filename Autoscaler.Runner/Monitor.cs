using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
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
    ISettingsRepository settingsRepository,
    AppSettings appSettings)
{
    private Thread Thread => new(async () => await Run());

    private async Task Run()
    {
        logger.LogInformation("Started monitoring loop...");
        try
        {
            var clock = new Stopwatch();
            clock.Start();
            var counter = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!deployment.Service.AutoscalingEnabled)
                {
                    logger.LogDebug("Autoscaling is disabled, waiting...");
                    await Task.Delay(deployment.Settings.ScalePeriod);
                    continue;
                }
                
                var forecastHorizon =
                    await kubernetes.GetPodStartupTimePercentileAsync(deployment);

                var now = DateTime.Now;
                var data = await prometheus.SumCpuUsage(
                    deployment,
                    now.Subtract(TimeSpan.FromMilliseconds(deployment.Settings.TrainInterval)).Subtract(TimeSpan.FromMinutes(1)),
                    now, 
                    forecastHorizon);
                await historicRepository.UpsertHistoricDataAsync(data);

                await UpdateSettings();

                // Retrain periodically based on TrainInterval.
                if (clock.ElapsedMilliseconds >= deployment.Settings.TrainInterval || counter == 0)
                {
                    await forecaster.Retrain(deployment, forecastHorizon);
                    // Regardless of the existence of a forecast currently, we would ALWAYS want a new forecast every time we've trained
                    // This means that splitting these two functions is redundant when TrainInterval and ScalePeriod is equal
                    await forecaster.Forecast(deployment, forecastHorizon);
                    clock.Restart();
                    counter++;
                }

                var startTime = DateTime.Now;
                try
                {
                    logger.LogInformation($"Checking deployment {deployment.Service.Name}");

                    var forecastEntity =
                        await forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id);
                    if (forecastEntity == null)
                    {
                        await forecaster.Forecast(deployment, forecastHorizon);
                        forecastEntity =
                            await forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id) ??
                            throw new ArgumentNullException(nameof(ForecastRepository),
                                "Error, there's probably no forecasts in the database");
                    }

                    logger.LogDebug(forecastEntity.Forecast);

                    var forecast = JObject.Parse(forecastEntity.Forecast);
                    var replicas = await kubernetes.GetReplicas(deployment);
                    var timestamps = forecast["index"]?.ToObject<List<string>>() ??
                                     throw new ArgumentNullException(nameof(forecast),
                                         "Failed to get timestamps from forecast");
                    var cpuValues = forecast["data"]?.ToObject<List<List<double>>>() ??
                                    throw new ArgumentNullException(nameof(forecast),
                                        "Failed to get value forecast");

                    logger.LogInformation(
                        $"Forecast horizon for {deployment.Service.Name}: {forecastHorizon.TotalSeconds} seconds");

                    // Instead of a fixed 1 minute, use the forecast horizon from pod startup time
                    var nextTime = DateTime.Now.Add(forecastHorizon)
                        .ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
                    logger.LogInformation(nextTime);
                    var formattedTimestamps = timestamps.Select(item =>
                        DateTime.Parse(item).ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture)).ToList();
                    var forecastIndex = formattedTimestamps.FindIndex(timestamp => timestamp.Contains(nextTime));

                    if (forecastIndex < 0 || forecastIndex >= cpuValues.Count)
                    {
                        await forecaster.Forecast(deployment, forecastHorizon);
                    }

                    if (forecastIndex < 0)
                    {
                        continue;
                    }

                    var nextForecast = cpuValues[forecastIndex][0];

                    if (timestamps.Count == 0 || cpuValues.Count == 0)
                    {
                        logger.LogError("Forecast data format is invalid");
                        continue;
                    }
                    logger.LogInformation($"Next forecasted value: {nextForecast}");
                    await SetReplicas(nextForecast, replicas);

                    // Calculate delay based on processing time.
                    var processingTime = DateTime.Now - startTime;
                    var delay = forecastHorizon.Subtract(processingTime);
                    logger.LogInformation($"Thread {Thread.CurrentThread.Name} sleeping for {delay.TotalMilliseconds}ms");
                    await Task.Delay(delay, cancellationToken);
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

    private async Task SetReplicas(double nextForecast, int currentReplicas)
    {
        var avgForecast = nextForecast / currentReplicas;
        logger.LogDebug("Setting replica count");
        int desiredReplicas;
        logger.LogDebug($"Next forecast value: {avgForecast} for {currentReplicas} replicas");
        logger.LogDebug($"Scaleup: {deployment.Settings.ScaleUp} scaledown: {deployment.Settings.ScaleDown}");
        if (nextForecast > deployment.Settings.ScaleUp)
        {
            logger.LogDebug("Scaling up...");
            desiredReplicas = CalculateReplicas(nextForecast, currentReplicas, deployment.Settings.ScaleUp);
            if (desiredReplicas > deployment.Settings.MaxReplicas)
            {
                logger.LogWarning($"Tried to scale up to {desiredReplicas}");
                desiredReplicas = deployment.Settings.MaxReplicas;
            }
        }
        else if (nextForecast < deployment.Settings.ScaleDown)
        {
            logger.LogDebug("Scaling down...");
            desiredReplicas = CalculateReplicas(avgForecast, currentReplicas, deployment.Settings.ScaleDown);
            if (desiredReplicas < deployment.Settings.MinReplicas)
            {
                logger.LogWarning($"Tried to scale down to {desiredReplicas}");
                desiredReplicas = deployment.Settings.MinReplicas;
            }
        }
        else
        {
            desiredReplicas = currentReplicas;
        }

        logger.LogInformation($"Updating {deployment.Service.Name} to {desiredReplicas} replicas");

        kubernetes.SetReplicas(deployment, desiredReplicas);
    }

    private int CalculateReplicas(double metric, int current, int threshold)
    {
        var avgMetric = prometheus.Type == "avg" ? metric : metric / current;
        return (int)Math.Ceiling(current * (avgMetric / threshold));
    }

}