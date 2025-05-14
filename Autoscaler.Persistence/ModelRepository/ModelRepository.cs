using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Dapper;

namespace Autoscaler.Persistence.ModelRepository;

public class ModelRepository : IModelRepository
{
    private static readonly string TableName = "Models";
    private static readonly string BaselineTablename = "BaselineModels";
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.Connection;

    public ModelRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<ModelEntity>> GetModelsForServiceAsync(Guid serviceId)
    {
        var models = await Connection.QueryAsync<ModelEntity>($"SELECT * FROM {TableName} WHERE ServiceId = @ServiceId",
            new { ServiceId = serviceId });
        return models;
    }
    
    public async Task<bool> InsertModelsForServiceAsync(Guid serviceId)
    {
        var sql = $"INSERT INTO {TableName} (Id, ServiceId, Name, Bin, Ckpt, TrainedAt) " +
                  "VALUES (@Id, @ServiceId, @Name, @Bin, @Ckpt, @TrainedAt)";
        
        var models = await Connection.QueryAsync<ModelEntity>($"SELECT * FROM {BaselineTablename}",
            new { ServiceId = serviceId });
        
        var conn = Connection;
        conn.Open();
        using var tx = conn.BeginTransaction();
        
        foreach (var model in models)
        {
            var param = new
            {
                Id         = Guid.NewGuid(),
                ServiceId  = serviceId,
                Name       = model.Name,
                Bin        = model.Model,
                Ckpt       = (object)model.Ckpt ?? DBNull.Value,
                TrainedAt  = DateTime.UtcNow
            };

            await conn.ExecuteAsync(sql, param, tx);
        }
        
        tx.Commit();
        return true;
    }
}