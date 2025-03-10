using System;
using System.Text.Json.Nodes;

namespace Autoscaler.Persistence.HistoricRepository;

public class HistoricEntity
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public JsonObject HistoricData { get; set; }

    // Empty constructor for Dapper
    public HistoricEntity()
    {
    }

    public HistoricEntity(Guid id, Guid serviceId, DateTime createdAt, JsonObject historicData)
    {
        Id = id;
        ServiceId = serviceId;
        CreatedAt = createdAt;
        HistoricData = historicData;
    }
}