﻿using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ModelRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Microsoft.Extensions.DependencyInjection;

namespace Autoscaler.Persistence.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection ConfigurePersistencePostGreSqlConnection(this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(provider => new NpgsqlConnectionFactory(connectionString));

        // Add repositories
        services.AddScoped<IModelRepository, ModelRepository.ModelRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository.SettingsRepository>();
        services.AddScoped<IForecastRepository, ForecastRepository.ForecastRepository>();
        services.AddScoped<IHistoricRepository, HistoricRepository.HistoricRepository>();
        services.AddScoped<IServicesRepository, ServicesRepository.ServicesRepository>();

        return services;
    }
}