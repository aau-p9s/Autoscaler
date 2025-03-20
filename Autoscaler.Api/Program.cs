using Autoscaler.Persistence.Extensions;
using Autoscaler.Runner;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var autoscalerSettings = builder.Configuration.GetSection("AUTOSCALER");
var port = autoscalerSettings.GetValue<int>("PORT");
var host = autoscalerSettings.GetValue<string>("HOST");
var dbSettings = autoscalerSettings.GetSection("PGSQL");
var dbAddr = dbSettings.GetValue<string>("ADDR");
var dbPort = dbSettings.GetValue<int>("PORT");
var dbName = dbSettings.GetValue<string>("DATABASE");
var dbUser = dbSettings.GetValue<string>("USER");
var dbPassword = dbSettings.GetValue<string>("PASSWORD"); // TODO: FIX
var apis = autoscalerSettings.GetSection("APIS");

Console.WriteLine("Settings set by env vars:");
Console.WriteLine($@"
    AUTOSCALER.PORT:            {port}
    AUTOSCALER.HOST:            {host}
    AUTOSCALER.PGSQL.ADDR:      {dbAddr}
    AUTOSCALER.PGSQL.PORT:      {dbPort}
    AUTOSCALER.PGSQL.DATABASE:  {dbName}
    AUTOSCALER.PGSQL.USER:      {dbUser}
    AUTOSCALER.APIS.FORECASTER: {apis.GetValue<string>("FORECASTER")}
    AUTOSCALER.APIS.KUBERNETES: {apis.GetValue<string>("KUBERNETES")}
    AUTOSCALER.APIS.PROMETHEUS: {apis.GetValue<string>("PROMETHEUS")}
");

builder.Services.ConfigurePersistencePostGreSqlConnection(
    $"Server={dbAddr};Port={dbPort};Database={dbName};Uid={dbUser};Password={dbPassword}");
builder.Services.AddSingleton<Runner>(provider =>
    new Runner(
        apis.GetValue<string>("FORECASTER") ?? "http://forecaster",
        apis.GetValue<string>("KUBERNETES") ?? "http://kubernetes",
        apis.GetValue<string>("PROMETHEUS") ?? "http://prometheus",
        provider,
        true,
        false
    )
);

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
app.MapControllers();
app.Lifetime.ApplicationStopping.Register(() => { });
app.Run();