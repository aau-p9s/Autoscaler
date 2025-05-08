namespace Autoscaler.Config;

public class AppSettings
{
    public required Logging Logging { get; set; }
    public required string AllowedHosts { get; set; }
    public required Autoscaler Autoscaler { get; set; }
}

public class Logging
{
    public required LogLevel LogLevel { get; set; }
}

public class LogLevel
{
    public required string Default { get; set; }
    public required string Microsoft { get; set; }
    public required string MicrosoftHostingLifetime { get; set; }
    public required string Autoscaler { get; set; }
}

public class Autoscaler
{
    public required int Port { get; set; }
    public required string Host { get; set; }
    public required Apis Apis { get; set; }
    public required bool UseForecasterInDevelopmentMode { get; set; }
    public required bool DevelopmentMode { get; set; }
    public required Pgsql Pgsql { get; set; }
}

public class Apis
{
    public required string Kubernetes { get; set; }
    public required string Prometheus { get; set; }
    public required string Forecaster { get; set; }
}

public class Pgsql {
    public required string Addr { get; set; }
    public required int Port { get; set; }
    public required string Database { get; set; }
    public required string User { get; set; }
    public required string Password { get; set; }
}