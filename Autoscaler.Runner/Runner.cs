using Autoscaler.Runner.Kubernetes;

class Runner
{
    private readonly string deployment;
    private readonly string kubernetesAddress;
    private readonly string prometheusAddress;
    private readonly IScaleSettingsRepository _scaleSettingsRepository;

    public Runner(string deployment, string kubernetesAddress, string prometheusAddress, IScaleSettingsRepository scaleSettingsRepository) {
        this.deployment = deployment;
        this.kubernetesAddress = kubernetesAddress;
        this.prometheusAddress = prometheusAddress;
        _scaleSettingsRepository = scaleSettingsRepository;

    }

    public async void MainLoop()
    {
        Forecaster forecaster = new();
        Prometheus prometheus = new(prometheusAddress);
        Kubernetes kubernetes = new(kubernetesAddress);
        while(true)
        {
        }
    }
}
