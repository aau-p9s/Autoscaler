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
}