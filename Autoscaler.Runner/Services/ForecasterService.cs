using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class ForecasterService(
    AppSettings appSettings,
    ILogger logger)
{
    private string Addr => appSettings.Autoscaler.Apis.Forecaster;
    private bool UseMockData => appSettings.Autoscaler.DevelopmentMode;
    private bool UseForecasterInDevelopmentMode => appSettings.Autoscaler.UseForecasterInDevelopmentMode;
    private HttpClient Client => new();
    private ILogger Logger => logger;

    public async Task<bool> Forecast(Guid serviceId)
    {
        if (UseMockData && !UseForecasterInDevelopmentMode)
        {
            Logger.LogWarning("Using mock Forecaster data...");
            Thread.Sleep(20000);
            return true;
        }


        var res = await Client.GetAsync(Addr + "/predict/" + serviceId);
        Logger.LogDebug($"Forecaster forecast response: {await res.Content.ReadAsStringAsync()}");

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to forecast the data");
            return false;
        }

        return true;
    }

    public async Task<bool> Retrain(Guid serviceId)
    {
        if (UseMockData)
        {
            Logger.LogWarning("Using mock Forecaster data...");
            Thread.Sleep(100000);
            return true;
        }

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("serviceId", serviceId.ToString())
        });

        var res = await Client.PostAsync(Addr + "/train", content);

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to retrain the model");
            return false;
        }

        return true;
    }
}