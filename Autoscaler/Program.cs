using System.Diagnostics;
using System.Text.Json;
using Autoscaler;
using Autoscaler.Lib.Database;
using Autoscaler.Scaler;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

ArgumentParser Args = new(args);
Database database = new(Args.Get("--database"));
Scaler scaler = new(database, Args.Get("--deployment"), int.Parse(Args.Get("--period")), Args.Get("--kube-api"),
    Args.Get("--prometheus-addr"), Args.Get("--scaler"), Args.Get("--re-trainer"));

builder.Services.AddSingleton(database);
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
builder.WebHost.UseUrls("http://0.0.0.0:8080");
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
        options.RoutePrefix = string.Empty; // Makes Swagger UI available at the root ("/")
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseEndpoints(endpoints => { endpoints.MapControllers().RequireCors("AllowSpecificOrigin"); });
app.Lifetime.ApplicationStopping.Register(() => { });
app.Run();