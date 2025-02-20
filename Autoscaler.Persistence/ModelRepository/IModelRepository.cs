using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Autoscaler.Persistence.ModelRepository;

public interface IModelRepository
{
    Task<IEnumerable<ModelEntity>> GetModelsForServiceAsync(Guid serviceId);
}