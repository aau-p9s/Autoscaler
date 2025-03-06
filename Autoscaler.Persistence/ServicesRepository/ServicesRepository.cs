using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Dapper;

namespace Autoscaler.Persistence.ServicesRepository;

public class ServicesRepository : IServicesRepository
{
    private static readonly string TableName = "Services";
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.Connection;

    public ServicesRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<ServiceEntity>> GetAllServicesAsync()
    {
        var services = await Connection.QueryAsync<ServiceEntity>($"SELECT * FROM {TableName}");
        return services.AsList();
    }

    public async Task<ServiceEntity> GetServiceByIdAsync(Guid serviceId)
    {
        var service =
            await Connection.QueryFirstOrDefaultAsync<ServiceEntity>($"SELECT * FROM {TableName} WHERE Id = @Id",
                new { Id = serviceId });
        return service;
    }

    public async Task<Guid> GetServiceIdByNameAsync(string serviceName)
    {
        var service =
            await Connection.QueryFirstOrDefaultAsync<ServiceEntity>($"SELECT * FROM {TableName} WHERE Name = @Name",
                new { Name = serviceName });
        return service?.Id ?? Guid.Empty;
    }

    public async Task<bool> UpsertServiceAsync(ServiceEntity service)
    {
        var result = await Connection.ExecuteAsync($@"
            INSERT INTO {TableName} (Id, Name, AutoscalingEnabled)
            VALUES (@Id, @Name, @AutoscalingEnabled)
            ON CONFLICT (Id) DO UPDATE SET
                Name = @Name,
                AutoscalingEnabled = @AutoscalingEnabled",
            service);
        return result > 0;
    }
}