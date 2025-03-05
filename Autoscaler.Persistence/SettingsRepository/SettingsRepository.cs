using System;
using System.Data;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.SettingsRepository;
using Dapper;

namespace Autoscaler.Persistence.ScaleSettingsRepository;

public class SettingsRepository : ISettingsRepository
{
    private static readonly string TableName = "Settings";
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.Connection;

    public SettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SettingsEntity> GetSettingsForServiceAsync(Guid serviceId)
    {
        var settings = await Connection.QueryFirstOrDefaultAsync<SettingsEntity>(
            $"SELECT * FROM {TableName} WHERE (ServiceId::uuid) = @ServiceId", new { ServiceId = serviceId });
        return settings;
    }

    public async Task<bool> UpsertSettingsAsync(SettingsEntity settings)
    {
        var result = await Connection.ExecuteAsync($@"
            INSERT INTO {TableName} (Id, ServiceId, ScaleUp, ScaleDown, ScalePeriod, TrainInterval, Hyperparameters, OptunaConfig)
            VALUES (@Id, @ServiceId, @ScaleUp, @ScaleDown, @ScalePeriod, @TrainInterval, @Hyperparameters, @OptunaConfig)
            ON CONFLICT (ServiceId) DO UPDATE SET
                ScaleUp = @ScaleUp,
                ScaleDown = @ScaleDown,
                ScalePeriod = @ScalePeriod,
                TrainInterval = @TrainInterval,
                Hyperparameters = @Hyperparameters,
                OptunaConfig = @OptunaConfig",
            settings);
        return result > 0;
    }
}