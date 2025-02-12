using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Runner.Kubernetes;

class Forecaster : IAPI {
    private readonly string Addr;
    // add forecaster REST API bindings

    public Forecaster(string addr) {
        Addr = addr;
        if(!IsUp()) {
            Console.WriteLine("Forecaster shouldn't be down");
            Environment.Exit(1);
        }
    }
    public ForecastEntity Forecast(IEnumerable<HistoricEntity> data) {
        return new();
    }

    public bool IsUp() {
        return false;
    }
}
