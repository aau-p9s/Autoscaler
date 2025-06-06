using System;
using Autoscaler.Config;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner;

public class Utils(
    AppSettings appSettings)
{
    TimeSpan MockOffset => (DateTime.Now - DateTime.UnixEpoch.AddSeconds(1741084020.94));
    public string ToRFC3339(DateTime date)
    {
        return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
    }

    public void HandleException(Exception e, ILogger logger)
    {
        logger.LogError(e.Message);
        logger.LogDebug(e.StackTrace);
        if (e.InnerException != null)
            HandleException(e.InnerException, logger);
    }
    public DateTime Now()
    {
        if (appSettings.Autoscaler.Apis.Prometheus.Mock)
        {
            return DateTime.Now - MockOffset;
        }
        else
        {
            return DateTime.Now;
        }
    }
}