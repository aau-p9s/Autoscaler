using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Entities;
using Autoscaler.Runner.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using ForecastEntity = Autoscaler.Persistence.ForecastRepository.ForecastEntity;

namespace Autoscaler.Runner;

public class Runner
{
    private List<DeploymentEntity> _deployments;
    private readonly IServiceProvider _serviceProvider;
    private readonly ForecasterService _forecaster;
    private readonly PrometheusService _prometheus;
    private readonly KubernetesService _kubernetes;
    private readonly List<Thread> _runningThreads;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public Runner(string forecasterAddress, string kubernetesAddress, string prometheusAddress,
        IServiceProvider serviceProvider, bool developmentMode = false)
    {
        _forecaster = new(forecasterAddress, developmentMode);
        _kubernetes = new(kubernetesAddress, developmentMode);
        _prometheus = new(prometheusAddress, developmentMode);
        _serviceProvider = serviceProvider;
        _deployments = new List<DeploymentEntity>();
        _runningThreads = new List<Thread>();
        _cancellationTokenSource = new CancellationTokenSource();
        Console.WriteLine("Created runner");
    }

    public async Task MainLoop()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var servicesRepository = scope.ServiceProvider.GetRequiredService<IServicesRepository>();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();

            var services = await servicesRepository.GetAllServicesAsync();
            foreach (var service in services)
            {
                var settings = await settingsRepository.GetSettingsForServiceAsync(service.Id);
                _deployments.Add(new DeploymentEntity(service, settings));
            }


            var getServicesFromKubernetes = await _kubernetes.Get("/api/v1/services");
            if (getServicesFromKubernetes != null)
            {
                var deployments = ExtractNonSystemDeployments(getServicesFromKubernetes,
                    new[] {"autoscaler", "mysql", "generator"});
                foreach (var deployment in deployments)
                {
                    var serviceId = Guid.NewGuid();


                    if (_deployments.All(d => d.Service.Name != deployment))
                    {
                        await servicesRepository.UpsertServiceAsync(new ServiceEntity
                        {
                            Id = serviceId,
                            Name = deployment,
                            AutoscalingEnabled = false
                        });

                        await settingsRepository.UpsertSettingsAsync(new SettingsEntity
                        {
                            Id = Guid.NewGuid(),
                            ServiceId = serviceId,
                            ScalePeriod = 60000,
                            ScaleUp = 80,
                            ScaleDown = 20,
                            TrainInterval = 600000,
                            ModelHyperParams = "",
                            OptunaConfig = ""
                        });
                    }
                }
            }
        }

        foreach (var deployment in _deployments)
        {
            var thread = new Thread(() => DeploymentMonitorLoop(deployment, _cancellationTokenSource.Token));
            thread.Name = $"Monitor-{deployment.Service.Name}";
            thread.IsBackground = true;
            _runningThreads.Add(thread);
            thread.Start();
            Console.WriteLine($"Started monitoring thread for {deployment.Service.Name}");
        }
    }

    private async void DeploymentMonitorLoop(DeploymentEntity deployment, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var startTime = DateTime.Now;

                try
                {
                    Console.WriteLine($"Checking deployment {deployment.Service.Name}");

                    // Create a new scope for each iteration to get fresh repository instances
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var historicRepository = scope.ServiceProvider.GetRequiredService<IHistoricRepository>();
                        var forecastRepository = scope.ServiceProvider.GetRequiredService<IForecastRepository>();

                        var data = await _prometheus.QueryRange(
                            deployment.Service.Id,
                            deployment.Service.Name,
                            DateTime.Now.AddHours(-12),
                            DateTime.Now,
                            deployment.Settings.ScalePeriod);
                        await historicRepository.UpsertHistoricDataAsync(data);

                        var forecastEntity = await forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id);
                        
                        if(forecastEntity == null)
                        {
                            await _forecaster.Forecast(deployment.Service.Id);
                        }
                        
                        forecastEntity = await forecastRepository.GetForecastsByServiceIdAsync(deployment.Service.Id);
                        
                        var forecast = JObject.Parse(forecastEntity.Forecast);
                        var historic = JObject.Parse(data.HistoricData);
                        
                        var newestHistorical = historic["data"]["result"][0]["values"].Last;
                        // TODO: Implement the actual logic for scaling. We need to compare the forecasted value with the newest historical value, and scale accordingly,
                        // and we also need to know what format the JSON from the model has such that this can be implemented.
                        
                        // this should probably be rewritten somehow someway
                        /*if (!(forecast. < newestHistorical.Value<double>() * 0.8) || !(forecast.Value > newestHistorical.Value * 1.2) || Database.IsManualChange)
                        {
                            await _forecaster.Retrain(deployment.Service.Id, forecastEntity.ModelId);
                            await _forecaster.Forecast(deployment.Service.Id);
                        }*/
                        
                        var replicas = await _kubernetes.GetReplicas(deployment.Service.Name);
                        if (true) // TODO: forecast.value > scaleUp
                            replicas++;
                        //if (false) // TODO: forecast.value <= scaleDown && replicas > 1
                        //    replicas--;
                        //if (false) // TODO: replicas < 1
                        //    replicas = 1;

                        Console.WriteLine($"Updating {deployment.Service.Name} to {replicas} replicas");

                        // Using JsonObject instead of Dictionary
                        var jsonObject = new
                        {
                            spec = new
                            {
                                replicas = replicas
                            }
                        };

                        await _kubernetes.Update(
                            $"/apis/apps/v1/namespaces/default/deployments/{deployment.Service.Name}/scale",
                            jsonObject);
                    }

                    // Calculate delay based on the processing time
                    var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                    var delay = Math.Max(0, deployment.Settings.ScalePeriod - processingTime);

                    if (true) // TODO: forecast.timestamp > (Datetime.Now - delay)
                    {
                        Console.WriteLine($"Thread {Thread.CurrentThread.Name} sleeping for {delay}ms");
                        await Task.Delay((int) delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring {deployment.Service.Name}: {ex.Message}");
                    // Sleep for a short period before retrying
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

    public void Stop()
    {
        Console.WriteLine("Stopping all monitoring threads");
        _cancellationTokenSource.Cancel();
    }

    public static List<string> ExtractNonSystemDeployments(JObject kubeApiResponse, string[] excludePatterns = null)
    {
        // Initialize result list
        List<string> deploymentNames = new List<string>();

        // Use default exclusion patterns if none provided
        HashSet<string> patternsToExclude = new HashSet<string>(
            excludePatterns ?? new[] {"kubernetes", "prometheus", "autoscaler-deployment"},
            StringComparer.OrdinalIgnoreCase);

        // Get the "items" array which contains all deployments
        JArray items = (JArray) kubeApiResponse["items"];

        // Check if items exist
        if (items == null)
        {
            return deploymentNames;
        }

        // Iterate through each deployment
        foreach (JToken item in items)
        {
            string name = item["metadata"]?["name"]?.ToString();

            // Skip if name is null
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            // Check if name contains any of the exclude patterns
            bool shouldExclude = patternsToExclude.Any(pattern =>
                name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);

            // If it doesn't match any exclusion pattern, add it to our list
            if (!shouldExclude)
            {
                deploymentNames.Add(name);
            }
        }

        return deploymentNames;
    }
}