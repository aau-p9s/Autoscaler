using Autoscaler.Lib.Forecasts;
using Autoscaler.Lib.Kubernetes;

namespace Autoscaler.Scaler;

class Scaler {
    

    private readonly Lib.Database.Database Database;
    private readonly string Deployment;
    private readonly int Period;
    readonly string KubeAddr;
    readonly string PrometheusAddr;
    readonly string Script;
    private readonly string Retrainer;
    readonly Thread thread;
    public Scaler(Lib.Database.Database database, string deployment, int period, string kubeAddr, string prometheusAddr, string script, string retrainer) {
        Database = database;
        Deployment = deployment;
        Period = period;
        KubeAddr = kubeAddr;
        PrometheusAddr = prometheusAddr;
        Script = script;
        Retrainer = retrainer;
        thread = new(Scale);
        thread.Start();
    }
    public async void Scale() {
        Forecaster forecaster = new(Database, Script, Period, Retrainer);
        Prometheus prometheus = new(PrometheusAddr);
        var settings2 = Database.GetSettings();
        var initData = await prometheus.QueryRange("(sum(rate(container_cpu_usage_seconds_total{container=~\"stregsystemet\"}[5m]))/count(container_cpu_usage_seconds_total{container=~\"stregsystemet\"}))*100", DateTime.Now.AddHours(-12), DateTime.Now, settings2.ScalePeriod.Value);

        await forecaster.RetrainModel(initData);
        await forecaster.Run();
        Kubernetes kubernetes = new(KubeAddr);
        Forecast forecast;
        while(true) {
            var settings = Database.GetSettings();
            //if (settings.ScalePeriod != null)
            //{
                var data = await prometheus.QueryRange("(sum(rate(container_cpu_usage_seconds_total{container=~\"stregsystemet\"}[5m]))/count(container_cpu_usage_seconds_total{container=~\"stregsystemet\"}))*100", DateTime.Now.AddHours(-12), DateTime.Now, settings.ScalePeriod.Value);
                //Database.InsertHistorical(data);
            //}

            var replicas = await kubernetes.Replicas(Deployment);
            Console.WriteLine($"current replicas: {replicas}");
            try{
                forecast = forecaster.Next();
                forecast = forecaster.Next();
            } catch
            {
                Console.WriteLine("No forecast available, making new prdiction");
                Database.RemoveAllForecasts();
                await forecaster.Run();
                continue;
            }

            Forecast newestHistorical = new();
            try
            {
                var hist = data.MaxBy(h => h.Timestamp);
                newestHistorical = new(hist.Timestamp, hist.Value);
                //newestHistorical = Database.GetNewestHistorical();
            } catch
            {
                Console.WriteLine("Prometheus is either down or there is no data which should not happen");
            }

            //Check if the the forecasted value is within 20% of the newest historical value
            if (!(forecast.Value < newestHistorical.Value * 0.8) || !(forecast.Value > newestHistorical.Value * 1.2) || Database.IsManualChange)
            {
                await forecaster.RetrainModel(data);
                await forecaster.Run();
            }
            
            //var replicas = 1;
            if(forecast.Value > settings.ScaleUp)
                replicas++;
            if(forecast.Value <= settings.ScaleDown && replicas > 1)
                replicas--;
            if(replicas < 1)
                replicas = 1;

            Dictionary<string, Dictionary<string, int>> patchData = new() {{
                "spec", new() {{
                    "replicas",replicas
                }}
            }};
            Console.WriteLine($"Forecasted value: {forecast.Value}");
            Console.WriteLine($"new replicas: {replicas}");

            await kubernetes.Patch($"/apis/apps/v1/namespaces/default/deployments/{Deployment}/scale", patchData);

            var delay = (forecast.Timestamp - DateTime.Now).TotalMilliseconds;
            if(forecast.Timestamp > DateTime.Now)
                Thread.Sleep((int)delay);
        }
    }
}
