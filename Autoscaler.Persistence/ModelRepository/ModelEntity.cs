using System;

namespace Autoscaler.Persistence.ModelRepository;

public class ModelEntity
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string Name { get; set; }
    public byte[] Model { get; set; }
    public byte[] Ckpt { get; set; }
    public DateTime TrainedAt { get; set; }

    //Empty constructor for Dapper
    public ModelEntity()
    {
    }

    public ModelEntity(Guid id, Guid serviceId, string name, byte[] model, byte[] ckpt, DateTime trainedAt)
    {
        Id = id;
        ServiceId = serviceId;
        Name = name;
        Model = model;
        Ckpt = ckpt;
        TrainedAt = trainedAt;
    }
}