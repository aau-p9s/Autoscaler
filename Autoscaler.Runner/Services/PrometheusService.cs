using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Autoscaler.Config;
using Autoscaler.Persistence.HistoricRepository;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class PrometheusService(
    AppSettings appSettings,
    ILogger logger)
{
    private AppSettings AppSettings => appSettings;
    private HttpClient Client => new();
    private const string Rate = "5m";
    protected ILogger Logger => logger;

    public virtual async Task<HistoricEntity> QueryRange(Guid serviceId, string deployment, DateTime start, DateTime end,
        int period)
    {
        var queryType = "cpu";


        var target = new Dictionary<string, string>()
        {
            { "cpu", "container_cpu_usage_seconds_total" },
            { "memory", "container_memory_usage_bytes" },
            { "network", "container_network_receive_bytes_total" }
        }[queryType];
        var queryString =
            $"sum(rate({target}{{container=\"{deployment}\"}}[{Rate}])) / sum(machine_cpu_cores) * 100";
        Logger.LogDebug($"PromQL: {queryString}");

        var query =
            $"query={EncodeQuery(queryString)}&start={Utils.ToRFC3339(start)}&end={Utils.ToRFC3339(end)}&step={period / 1000}s";
        HttpResponseMessage response;
        try
        {
            response = await Client.GetAsync($"{AppSettings.Autoscaler.Apis.Prometheus}/api/v1/query_range?{query}");
        }
        catch (Exception e)
        {
            Logger.LogError("Prometheus seems to be down");
            Utils.HandleException(e, Logger);
            return new HistoricEntity();
        }

        var jsonString = await response.Content.ReadAsStringAsync();
        Logger.LogDebug($"Prometheus response: {jsonString}");

        return new HistoricEntity(Guid.NewGuid(), serviceId, DateTime.Now, jsonString);
    }

    private static string EncodeQuery(string target)
    {
        return HttpUtility.UrlEncode(target);
    }
}
