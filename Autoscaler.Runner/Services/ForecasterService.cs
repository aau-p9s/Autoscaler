using System;
using System.Collections.Generic;
using System.Net;
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
        var url = $"{AppSettings.Autoscaler.Apis.Forecaster}/predict/{serviceId}/{forecastHorizon}";
        var res = await Client.PostAsync(url, new StringContent(""));
        Logger.LogDebug($"Forecaster forecast response: {await res.Content.ReadAsStringAsync()}");

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to forecast the data");
            return false;
        }

        await Wait(url);

        return true;
    }

    public virtual async  Task<bool> Retrain(Guid serviceId, int forecastHorizon)
    {
        var url = $"{AppSettings.Autoscaler.Apis.Forecaster}/train/{serviceId}/{forecastHorizon}";
        var res = await Client.PostAsync(url, new StringContent(""));

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to retrain the model");
            return false;
        }
        
        // wait for finishing
        await Wait(url);
        
        return true;
    }

    private async Task Wait(string url)
    {
        var status = HttpStatusCode.Accepted;
        while (status != HttpStatusCode.OK)
        {
            var res = await Client.GetAsync(url);
            status = res.StatusCode;
            Thread.Sleep(1000);
        }
    }
}