namespace Autoscaler.Lib.Forecasts;

public class PredictionResult
{
    public List<DateTime> Time {get; set;}
    public List<double> Amount {get; set;}
}