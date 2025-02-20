using System;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.HistoricRepository;

public interface IHistoricRepository
{
    public Task<HistoricEntity> GetHistoricDataByServiceIdAsync(Guid serviceId);
    public Task<bool> UpsertHistoricDataAsync(HistoricEntity historicEntity);
}