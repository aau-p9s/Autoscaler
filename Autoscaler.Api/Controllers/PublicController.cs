using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Microsoft.AspNetCore.Mvc;

namespace Autoscaler.Controllers;

[ApiController]
[Route("services")]
public class PublicController(
    IServicesRepository servicesRepository,
    ISettingsRepository settingsRepository,
    IForecastRepository forecastRepository,
    Runner.Runner runner,
    ILogger logger) : ControllerBase
{
    private IServicesRepository ServicesRepository => servicesRepository;
    private ISettingsRepository SettingsRepository => settingsRepository;
    private IForecastRepository ForecastRepository => forecastRepository;
    private Runner.Runner Runner => runner;
    private static bool _isMainLoopRunning;
    private ILogger Logger => logger;
    private static readonly object LockObject = new();

    [HttpGet("start")]
    public IActionResult StartAutoscaler()
    {
        lock (LockObject)
        {
            if (_isMainLoopRunning)
            {
                return Ok(new { message = "Autoscaler is already running" });
            }

            // Start the MainLoop in a background task
            _ = Task.Run(async () =>
            {
                lock (LockObject)
                {
                    _isMainLoopRunning = true;
                }

                try
                {
                    await Runner.MainLoop();
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Logger.LogError($"Error in MainLoop: {ex}");
                }
                finally
                {
                    logger.LogInformation("Finished starting autoscaler");
                    _isMainLoopRunning = false;
                }
            });

            return Ok(new { message = "Autoscaler started successfully" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetServices()
    {
        var services = await ServicesRepository.GetAllServicesAsync();
        return Ok(services);
    }

    [HttpGet("{serviceId}")]
    public async Task<IActionResult> GetServiceById([FromRoute] Guid serviceId)
    {
        var service = await ServicesRepository.GetServiceByIdAsync(serviceId);
        return Ok(service);
    }

    [HttpGet("{serviceId}/settings")]
    public async Task<IActionResult> GetSettingsForServiceById([FromRoute] Guid serviceId)
    {
        var settings = await SettingsRepository.GetSettingsForServiceAsync(serviceId);
        return Ok(settings);
    }

    [HttpPost("{serviceId}/settings")]
    public async Task<IActionResult> UpsertSettingsForServiceById([FromRoute] Guid serviceId,
        [FromBody] SettingsEntity settings)
    {
        settings.ServiceId = serviceId;
        var result = await SettingsRepository.UpsertSettingsAsync(settings);
        return Ok(result);
    }

    [HttpPost("{serviceId}")]
    public async Task<IActionResult> UpsertServiceById([FromRoute] Guid serviceId, [FromBody] ServiceEntity service)
    {
        service.Id = serviceId;
        var result = await ServicesRepository.UpsertServiceAsync(service);
        return Ok(result);
    }

    [HttpGet("{serviceId}/forecast")]
    public async Task<IActionResult> GetForecastForServiceById([FromRoute] Guid serviceId)
    {
        var forecast = await ForecastRepository.GetForecastsByServiceIdAsync(serviceId);
        return Ok(forecast);
    }

    [HttpPost("{serviceId}/forecast")]
    public async Task<IActionResult> UpsertForecastForServiceById([FromRoute] Guid serviceId,
        [FromBody] ForecastEntity forecast)
    {
        forecast.ServiceId = serviceId;
        var result = await ForecastRepository.UpdateForecastAsync(forecast);
        return Ok(result);
    }
}