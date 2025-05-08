using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Entities;
using Autoscaler.Runner.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner;

public class Runner(
    AppSettings appSettings,
    ILogger logger,
    ForecasterService forecaster,
    PrometheusService prometheus,
    KubernetesService kubernetes,
    IServiceProvider serviceProvider)
{
    private ILogger Logger => logger;
    private List<DeploymentEntity> Deployments => new();
    private ForecasterService Forecaster => forecaster;
    private PrometheusService Prometheus => prometheus;
    private KubernetesService Kubernetes => kubernetes;
    private List<Monitor> Monitors => new();
    private CancellationTokenSource CancellationTokenSource => new();
    private bool DevelopmentMode => appSettings.Autoscaler.DevelopmentMode;
    private static string[] Collection => new[] { "kubernetes", "prometheus", "autoscaler-deployment" };
    private IServiceProvider _serviceProvider => serviceProvider;

    public async Task MainLoop()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var servicesRepository = scope.ServiceProvider.GetRequiredService<IServicesRepository>();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var forecastRepository = scope.ServiceProvider.GetRequiredService<IForecastRepository>();

            var services = await servicesRepository.GetAllServicesAsync();
            foreach (var service in services)
            {
                var settings = await settingsRepository.GetSettingsForServiceAsync(service.Id);
                Deployments.Add(new DeploymentEntity(service, settings));
            }


            var getServicesFromKubernetes = await Kubernetes.Get("/apis/apps/v1/deployments");
            if (getServicesFromKubernetes != null)
            {
                Logger.LogDebug(getServicesFromKubernetes.ToString());
                var deployments = ExtractNonSystemDeployments(getServicesFromKubernetes,
                    new[] { "autoscaler", "mysql", "generator" });
                foreach (var deployment in deployments)
                {
                    var serviceId = Guid.NewGuid();

                    if (DevelopmentMode)
                    {
                        var forecast = await File.ReadAllTextAsync(
                            "./DevelopmentData/forecast.json");
                        await forecastRepository.InsertForecast(new ForecastEntity(Guid.NewGuid(), serviceId,
                            DateTime.Now, Guid.NewGuid(), forecast, false));
                    }

                    if (Deployments.All(d => d.Service.Name != deployment))
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
                            MinReplicas = 1,
                            MaxReplicas = 10,
                            TrainInterval = 600000,
                            ModelHyperParams = "",
                            OptunaConfig = ""
                        });
                    }
                }
            }
        }

        foreach (var deployment in Deployments)
        {
            var monitor = new Monitor(deployment, CancellationTokenSource.Token, Logger, _serviceProvider, Forecaster, Prometheus, Kubernetes);

            Monitors.Add(monitor);
            monitor.Start();
            Logger.LogInformation($"Started monitoring thread for {deployment.Service.Name}");
        }
    }

    private static List<string> ExtractNonSystemDeployments(JObject kubeApiResponse, string[]? excludePatterns)
    {
        // Initialize result list
        List<string> deploymentNames = new List<string>();

        // Use default exclusion patterns if none provided
        HashSet<string> patternsToExclude = new HashSet<string>(
            excludePatterns ?? Collection,
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
