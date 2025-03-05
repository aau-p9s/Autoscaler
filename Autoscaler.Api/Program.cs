using Microsoft.OpenApi.Models;
using Autoscaler.Persistence.Extensions;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var port = builder.Configuration.GetValue<int>("AUTOSCALER_PORT");
var host = builder.Configuration.GetValue<string>("AUTOSCALER_HOST");
var dbAddr = builder.Configuration.GetValue<string>("AUTOSCALER_PGSQL_ADDR");
var dbPort = builder.Configuration.GetValue<int>("AUTOSCALER_PGSQL_PORT");
var dbName = builder.Configuration.GetValue<string>("AUTOSCALER_PGSQL_DATABASE");
var dbUser = builder.Configuration.GetValue<string>("AUTOSCALER_PGSQL_USER");
var dbPassword = builder.Configuration.GetValue<string>("AUTOSCALER_PGSQL_PASSWORD"); // TODO: fix

builder.Services.ConfigurePersistencePostGreSqlConnection($"Server={dbAddr};Port={dbPort};Database={dbName};Uid={dbUser};Password={dbPassword}");
builder.Services.AddSingleton<Runner>(provider => 
    new Runner(
        "something", // Deployment name
        builder.Configuration.GetValue<string>("AUTOSCALER_FORECASTER_ADDR"), 
        builder.Configuration.GetValue<string>("AUTOSCALER_KUBERNETES_ADDR"), 
        builder.Configuration.GetValue<string>("AUTOSCALER_PROMETHEUS_ADDR"),
        provider.GetRequiredService<ISettingsRepository>()
    )
);//Get connectionstring from appsettings.json
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
builder.WebHost.UseUrls($"{host}:{port}");
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
app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
app.Lifetime.ApplicationStopping.Register(() => { });
app.Run();