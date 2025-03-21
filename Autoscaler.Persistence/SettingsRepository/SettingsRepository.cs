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
        string modelHyperParams =
            string.IsNullOrWhiteSpace(settings.ModelHyperParams) ? null : settings.ModelHyperParams;
        string optunaConfig = string.IsNullOrWhiteSpace(settings.OptunaConfig) ? null : settings.OptunaConfig;

        var query = $@"
        INSERT INTO {TableName} (
            Id, ServiceId, ScaleUp, ScaleDown, MinReplicas, MaxReplicas, ScalePeriod, TrainInterval, ModelHyperParams, OptunaConfig
        ) 
        VALUES (
            @Id, @ServiceId, @ScaleUp, @ScaleDown, @MinReplicas, @MaxReplicas, @ScalePeriod, @TrainInterval,
            COALESCE(
                (SELECT ModelHyperParams FROM {TableName} WHERE ServiceId = @ServiceId),
                CASE WHEN @ModelHyperParams IS NULL THEN NULL ELSE @ModelHyperParams::jsonb END
            ),
            COALESCE(
                (SELECT OptunaConfig FROM {TableName} WHERE ServiceId = @ServiceId),
                CASE WHEN @OptunaConfig IS NULL THEN NULL ELSE @OptunaConfig::jsonb END
            )
        )
        ON CONFLICT (ServiceId) DO UPDATE SET
            ScaleUp = @ScaleUp,
            ScaleDown = @ScaleDown,
            MinReplicas = @MinReplicas,
            MaxReplicas = @MaxReplicas,
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
            MinReplicas = settings.MinReplicas,
            MaxReplicas = settings.MaxReplicas,
            ScalePeriod = settings.ScalePeriod,
            TrainInterval = settings.TrainInterval,
            ModelHyperParams = modelHyperParams,
            OptunaConfig = optunaConfig
        });

        return result > 0;
    }
}