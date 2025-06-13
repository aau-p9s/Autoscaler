using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Autoscaler.Config;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Runner.Entities;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class PrometheusService(
    AppSettings appSettings,
    ILogger logger)
{
    private AppSettings AppSettings => appSettings;
    private HttpClient Client => new();
    private const string Rate = "5m";
    public string Type = "sum";
    protected ILogger Logger => logger;

    private async Task<string> QueryRange(string query, DateTime start, DateTime end, TimeSpan horizon)
    {
        Logger.LogDebug($"PromQL: {query}");

        HttpResponseMessage response;
        try
        {
            response = await Client.GetAsync($"{
                AppSettings.Autoscaler.Apis.Prometheus.Url
            }/api/v1/query_range?query={
                Encode(query)
            }&start={
                Encode(Utils.ToRFC3339(start))
            }&end={
                Encode(Utils.ToRFC3339(end))
            }&step={
                horizon.TotalSeconds
            }s");
        }
        catch (Exception e)
        {
            Logger.LogError("Prometheus seems to be down");
            Utils.HandleException(e, Logger);
            return "";
        }

        var jsonString = await response.Content.ReadAsStringAsync();
        Logger.LogDebug($"Prometheus response: {jsonString}");

        return jsonString;
    }

    public virtual async Task<HistoricEntity> SumCpuUsage(DeploymentEntity deployment, DateTime start, DateTime end,
        TimeSpan horizon)
    {
        Type = "sum";
        // 200 is cpu utilization for 1/2 cpu's
        var query =
            $"sum(rate(container_cpu_usage_seconds_total{{container=\"{deployment.Service.Name}\"}}[{Rate}])) * 200";
        var raw = await QueryRange(query, start, end, horizon);
        return new HistoricEntity(Guid.NewGuid(), deployment.Service.Id, DateTime.Now, raw);
    }

    public virtual async Task<HistoricEntity> AvgCpuUsage(DeploymentEntity deployment, DateTime start, DateTime end,
        TimeSpan horizon)
    {
        Type = "avg";
        var query =
            $"avg(rate(container_cpu_usage_seconds_total{{container=\"{deployment.Service.Name}\"}}[{Rate}])) * 200";
        var raw = await QueryRange(query, start, end, horizon);
        return new HistoricEntity(Guid.NewGuid(), deployment.Service.Id, DateTime.Now, raw);
    }

    private static string Encode(string target)
    {
        return HttpUtility.UrlEncode(target);
    }
}
