using System;
using System.Collections.Generic;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;

namespace Autoscaler.Runner.Kubernetes;

public class Forecaster : IAPI
{
    private readonly string _addr;
    // add forecaster REST API bindings

    public Forecaster(string addr)
    {
        _addr = addr;
        if (!IsUp())
        {
            Console.WriteLine("Forecaster shouldn't be down");
            Environment.Exit(1);
        }
    }

    public ForecastEntity Forecast(IEnumerable<HistoricEntity> data)
    {
        return new();
    }

    public bool IsUp()
    {
        return false;
    }
}