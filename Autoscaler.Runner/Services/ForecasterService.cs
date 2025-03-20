using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Autoscaler.Runner.Services;

public class ForecasterService
{
    private readonly string _addr;
    private readonly bool _useMockData;
    private readonly bool _useForecasterInDevelopmentMode;
    readonly HttpClient _client;


    public ForecasterService(string addr, bool useMockData, bool useForecasterInDevelopmentMode)
    {
        _addr = addr;
        _useMockData = useMockData;
        _useForecasterInDevelopmentMode = useForecasterInDevelopmentMode;
        _client = new();
    }

    public async Task<bool> Forecast(Guid serviceId)
    {
        if (_useMockData && !_useForecasterInDevelopmentMode)
        {
            Console.WriteLine("Using mock Forecaster data...");
            Thread.Sleep(20000);
            return true;
        }

  

        var res = await _client.PostAsync(_addr + "/predict/" + serviceId, null);

        if (!res.IsSuccessStatusCode)
        {
            Console.WriteLine("Failed to forecast the data");
            return false;
        }

        return true;
    }

    public async Task<bool> Retrain(Guid serviceId)
    {
        if (_useMockData)
        {
            Console.WriteLine("Using mock Forecaster data...");
            Thread.Sleep(100000);
            return true;
        }

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("serviceId", serviceId.ToString())
        });

        var res = await _client.PostAsync(_addr + "/train", content);

        if (!res.IsSuccessStatusCode)
        {
            Console.WriteLine("Failed to retrain the model");
            return false;
        }

        return true;
    }
}