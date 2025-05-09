using System;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner;

public class Utils
{
    


        public static string ToRFC3339(DateTime date)
        {
            return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
        }
        
        public static void HandleException(Exception e, ILogger logger)
        {
            logger.LogError(e.Message);
            logger.LogDebug(e.StackTrace);
            if (e.InnerException != null)
                HandleException(e.InnerException, logger);
        }
}