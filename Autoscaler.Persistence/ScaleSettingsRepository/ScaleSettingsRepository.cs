using System;
using System.Data;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Dapper;

namespace Autoscaler.Persistence.ScaleSettingsRepository;

public class ScaleSettingsRepository : IScaleSettingsRepository
{
    private static readonly string TableName = "Settings";
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.Connection;

    public ScaleSettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ScaleSettingsEntity> GetSettingsForServiceAsync(Guid serviceId)
    {
        var settings = await Connection.QueryFirstOrDefaultAsync<ScaleSettingsEntity>(
            $"SELECT * FROM {TableName} WHERE ServiceId = @ServiceId", new { ServiceId = serviceId });
        return settings;
    }

    public async Task<bool> UpsertSettingsAsync(ScaleSettingsEntity settings)
    {
        var result = await Connection.ExecuteAsync($@"
            INSERT INTO {TableName} (Id, ServiceId, ScaleUp, ScaleDown, ScalePeriod)
            VALUES (@Id, @ServiceId, @ScaleUp, @ScaleDown, @ScalePeriod)
            ON CONFLICT (ServiceId) DO UPDATE SET
                ScaleUp = @ScaleUp,
                ScaleDown = @ScaleDown,
                ScalePeriod = @ScalePeriod",
            settings);
        return result > 0;
    }
}