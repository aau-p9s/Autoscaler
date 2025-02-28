using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Persistence.ScaleSettingsRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Kubernetes;

namespace Autoscaler.Runner;

public class Runner
{
    private readonly string _deployment;
    private readonly ISettingsRepository _settingsRepository;

    private readonly Forecaster _forecaster;
    private readonly Prometheus _prometheus;
    private readonly Kubernetes.Kubernetes _kubernetes;

    public Runner(string deployment, string forecasterAddress, string kubernetesAddress, string prometheusAddress,
        ISettingsRepository settingsRepository)
    {
        this._deployment = deployment;
        _forecaster = new(forecasterAddress);
        _kubernetes = new(kubernetesAddress);
        _prometheus = new(prometheusAddress);
        _settingsRepository = settingsRepository;
        Console.WriteLine("Created runner");
    }

    public async void MainLoop()
    {
        while (true)
        {
            // TODO: get real scale settings from scale settings repository
            // var scaleUp = 50;
            // var scaleDown = 20;
            var scalePeriod = 50000;
            var startupTime = 60;
            var queryType = "cpu";

            var target = new Dictionary<string, string>()
            {
                { "cpu", "container_cpu_usage_seconds_total" },
                { "memory", "container_memory_usage_bytes" },
                { "network", "container_network_receive_bytes_total" }
            }[queryType];
            var query =
                $"(sum(rate({target}{{container=~\"{_deployment}\"}}))/count({target}{{container=~\"{_deployment}\"}}))*100";
            Console.WriteLine($"PromQL: {query}");

            var data = await _prometheus.QueryRange(query, DateTime.Now.AddHours(-12), DateTime.Now, scalePeriod);

            Console.WriteLine(data.Count());

            var forecast = _forecaster.Forecast(data);

            var replicas = await Replicas();
            if (true) // TODO: forecast.value > scaleUp
                replicas++;
            //if (false) // TODO: forecast.value <= scaleDown && replicas > 1
            //    replicas--;
            //if (false) // TODO: replicas < 1
            //    replicas = 1;

            Console.WriteLine(replicas);

            await _kubernetes.Update($"/apis/apps/v1/namespaces/default/deployments/{_deployment}/scale",
                new Dictionary<string, Dictionary<string, int>>()
                {
                    {
                        "spec", new()
                        {
                            {
                                "replicas", replicas
                            }
                        }
                    }
                });

            Console.WriteLine("sleeping...");
            var delay = scalePeriod - startupTime; //
            if (true) // TODO: forecast.timestamp > (Datetime.Now - delay)
                Thread.Sleep(delay);
        }
    }

    private async Task<int> Replicas()
    {
        var json = await _kubernetes.Get($"/apis/apps/v1/namespaces/default/deployments/{_deployment}/scale");
        if (json == null)
            return 0;
        var spec = json["spec"];
        if (spec == null)
            return 0;
        var replicas = spec["replicas"];
        if (replicas == null)
            return 0;
        return (int)replicas;
    }
}