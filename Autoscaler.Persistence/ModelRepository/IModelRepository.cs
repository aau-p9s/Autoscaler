using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.ModelRepository;

public interface IModelRepository
{
    public Task<IEnumerable<ModelEntity>> GetModelsForServiceAsync(Guid serviceId);
    public Task<bool> InsertModelsForServiceAsync(Guid serviceId);
}