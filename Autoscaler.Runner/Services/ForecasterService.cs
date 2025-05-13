using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.ForecastRepository;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class ForecasterService(
    AppSettings appSettings,
    ILogger logger)
{
    protected AppSettings AppSettings => appSettings;
    private HttpClient Client => new();
    protected ILogger Logger => logger;

    public virtual async Task<bool> Forecast(Guid serviceId, int forecastHorizon)
    {
        var res = await Client.GetAsync($"{AppSettings.Autoscaler.Apis.Forecaster}/predict/{serviceId}/{forecastHorizon}");
        Logger.LogDebug($"Forecaster forecast response: {await res.Content.ReadAsStringAsync()}");

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to forecast the data");
            return false;
        }

        return true;
    }

    public virtual async  Task<bool> Retrain(Guid serviceId, int forecastHorizon)
    {
        var res = await Client.PostAsync($"{AppSettings.Autoscaler.Apis.Forecaster}/train/{serviceId}/{forecastHorizon}", new StringContent(""));

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to retrain the model");
            return false;
        }

        return true;
    }
}