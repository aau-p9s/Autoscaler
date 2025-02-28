using System;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.ServicesRepository;

public interface IServicesRepository
{
    public Task<ServiceEntity> GetServiceByIdAsync(Guid serviceId);
    public Task<Guid> GetServiceIdByNameAsync(string serviceName);
    public Task<bool> UpsertServiceAsync(ServiceEntity service);
}