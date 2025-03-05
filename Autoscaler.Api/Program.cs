using Microsoft.OpenApi.Models;
using Autoscaler.Persistence.Extensions;
using Autoscaler.Persistence.ScaleSettingsRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner;


var builder = WebApplication.CreateBuilder(args);

ArgumentParser Args = new(args);
builder.Services.ConfigurePersistenceMySqlConnection(builder.Configuration.GetConnectionString("MySQLDatabase"));
builder.Services.AddSingleton<Runner>(provider => 
    new Runner(
        "something", // Deployment name
        "http://forecaster", 
        "http://kubernetes", 
        "http://prometheus",
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