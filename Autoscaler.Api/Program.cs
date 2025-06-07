using Autoscaler.Config;
using Autoscaler.Persistence.BaselineModelRepository;
using Autoscaler.Persistence.Extensions;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ModelRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner;
using Autoscaler.Runner.Services;
using Microsoft.OpenApi.Models;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();


var appSettings = builder.Configuration.Get<AppSettings>() ??
                  throw new ArgumentNullException(nameof(builder), "What? appsettings is null??");


Console.WriteLine("Settings set by env vars:");
Console.WriteLine($@"
    Port:               {appSettings.Autoscaler.Port}
    Host:               {appSettings.Autoscaler.Host}
    Database hostname:  {appSettings.Autoscaler.Database.Hostname}
    Database port:      {appSettings.Autoscaler.Database.Port}
    Database name:      {appSettings.Autoscaler.Database.Database}
    Database user:      {appSettings.Autoscaler.Database.User}
    Forecaster url:     {appSettings.Autoscaler.Apis.Forecaster.Url}
    Kubernetes url:     {appSettings.Autoscaler.Apis.Kubernetes.Url}
    Prometheus url:     {appSettings.Autoscaler.Apis.Prometheus.Url}
    Forecaster mock:    {appSettings.Autoscaler.Apis.Forecaster.Mock}
    Kubernetes mock:    {appSettings.Autoscaler.Apis.Kubernetes.Mock}
    Prometheus mock:    {appSettings.Autoscaler.Apis.Prometheus.Mock}
");

// Configure Postgres
var databaseConnectionString = $"Server={
    appSettings.Autoscaler.Database.Hostname
};Port={
    appSettings.Autoscaler.Database.Port
};Database={
    appSettings.Autoscaler.Database.Database
};Uid={
    appSettings.Autoscaler.Database.User
};Password={
    appSettings.Autoscaler.Database.Password
}";
builder.Services.ConfigurePersistencePostGreSqlConnection(databaseConnectionString);
        
// Configure Logger
Enum.TryParse(appSettings.Logging.LogLevel.Autoscaler, out LogLevel logLevel);
var factory = LoggerFactory.Create(builder1 => builder1.SetMinimumLevel(logLevel).AddConsole());
var logger = factory.CreateLogger("Autoscaler");

// Configure Project Services
builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton(logger);

var mapping = new List<Tuple<Type, Type, bool>>()
{
    new (typeof(MockKubernetesService), typeof(KubernetesService), appSettings.Autoscaler.Apis.Kubernetes.Mock),
    new (typeof(MockPrometheusService), typeof(PrometheusService), appSettings.Autoscaler.Apis.Prometheus.Mock),
    new (typeof(MockForecasterService), typeof(ForecasterService), appSettings.Autoscaler.Apis.Forecaster.Mock)
};
foreach (var (mockType, baseType, isMock) in mapping)
{
    if (isMock)
    {
        builder.Services.AddScoped(baseType, mockType);
    }
    else
    {
        builder.Services.AddSingleton(baseType);
    }
}

builder.Services.AddSingleton<IServicesRepository, ServicesRepository>();
builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
builder.Services.AddSingleton<IForecastRepository, ForecastRepository>();
builder.Services.AddSingleton<IHistoricRepository, HistoricRepository>();
builder.Services.AddSingleton<IModelRepository, ModelRepository>();
builder.Services.AddSingleton<IBaselineModelRepository, BaselineModelRepository>();
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

using (var scope = app.Services.CreateScope())
{
    var repo = scope.ServiceProvider.GetRequiredService<IBaselineModelRepository>();
    await repo.InsertAllBaselineModels("./BaselineModels");
}

// Start runner
if (appSettings.Autoscaler.Runner.Start)
{
    var runner = app.Services.CreateScope().ServiceProvider.GetService<Runner>() ?? throw new NullReferenceException();
    await runner.MainLoop();
}

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