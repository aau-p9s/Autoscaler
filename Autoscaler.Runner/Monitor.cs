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
            var counter = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var data = await prometheus.QueryRange(
                    deployment.Service.Id,
                    deployment.Service.Name,
                    DateTime.Now.AddHours(-1),
                    DateTime.Now,
                    deployment.Settings.ScalePeriod);
                await historicRepository.UpsertHistoricDataAsync(data);

                if (!deployment.Service.AutoscalingEnabled)
                {
                    logger.LogDebug("Autoscaling is disabled, waiting...");
                    Thread.Sleep(deployment.Settings.ScalePeriod);
                    continue;
                }

                await UpdateSettings();

                // Retrain periodically based on TrainInterval.
                if (clock.ElapsedMilliseconds >= deployment.Settings.TrainInterval || counter == 0)
                {
                    await forecaster.Retrain(deployment.Service.Id, deployment.Settings.ScalePeriod / 60000);
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
                        await forecaster.Forecast(deployment.Service.Id, deployment.Settings.ScalePeriod / 60000);
                        forecastEntity =
                            await forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id) ??
                            throw new ArgumentNullException(nameof(ForecastRepository),
                                "Error, there's probably no forecasts in the database");
                    }

                    logger.LogDebug(forecastEntity.Forecast);

                    var forecast = JObject.Parse(forecastEntity.Forecast);
                    var replicas = await kubernetes.GetReplicas(deployment.Service.Name);
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
                    var formattedTimestamps = timestamps.Select(item =>
                        DateTime.Parse(item).ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture)).ToList();
                    var forecastIndex = formattedTimestamps.FindIndex(timestamp => timestamp.Contains(nextTime));

                    if (forecastIndex < 0 || forecastIndex >= cpuValues.Count)
                    {
                        await forecaster.Forecast(deployment.Service.Id, deployment.Settings.ScalePeriod / 60000);
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

                    await SetReplicas(nextForecast * 100, replicas);

                    // Calculate delay based on processing time.
                    var processingTime = (DateTime.Now - startTime);
                    var delay = DateTime.Now.Add(forecastHorizon) - processingTime;
                    logger.LogInformation($"Thread {Thread.CurrentThread.Name} sleeping for {delay}ms");
                    await Task.Delay(delay.Millisecond, cancellationToken);
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
}