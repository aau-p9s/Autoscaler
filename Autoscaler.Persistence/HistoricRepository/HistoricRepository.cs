using System;
using System.Data;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Dapper;

namespace Autoscaler.Persistence.HistoricRepository;

public class HistoricRepository : IHistoricRepository
{
    private static readonly string TableName = "HistoricData";
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.Connection;

    public HistoricRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HistoricEntity> GetHistoricDataByServiceIdAsync(Guid serviceId)
    {
        var historicData = await Connection.QueryFirstOrDefaultAsync<HistoricEntity>(
            $"SELECT * FROM {TableName} WHERE ServiceId = @ServiceId", new { ServiceId = serviceId });
        return historicData;
    }

    public async Task<bool> UpsertHistoricDataAsync(HistoricEntity historicEntity)
    {
        var parameters = new
        {
            Id = historicEntity.Id,
            ServiceId = historicEntity.ServiceId,
            CreatedAt = historicEntity.CreatedAt,
            HistoricData = historicEntity.HistoricData
        };
        await Connection.ExecuteAsync($@"
                    DELETE FROM {TableName} 
                    WHERE ServiceId = @ServiceId",
            parameters);

        var result = await Connection.ExecuteAsync($@"
                    INSERT INTO {TableName} (Id, ServiceId, CreatedAt, HistoricData)
                    VALUES (@Id, @ServiceId, @CreatedAt, CAST(@HistoricData AS jsonb))
                    ON CONFLICT (Id) DO UPDATE SET 
                HistoricData = CAST(@HistoricData AS jsonb), 
                CreatedAt = @CreatedAt",
            parameters);


        return result > 0;
    }
}