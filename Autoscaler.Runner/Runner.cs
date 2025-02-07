using Autoscaler.Runner.Kubernetes;

class Runner
{
    private readonly string deployment;
    private readonly string kubernetesAddress;
    private readonly string prometheusAddress;

    public Runner(string deployment, string kubernetesAddress, string prometheusAddress) {
        this.deployment = deployment;
        this.kubernetesAddress = kubernetesAddress;
        this.prometheusAddress = prometheusAddress;
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
