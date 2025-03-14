using System;
using System.Data;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Dapper;

namespace Autoscaler.Persistence.ForecastRepository;

public class ForecastRepository : IForecastRepository
{
    private static readonly string TableName = "Forecasts";
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.Connection;

    public ForecastRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }


    public async Task<ForecastEntity> GetForecastsByServiceIdAsync(Guid serviceId)
    {
        var forecasts = await Connection.QueryFirstOrDefaultAsync<ForecastEntity>(
            $"SELECT * FROM {TableName} WHERE ServiceId = @ServiceId", new { ServiceId = serviceId });
        return forecasts;
    }

    public async Task<ForecastEntity> GetForecastByIdAsync(Guid id)
    {
        var forecast =
            await Connection.QueryFirstOrDefaultAsync<ForecastEntity>($"SELECT * FROM {TableName} WHERE Id = @Id",
                new { Id = id });
        return forecast;
    }

    public async Task<bool> UpdateForecastAsync(ForecastEntity forecast)
    {
        var query =
            $"UPDATE {TableName} SET Forecast = CAST(@Forecast AS jsonb), HasManualChange = @HasManualChange WHERE Id = @Id";

        var parameters = new
        {
            Forecast = forecast.Forecast,
            Id = forecast.Id,
            HasManualChange = forecast.HasManualChange
        };

        var result = await Connection.ExecuteAsync(query, parameters);
        return result > 0;
    }

    //ONLY USE FOR DEVMODE
    public async Task<bool> InsertForecast(ForecastEntity forecast)
    {
        var query =
            $"INSERT INTO {TableName} (Id, ServiceId, Forecast, HasManualChange) VALUES (@Id, @ServiceId, CAST(@Forecast AS jsonb), @HasManualChange)";

        var parameters = new
        {
            Id = forecast.Id,
            ServiceId = forecast.ServiceId,
            Forecast = forecast.Forecast,
            HasManualChange = forecast.HasManualChange
        };

        var result = await Connection.ExecuteAsync(query, parameters);
        return result > 0;
    }
}