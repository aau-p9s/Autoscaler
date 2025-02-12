using Autoscaler.Runner.Kubernetes;

using Autoscaler.Persistence.ScaleSettingsRepository;

class Runner
{
    private readonly string deployment;
    private readonly IScaleSettingsRepository _scaleSettingsRepository;

    private readonly Forecaster forecaster;
    private readonly Prometheus prometheus;
    private readonly Kubernetes kubernetes;
    public Runner(string deployment, string forecasterAddress, string kubernetesAddress, string prometheusAddress, IScaleSettingsRepository scaleSettingsRepository) {
        this.deployment = deployment;
        forecaster = new(forecasterAddress);
        kubernetes = new(kubernetesAddress);
        prometheus = new(prometheusAddress);
        _scaleSettingsRepository = scaleSettingsRepository;
        Console.WriteLine("Created runner");
    }

    public async void MainLoop()
    {
        while(true)
        {
            // TODO: get real scale settings from scale settings repository
            // var scaleUp = 50;
            // var scaleDown = 20;
            var scalePeriod = 50000;
            var startupTime = 60;
            var queryType = "cpu";

            var target = new Dictionary<string, string>(){
                { "cpu", "container_cpu_usage_seconds_total" },
                { "memory", "container_memory_usage_bytes" },
                { "network", "container_network_receive_bytes_total" }
            }[queryType];
            var query = $"(sum(rate({target}{{container=~\"{deployment}\"}}))/count({target}{{container=~\"{deployment}\"}}))*100";
            Console.WriteLine($"PromQL: {query}");

            var data = await prometheus.QueryRange(query, DateTime.Now.AddHours(-12), DateTime.Now, scalePeriod);

            Console.WriteLine(data.Count());

            var forecast = forecaster.Forecast(data);

            var replicas = await Replicas();
            if (true) // TODO: forecast.value > scaleUp
                replicas++;
            //if (false) // TODO: forecast.value <= scaleDown && replicas > 1
            //    replicas--;
            //if (false) // TODO: replicas < 1
            //    replicas = 1;

            Console.WriteLine(replicas);

            await kubernetes.Update($"/apis/apps/v1/namespaces/default/deployments/{deployment}/scale", new Dictionary<string, Dictionary<string, int>>() {{
                "spec", new() {{
                    "replicas",replicas
                }}
            }});

            Console.WriteLine("sleeping...");
            var delay = scalePeriod - startupTime; //
            if(true) // TODO: forecast.timestamp > (Datetime.Now - delay)
                Thread.Sleep(delay);

        }
    }

    private async Task<int> Replicas() {
        var json = await kubernetes.Get($"/apis/apps/v1/namespaces/default/deployments/{deployment}/scale");
        if (json == null)
            return 0;
        var spec = json["spec"];
        if (spec == null)
            return 0;
        var replicas = spec["replicas"];
        if (replicas == null)
            return 0;
        return (int) replicas;
    }
}
