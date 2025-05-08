using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.ForecastRepository;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class MockForecasterService(AppSettings appSettings, ILogger logger) : ForecasterService(appSettings, logger)
{
    private bool UseForecasterInDevelopmentMode => appSettings.Autoscaler.UseForecasterInDevelopmentMode;
    public override async Task<bool> Forecast(Guid serviceId, IForecastRepository repository)
    {
        if (UseForecasterInDevelopmentMode)
        {

            return await base.Forecast(serviceId, repository);
        }
        Logger.LogWarning("Running Mock Forecaster");
        Thread.Sleep(2000);
       
        var forecast = await File.ReadAllTextAsync(
            "./DevelopmentData/forecast.json");
        await repository.InsertForecast(new ForecastEntity(Guid.NewGuid(), serviceId,
            DateTime.Now, Guid.NewGuid(), forecast, false)); 
        
        return true;

    }

    public async override Task<bool> Retrain(Guid serviceId)
    {
       Logger.LogWarning("Running Mock Retrainer");
       Thread.Sleep(100000);
       return true;
    }
}