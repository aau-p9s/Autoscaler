using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Runner.Entities;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class MockPrometheusService(AppSettings appSettings, ILogger logger) : PrometheusService(appSettings, logger)
{
    private class PrometheusResult
    {
        [JsonPropertyName("metric")]
        public required Dictionary<string, string> Metric { get; set; }

        [JsonPropertyName("values")]
        public required List<List<object>> Values { get; set; }
    }

    private class PrometheusData
    {
        [JsonPropertyName("resultType")]
        public required string ResultType { get; set; }

        [JsonPropertyName("result")]
        public required List<PrometheusResult> Result { get; set; }
    }

    private class PrometheusResponse
    {
        [JsonPropertyName("status")]
        public required string Status { get; set; }

        [JsonPropertyName("data")]
        public required PrometheusData Data { get; set; }
    }


    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };
    
    public async override Task<HistoricEntity> SumCpuUsage(DeploymentEntity deployment, DateTime start, DateTime end, TimeSpan horizon)
    {
        const double amplitude = 2.5;
        const double baseline = 20.0;

        var values = new List<List<object>>();
        var totalPoints = (int)((end - start).TotalSeconds / horizon.TotalSeconds);

        for (var i = 0; i <= totalPoints; i++)
        {
            var currentTime = start.AddSeconds(i * horizon.TotalSeconds);
            double unixTimestamp = new DateTimeOffset(currentTime).ToUnixTimeSeconds();

            // 1 full sine wave every 30 points
            var secondsSinceStart = i * horizon.TotalSeconds;
            var angle = (2 * Math.PI * secondsSinceStart) / 86400.0; // 86400 = 1 day...
            var value = baseline + amplitude * Math.Sin(angle);

            values.Add([unixTimestamp, value.ToString("F12")]);
        }

        var result = new PrometheusResult
        {
            Metric = new Dictionary<string, string>
            {
                { "service_id", deployment.Service.Id.ToString() },
                { "deployment", deployment.Service.Name }
            },
            Values = values
        };

        var response = new PrometheusResponse
        {
            Status = "success",
            Data = new PrometheusData
            {
                ResultType = "matrix",
                Result = [result]
            }
        };


        return new HistoricEntity(Guid.NewGuid(), deployment.Service.Id, DateTime.Now, JsonSerializer.Serialize(response, Options));
    }
 
    public override Task<HistoricEntity> AvgCpuUsage(DeploymentEntity deployment, DateTime start, DateTime end, TimeSpan horizon)
    {
        return SumCpuUsage(deployment, start, end, horizon);
    }
}