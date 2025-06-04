using System;
using System.IO;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.HistoricRepository;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class MockPrometheusService(AppSettings appSettings, ILogger logger) : PrometheusService(appSettings, logger)
{
    public override async Task<HistoricEntity> QueryRange(Guid serviceId, string deployment, DateTime start,
        DateTime end, int period)
    {
        Logger.LogInformation("Running Mock Prometheus QueryRange");
        var promTrace = await File.ReadAllTextAsync("./DevelopmentData/prometheus_trace.json");
        return new HistoricEntity(Guid.NewGuid(), serviceId, DateTime.Now, promTrace);
    }
}