using Autoscaler.Config;
using Autoscaler.Persistence.Extensions;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ScaleSettingsRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner;
using Autoscaler.Runner.Services;
using Microsoft.OpenApi.Models;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();


var appSettings = builder.Configuration.Get<AppSettings>();


Console.WriteLine("Settings set by env vars:");
Console.WriteLine($@"
    AUTOSCALER.PORT:            {appSettings.Autoscaler.Port}
    AUTOSCALER.HOST:            {appSettings.Autoscaler.Host}
    AUTOSCALER.PGSQL.ADDR:      {appSettings.Autoscaler.Pgsql.Addr}
    AUTOSCALER.PGSQL.PORT:      {appSettings.Autoscaler.Pgsql.Port}
    AUTOSCALER.PGSQL.DATABASE:  {appSettings.Autoscaler.Pgsql.Database}
    AUTOSCALER.PGSQL.USER:      {appSettings.Autoscaler.Pgsql.User}
    AUTOSCALER.APIS.FORECASTER: {appSettings.Autoscaler.Apis.Forecaster}
    AUTOSCALER.APIS.KUBERNETES: {appSettings.Autoscaler.Apis.Kubernetes}
    AUTOSCALER.APIS.PROMETHEUS: {appSettings.Autoscaler.Apis.Prometheus}
");

// Configure Postgres
builder.Services.ConfigurePersistencePostGreSqlConnection(
    $"Server={appSettings.Autoscaler.Pgsql.Addr};Port={appSettings.Autoscaler.Pgsql.Port};Database={appSettings.Autoscaler.Pgsql.Database};Uid={appSettings.Autoscaler.Pgsql.User};Password={appSettings.Autoscaler.Pgsql.Password}");

// Configure Project Services
builder.Services.AddSingleton(appSettings);
Enum.TryParse(appSettings.Logging.LogLevel.Autoscaler, out LogLevel logLevel);
var factory = LoggerFactory.Create(builder1 => builder1.SetMinimumLevel(logLevel).AddConsole());
var logger = factory.CreateLogger("Autoscaler");
builder.Services.AddSingleton(logger);

builder.Services.AddSingleton<KubernetesService>();
builder.Services.AddSingleton<PrometheusService>();
builder.Services.AddSingleton<ForecasterService>();

builder.Services.AddScoped<IServicesRepository, ServicesRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IForecastRepository, ForecastRepository>();
builder.Services.AddScoped<IHistoricRepository, HistoricRepository>();

builder.Services.AddScoped<Runner>();

// Add services to the container.
builder.Services.AddControllers();
// Add Swagger services
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "AutoScaler API",
        Description = "API documentation for AutoScaler Frontend",
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", builder =>
    {
        builder.WithOrigins("http://localhost:44411", "https://localhost:44411")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.WebHost.UseUrls($"{appSettings.Autoscaler.Host}:{appSettings.Autoscaler.Port}");

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    // Enable Swagger in development environment
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AutoScaler API v1");
        options.RoutePrefix = "autoscaler/swagger";
    });
}

app.MapFallbackToFile("index.html");


app.UseStaticFiles();
app.UseRouting();
app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
app.MapControllers();
app.Lifetime.ApplicationStopping.Register(() => { });
app.Run();