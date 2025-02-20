using System;
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

    public async Task<ServiceEntity> GetServiceAsync(Guid serviceId)
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
            INSERT INTO {TableName} (Id, Name)
            VALUES (@Id, @Name)
            ON CONFLICT (Id) DO UPDATE SET
                Name = @Name",
            service);
        return result > 0;
    }
}