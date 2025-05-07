using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Autoscaler.Config;
using Autoscaler.Persistence.HistoricRepository;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class PrometheusService
{
    private readonly bool _useMockData;
    readonly string _addr;
    private readonly HttpClient _client;
    private string _rate = "1m";
    private readonly ILogger logger;

    public PrometheusService(AppSettings appSettings, ILogger logger)
    {
        _addr = appSettings.Autoscaler.Apis.Prometheus;
        _client = new();
        _useMockData = appSettings.Autoscaler.DevelopmentMode;
        this.logger = logger;
    }

    public async Task<HistoricEntity> QueryRange(Guid serviceId, string deployment, DateTime start, DateTime end,
        int period)
    {
        var queryType = "cpu";
        if (_useMockData)
        {
            logger.LogInformation("Using mock Prometheus data...");
            var promTrace = await File.ReadAllTextAsync("./DevelopmentData/prometheus_trace.json");
            return new HistoricEntity(Guid.NewGuid(), serviceId, DateTime.Now, promTrace);
        }

        var target = new Dictionary<string, string>()
        {
            { "cpu", "container_cpu_usage_seconds_total" },
            { "memory", "container_memory_usage_bytes" },
            { "network", "container_network_receive_bytes_total" }
        }[queryType];
        var queryString =
            $"sum(rate({target}{{container=\"{deployment}\"}}[{_rate}])) / sum(machine_cpu_cores) * 100";
        logger.LogDebug($"PromQL: {queryString}");

        var query =
            $"query={EncodeQuery(queryString)}&start={ToRFC3339(start)}&end={ToRFC3339(end)}&step={period / 1000}s";
        var result = new HistoricEntity();
        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync($"{_addr}/api/v1/query_range?{query}");
        }
        catch (Exception e)
        {
            logger.LogError("Prometheus seems to be down");
            HandleException(e);
            return new HistoricEntity();
        }

        var jsonString = await response.Content.ReadAsStringAsync();
        logger.LogDebug($"Prometheus response: {jsonString}");

        return new HistoricEntity(Guid.NewGuid(), serviceId, DateTime.Now, jsonString);
    }

    private static string EncodeQuery(string target)
    {
        return HttpUtility.UrlEncode(target);
    }

    private static string ToRFC3339(DateTime date)
    {
        return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
    }

    void HandleException(Exception e)
    {
        // TODO: Move to an interface
        logger.LogError(e.Message);
        if (e.InnerException != null)
            HandleException(e.InnerException);
    }
}
