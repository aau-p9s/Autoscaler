using System;
using System.Collections.Generic;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;

namespace Autoscaler.Runner.Services;

public class ForecasterService
{
    private readonly string _addr;

    public ForecastEntity Forecast(IEnumerable<HistoricEntity> data)
    {
        return new();
    }
    
}