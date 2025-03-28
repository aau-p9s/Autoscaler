using System;

namespace Autoscaler.Persistence.SettingsRepository;

public class SettingsEntity
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public int ScaleUp { get; set; }
    public int ScaleDown { get; set; }
    public int MinReplicas { get; set; }
    public int MaxReplicas { get; set; }
    public int ScalePeriod { get; set; }
    public int TrainInterval { get; set; }
    public string ModelHyperParams { get; set; }
    public string OptunaConfig { get; set; }

    //Empty constructor for Dapper
    public SettingsEntity()
    {
    }

    public SettingsEntity(Guid id, Guid serviceId, int scaleUp, int scaleDown, int minReplicas, int maxReplicas,
        int scalePeriod, int trainInterval, string modelHyperParams, string optunaConfig)
    {
        Id = id;
        ServiceId = serviceId;
        ScaleUp = scaleUp;
        ScaleDown = scaleDown;
        MinReplicas = minReplicas;
        MaxReplicas = maxReplicas;
        ScalePeriod = scalePeriod;
        TrainInterval = trainInterval;
        ModelHyperParams = modelHyperParams;
        OptunaConfig = optunaConfig;
    }
}