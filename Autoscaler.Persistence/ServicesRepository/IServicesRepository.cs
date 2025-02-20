using System;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.ServicesRepository;

public interface IServicesRepository
{
    Task<ServiceEntity> GetServiceAsync(Guid serviceId);
    Task<Guid> GetServiceIdByNameAsync(string serviceName);
    Task<bool> UpsertServiceAsync(ServiceEntity service);
}