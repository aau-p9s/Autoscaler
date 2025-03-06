using System;

namespace Autoscaler.Persistence.ServicesRepository;

public class ServiceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool AutoscalingEnabled { get; set; }

    // Empty constructor for Dapper
    public ServiceEntity()
    {
    }

    public ServiceEntity(Guid id, string name, bool autoscalingEnabled)
    {
        Id = id;
        Name = name;
        AutoscalingEnabled = autoscalingEnabled;
    }
}