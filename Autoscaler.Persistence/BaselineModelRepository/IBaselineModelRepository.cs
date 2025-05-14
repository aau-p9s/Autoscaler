using System.Threading.Tasks;

namespace Autoscaler.Persistence.BaselineModelRepository;

public interface IBaselineModelRepository
{
    public Task InsertAllBaselineModels(string modelsRootPath);
}