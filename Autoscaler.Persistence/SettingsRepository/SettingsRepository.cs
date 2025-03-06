using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.SettingsRepository;
using Dapper;
using Npgsql;
using NpgsqlTypes;

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
        var query = $@"
    INSERT INTO {TableName} (Id, ServiceId, ScaleUp, ScaleDown, ScalePeriod, TrainInterval, ModelHyperParams, OptunaConfig)
    VALUES (@Id, @ServiceId, @ScaleUp, @ScaleDown, @ScalePeriod, @TrainInterval, 
            COALESCE((SELECT ModelHyperParams FROM {TableName} WHERE ServiceId = @ServiceId), @ModelHyperParams::jsonb),
            COALESCE((SELECT OptunaConfig FROM {TableName} WHERE ServiceId = @ServiceId), @OptunaConfig::jsonb))
    ON CONFLICT (ServiceId) DO UPDATE SET
        ScaleUp = @ScaleUp,
        ScaleDown = @ScaleDown,
        ScalePeriod = @ScalePeriod,
        TrainInterval = @TrainInterval,
        ModelHyperParams = COALESCE(EXCLUDED.ModelHyperParams, {TableName}.ModelHyperParams),
        OptunaConfig = COALESCE(EXCLUDED.OptunaConfig, {TableName}.OptunaConfig);";
        
        var result = await Connection.ExecuteAsync(query, new
        {
            Id = settings.Id,
            ServiceId = settings.ServiceId,
            ScaleUp = settings.ScaleUp,
            ScaleDown = settings.ScaleDown,
            ScalePeriod = settings.ScalePeriod,
            TrainInterval = settings.TrainInterval,
            ModelHyperParams = settings.ModelHyperParams,
            OptunaConfig = settings.OptunaConfig
        });
        
        return result > 0;
    }
}