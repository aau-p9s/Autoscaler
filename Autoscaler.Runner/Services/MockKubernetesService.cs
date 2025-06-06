using System;
using System.IO;
using System.Threading.Tasks;
using Autoscaler.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner.Services;

public class MockKubernetesService(AppSettings appSettings, ILogger logger, Utils utils) : KubernetesService(appSettings, logger, utils)
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
        try
        {
            var dummyJson = JObject.Parse(kubeRes);
            var dummySpec = dummyJson["spec"] ??
                            throw new ArgumentNullException(nameof(dummyJson), "Error, mock data is invalid");
            var dummyReplicas = dummySpec["replicas"] ??
                                throw new ArgumentNullException(nameof(dummySpec), "Error, mock data spec is invalid");
            return (int)dummyReplicas;
        }
        catch (ArgumentNullException e)
        {
            utils.HandleException(e, Logger);
            return 0;
        }
    }

    protected override async Task<JObject?> GetPodsAsync(string serviceName)
    {
        Logger.LogWarning("Running Mock Kubernetes GET pods");
        var podsJson = await File.ReadAllTextAsync("./DevelopmentData/containers.json");
        return JObject.Parse(podsJson);
    }
}