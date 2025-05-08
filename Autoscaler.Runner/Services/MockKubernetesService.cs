using System.IO;
using System.Threading.Tasks;
using Autoscaler.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner.Services;

public class MockKubernetesService(AppSettings appSettings, ILogger logger) : KubernetesService(appSettings, logger)
{
    public override async Task Update(string endpoint, object body)
    {
        Logger.LogWarning("Using mock Kubernetes data...");
    }
    
    public override async Task<JObject?> Get(string endpoint)
    {
        Logger.LogWarning($"Running Mock Kubernetes GET Request: {endpoint}");
        var kubeRes =
            await File.ReadAllTextAsync(
                $"./DevelopmentData/kubernetes_GET{endpoint.Replace("/", "_")}.json");
        return JObject.Parse(kubeRes);
    }

    public override async Task<int> GetReplicas(string deploymentName)
    {
        Logger.LogWarning($"Running Mock Kubernetes GET Replicas: {deploymentName}");
        var kubeRes =
            await File.ReadAllTextAsync(
                $"./DevelopmentData/kubectl_GET__apis_apps_v1_namespaces_default_deployments_workload-api-deployment_scale.json");
        var dummyJson = JObject.Parse(kubeRes);
        var dummySpec = dummyJson["spec"];
        var dummyReplicas = dummySpec?["replicas"];
        if (dummyReplicas == null)
            return 0;
        return (int)dummyReplicas;
    }

    protected override async Task<JObject?> GetPodsAsync(string serviceName)
    {
        Logger.LogWarning("Running Mock Kubernetes GET pods");
        var podsJson = await File.ReadAllTextAsync("./DevelopmentData/containers.json");
        return JObject.Parse(podsJson);
    }
}