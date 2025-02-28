using System;
using System.Threading.Tasks;
using Autoscaler.Persistence.ScaleSettingsRepository;

namespace Autoscaler.Persistence.SettingsRepository;

public interface ISettingsRepository
{
    public Task<SettingsEntity> GetSettingsForServiceAsync(Guid serviceId);
    public Task<bool> UpsertSettingsAsync(SettingsEntity settings);
}