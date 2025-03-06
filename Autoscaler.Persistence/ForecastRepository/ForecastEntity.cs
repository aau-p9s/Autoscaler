using System;
using System.Text.Json.Nodes;

namespace Autoscaler.Persistence.ForecastRepository;

public class ForecastEntity
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ModelId { get; set; }
    public string Forecast { get; set; }
    public bool HasManualChange { get; set; }

    //Empty constructor for Dapper
    public ForecastEntity()
    {
    }

    public ForecastEntity(Guid id, Guid serviceId, DateTime createdAt, Guid modelId, string forecast,
        bool hasManualChange)
    {
        Id = id;
        ServiceId = serviceId;
        CreatedAt = createdAt;
        ModelId = modelId;
        Forecast = forecast;
        HasManualChange = hasManualChange;
    }
}