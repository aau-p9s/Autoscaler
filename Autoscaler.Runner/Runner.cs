using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner;

public class Runner(
    AppSettings appSettings,
    ILogger logger,
    ForecasterService forecaster,
    PrometheusService prometheus,
    KubernetesService kubernetes,
    IServicesRepository servicesRepository,
    ISettingsRepository settingsRepository,
    IForecastRepository forecastRepository,
    IHistoricRepository historicRepository)
{
    private ILogger Logger => logger;
    private List<DeploymentEntity> _deployments = new();
    private ForecasterService Forecaster => forecaster;
    private PrometheusService Prometheus => prometheus;
    private KubernetesService Kubernetes => kubernetes;
    private static List<Monitor> Monitors => new();
    private CancellationTokenSource CancellationTokenSource => new();
    private ISettingsRepository SettingsRepository => settingsRepository;
    private IForecastRepository ForecastRepository => forecastRepository;
    private IHistoricRepository HistoricRepository => historicRepository;

    public async Task MainLoop()
    {
        var services = await servicesRepository.GetAllServicesAsync();
        
        foreach (var service in services)
        {
            var settings = await SettingsRepository.GetSettingsForServiceAsync(service.Id);
            _deployments.Add(new(service, settings));
        }

        var deployments = await GetDeployments();
        foreach (var deployment in deployments.Where(deployment => _deployments.All(d => d.Service.Name != deployment)))
        {
            await AddService(deployment);
        }
        
        foreach (var deployment in _deployments)
        {
            var monitor = new Monitor(deployment, CancellationTokenSource.Token, Logger, Forecaster, Prometheus, Kubernetes, HistoricRepository, ForecastRepository, SettingsRepository);
            Monitors.Add(monitor);
            monitor.Start();
            Logger.LogInformation($"Started monitoring thread for {deployment.Service.Name}");
        }
    }

    private async Task<List<string>> GetDeployments()
    {
        var result = new List<string>();
        var response = await Kubernetes.Get("/apis/apps/v1/deployments") ?? throw new ArgumentNullException(nameof(Kubernetes), "Kubernetes response is null");
        var items = response["items"] ?? throw new ArgumentNullException(nameof(response), "Response from kubernetes was null");
        
        foreach (var item in items)
        {
            var metadata = item["metadata"] ?? throw new ArgumentNullException(nameof(item), "Item did not have metadata");
            var name = metadata["name"] ?? throw new ArgumentNullException(nameof(metadata), "Metadata did not have a name");
            result.Add(name.ToString());
        }
        return result;
    }

    private async Task AddService(string service)
    {
        var id = Guid.NewGuid();
        await servicesRepository.UpsertServiceAsync(new ServiceEntity
        {
            Id = id,
            Name = service,
            AutoscalingEnabled = false
        });
        await settingsRepository.UpsertSettingsAsync(new SettingsEntity
        {
            Id = Guid.NewGuid(),
            ServiceId = id,
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
