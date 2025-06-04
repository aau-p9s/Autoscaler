using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;

namespace Autoscaler.Runner.Entities;

public class DeploymentEntity(ServiceEntity service, SettingsEntity settings)
{
    public ServiceEntity Service => service;
    public SettingsEntity Settings => settings;
}