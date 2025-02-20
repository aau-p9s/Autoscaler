using System;

namespace Autoscaler.Persistence.ScaleSettingsRepository;

public class ScaleSettingsEntity
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public int ScaleUp { get; set; }
    public int ScaleDown { get; set; }
    public int ScalePeriod { get; set; }

    //Empty constructor for Dapper
    public ScaleSettingsEntity()
    {
    }

    public ScaleSettingsEntity(Guid id, Guid serviceId, int scaleUp, int scaleDown, int scalePeriod)
    {
        Id = id;
        ServiceId = serviceId;
        ScaleUp = scaleUp;
        ScaleDown = scaleDown;
        ScalePeriod = scalePeriod;
    }
}