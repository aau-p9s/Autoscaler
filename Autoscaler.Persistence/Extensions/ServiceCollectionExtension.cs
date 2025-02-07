using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.Dapper;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Autoscaler.Persistence.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection ConfigurePersistenceMySqlConnection(this IServiceCollection services,
        string? connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(new MySqlConnectionFactory(connectionString));

        // Add repositories
        

        // Dapper
        SqlMapper.AddTypeHandler(new GuidTypeHandler());

        return services;
    }
}