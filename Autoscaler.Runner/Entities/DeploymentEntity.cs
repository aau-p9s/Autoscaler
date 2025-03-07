using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;

namespace Autoscaler.Runner.Entities;

public class DeploymentEntity
{
    public ServiceEntity Service { get; set; }
    public SettingsEntity Settings { get; set; }

    public DeploymentEntity(ServiceEntity service, SettingsEntity settings)
    {
        Service = service;
        Settings = settings;
    }
}