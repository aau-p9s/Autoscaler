using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Runner.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner.Services;

public class MockKubernetesService(AppSettings appSettings, ILogger logger) : KubernetesService(appSettings, logger)
{
    private int replicas = 1;

    public override Task SetReplicas(DeploymentEntity deployment, int replicas)
    {
        this.replicas = replicas;
        return Task.CompletedTask;
    }

    public override async Task<int> GetReplicas(DeploymentEntity deployment)
    {
        Logger.LogWarning($"Running Mock Kubernetes GET Replicas: {deployment.Service.Name}");
        return replicas;
    }

    protected override async Task<JObject?> GetPodsAsync(DeploymentEntity deployment)
    {
        var items = new JArray();
        for (var i = 0; i < replicas; i++)
        {
            items.Add(new JObject()
            {
                ["metadata"] = new JObject()
                {
                    ["creationTimestamp"] = DateTime.Now.ToString("yyy-MM-ddTHH:mm:ss.fffZ")
                },
                ["status"] = new JObject()
                {
                    ["conditions"] = new JArray()
                    {
                        new JObject()
                        {
                            ["type"] = "Ready",
                            ["status"] = "True",
                            ["lastTransitionTime"] = (DateTime.Now + TimeSpan.FromSeconds(10)).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        }
                    }
                }
            });
        }

        var obj = new JObject()
        {
            ["items"] = items
        };
        return obj;
    }

    public override async Task<List<string>> GetDeployments()
    {
        var result = new List<string>()
        {
            "workload-0-api",
            "workload-1-api"
        };
        return result;
    }
}