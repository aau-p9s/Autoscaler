using System;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.ScaleSettingsRepository;

public interface IScaleSettingsRepository
{
    public Task<ScaleSettingsEntity> GetSettingsForServiceAsync(Guid serviceId);
    public Task<bool> UpsertSettingsAsync(ScaleSettingsEntity settings);
}