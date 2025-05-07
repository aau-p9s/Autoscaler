using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class ForecasterService
{
    private readonly string addr;
    private readonly bool useMockData;
    private readonly bool useForecasterInDevelopmentMode;
    readonly HttpClient _client;
    private readonly ILogger logger;


    public ForecasterService(AppSettings appSettings, ILogger logger)
    {
        addr = appSettings.Autoscaler.Apis.Forecaster;
        useMockData = appSettings.Autoscaler.DevelopmentMode;
        useForecasterInDevelopmentMode = appSettings.Autoscaler.UseForecasterInDevelopmentMode;
        _client = new();
        this.logger = logger;
    }

    public async Task<bool> Forecast(Guid serviceId)
    {
        if (useMockData && !useForecasterInDevelopmentMode)
        {
            logger.LogWarning("Using mock Forecaster data...");
            Thread.Sleep(20000);
            return true;
        }


        var res = await _client.GetAsync(addr + "/predict/" + serviceId);
        logger.LogDebug($"Forecaster forecast response: {await res.Content.ReadAsStringAsync()}");

        if (!res.IsSuccessStatusCode)
        {
            logger.LogError("Failed to forecast the data");
            return false;
        }

        return true;
    }

    public async Task<bool> Retrain(Guid serviceId)
    {
        if (useMockData)
        {
            logger.LogWarning("Using mock Forecaster data...");
            Thread.Sleep(100000);
            return true;
        }

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("serviceId", serviceId.ToString())
        });

        var res = await _client.PostAsync(addr + "/train", content);

        if (!res.IsSuccessStatusCode)
        {
            logger.LogError("Failed to retrain the model");
            return false;
        }

        return true;
    }
}