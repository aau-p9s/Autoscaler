using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.Dapper;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ModelRepository;
using Autoscaler.Persistence.ScaleSettingsRepository;
using Autoscaler.Persistence.ServicesRepository;
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
        services.AddScoped<IModelRepository, ModelRepository.ModelRepository>();
        services.AddScoped<IScaleSettingsRepository, ScaleSettingsRepository.ScaleSettingsRepository>();
        services.AddScoped<IForecastRepository, ForecastRepository.ForecastRepository>();
        services.AddScoped<IHistoricRepository, HistoricRepository.HistoricRepository>();
        services.AddScoped<IServicesRepository, ServicesRepository.ServicesRepository>();

        // Dapper
        SqlMapper.AddTypeHandler(new GuidTypeHandler());

        return services;
    }
}