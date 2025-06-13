using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner.Services;

public class MockForecasterService(AppSettings appSettings, ILogger logger, IForecastRepository forecastRepository,
    ISettingsRepository settingsRepository) : ForecasterService(appSettings, logger, settingsRepository)
{
    private IForecastRepository ForecastRepository => forecastRepository;

    public override async Task<bool> Forecast(DeploymentEntity deployment, TimeSpan forecastHorizon)
    {
        Logger.LogWarning("Running Mock Forecaster");
        //await Task.Delay(2000);
        
        var data = new List<List<double>>();
        var index = new List<DateTime>();
        var start = DateTime.Now.Subtract(TimeSpan.FromMinutes(10));
        var end = start.Add(TimeSpan.FromMilliseconds(deployment.Settings.ScalePeriod));
        var current = start;
        while(current < end)
        {
            var value = ((current - start).TotalSeconds / (end - start).TotalSeconds) * (deployment.Settings.ScaleUp * deployment.Settings.MaxReplicas);
            data.Add([value]);
            index.Add(current);
            current += forecastHorizon;
        }

        var forecast = new Dictionary<string, object>()
        {
            {"data", data},
            {"index", index}
        };
        var forecastString = JsonSerializer.Serialize(forecast);
        await ForecastRepository.UpsertForecast(new ForecastEntity(Guid.NewGuid(), deployment.Service.Id,
            DateTime.Now, Guid.NewGuid(), forecastString, false));

        return true;
    }

    public override async Task<bool> Retrain(DeploymentEntity deployment, TimeSpan forecastHorizon)
    {
        var delay = TimeSpan.FromSeconds(30);
        Logger.LogWarning($"Running Mock Retrainer for {delay} seconds");
        //await Task.Delay(delay);
        return true;
    }
}