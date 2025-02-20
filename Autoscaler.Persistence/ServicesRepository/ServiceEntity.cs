using System;

namespace Autoscaler.Persistence.ServicesRepository;

public class ServiceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    // Empty constructor for Dapper
    public ServiceEntity()
    {
    }

    public ServiceEntity(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}