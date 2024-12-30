namespace Autoscaler.Lib.Forecasts;

public class Historical
{
    public readonly DateTime Timestamp;
    public readonly double Value;

    public Historical()
    {
        
    }
    public Historical(DateTime timestamp, double value)
    {
        Timestamp = timestamp;
        Value = value;
    }
}