using System;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.ScaleSettingsRepository;

public interface IScaleSettingsRepository
{
    Task<ScaleSettingsEntity> GetSettingsForServiceAsync(Guid serviceId);
    Task<bool> UpsertSettingsAsync(ScaleSettingsEntity settings);
}