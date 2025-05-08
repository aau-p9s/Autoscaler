using System;
using Autoscaler.Config;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public interface IApi
{

    protected void HandleException(Exception exception, ILogger logger)
    {
        while (true)
        {
            logger.LogError(exception.Message);
            logger.LogDebug(exception.StackTrace);
            if (exception.InnerException != null)
            {
                exception = exception.InnerException;
                continue;
            }

            break;
        }
    }
}