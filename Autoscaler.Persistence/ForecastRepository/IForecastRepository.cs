using System;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.ForecastRepository;

public interface IForecastRepository
{
    public Task<ForecastEntity> GetForecastByIdAsync(Guid id);
    public Task<ForecastEntity> GetForecastsByServiceIdAsync(Guid serviceId);
}