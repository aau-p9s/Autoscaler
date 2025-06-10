using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ModelRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Entities;
using Autoscaler.Runner.Services;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner;

public class Runner(
    ILogger logger,
    ForecasterService forecaster,
    PrometheusService prometheus,
    KubernetesService kubernetes,
    IServicesRepository servicesRepository,
    ISettingsRepository settingsRepository,
    IForecastRepository forecastRepository,
    IHistoricRepository historicRepository,
    IModelRepository modelsRepository,
    AppSettings appSettings)
{
    private List<DeploymentEntity> _deployments = new();
    private static Dictionary<DeploymentEntity, Monitor> Monitors => new();
    private CancellationTokenSource CancellationTokenSource => new();

    public async Task MainLoop()
    {
        var services = await servicesRepository.GetAllServicesAsync();

        foreach (var service in services)
        {
            var settings = await settingsRepository.GetSettingsForServiceAsync(service.Id);
            _deployments.Add(new(service, settings));
        }

        var deployments = await kubernetes.GetDeployments();
        foreach (var deployment in deployments.Where(deployment => _deployments.All(d => d.Service.Name != deployment)))
        {
            await AddService(deployment);
        }

        foreach (var deployment in _deployments)
        {
            if (Monitors.ContainsKey(deployment))
                continue;
            var monitor = new Monitor(deployment, CancellationTokenSource.Token, logger, forecaster, prometheus,
                kubernetes, historicRepository, forecastRepository, settingsRepository, appSettings);
            Monitors.Add(deployment, monitor);
            monitor.Start();
            logger.LogInformation($"Started monitoring thread for {deployment.Service.Name}");
        }
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
            ScalePeriod = 3600000,
            ScaleUp = 80,
            ScaleDown = 20,
            MinReplicas = 1,
            MaxReplicas = 10,
            TrainInterval = 3600000
        });
        await modelsRepository.InsertModelsForServiceAsync(id);
    }
}