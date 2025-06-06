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
    public required RunnerType Runner { get; set; }
    public required DatabaseType Database { get; set; }
}

public class RunnerType
{
    public required bool Start { get; set; }
}

public class Apis
{
    public required Api Kubernetes { get; set; }
    public required Api Prometheus { get; set; }
    public required Api Forecaster { get; set; }
}

public class Api
{
    public required string Url { get; set; }
    public required bool Mock { get; set; }
}

public class DatabaseType
{
    public required string Hostname { get; set; }
    public required int Port { get; set; }
    public required string Database { get; set; }
    public required string User { get; set; }
    public required string Password { get; set; }
}