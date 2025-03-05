using System;
using System.Text.Json.Nodes;

namespace Autoscaler.Persistence.SettingsRepository;

public class SettingsEntity
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public int ScaleUp { get; set; }
    public int ScaleDown { get; set; }
    public int ScalePeriod { get; set; }
    public int TrainInterval { get; set; }
    //Change the type of Hyperparameters and OptunaConfig to custom objects when we know what they have in them
    public string ModelHyperParams { get; set; }
    public string OptunaConfig { get; set; }

    public JsonObject GetOptunaConfig()
    {
        return OptunaConfig != null ? JsonNode.Parse(OptunaConfig)?.AsObject() : null;
    }

    public JsonObject GetHyperparameters()
    {
        return ModelHyperParams != null ? JsonNode.Parse(ModelHyperParams)?.AsObject() : null;
    }

    //Empty constructor for Dapper
    public SettingsEntity()
    {
    }

    public SettingsEntity(Guid id, Guid serviceId, int scaleUp, int scaleDown, int scalePeriod, int trainInterval, string modelHyperParams, string optunaConfig)
    {
        Id = id;
        ServiceId = serviceId;
        ScaleUp = scaleUp;
        ScaleDown = scaleDown;
        ScalePeriod = scalePeriod;
        TrainInterval = trainInterval;
        ModelHyperParams = modelHyperParams;
        OptunaConfig = optunaConfig;
    }
}