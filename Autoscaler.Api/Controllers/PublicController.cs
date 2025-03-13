using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner;
using Microsoft.AspNetCore.Mvc;

namespace Autoscaler.Controllers;

[ApiController]
[Route("services")]
public class PublicController : ControllerBase
{
    private readonly IServicesRepository _servicesRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IForecastRepository _forecastRepository;
    private readonly Runner.Runner _runner;
    private static bool _isMainLoopRunning = false;
    private static readonly object _lockObject = new object();

    public PublicController(
        ISettingsRepository settingsRepository,
        IServicesRepository servicesRepository,
        IForecastRepository forecastRepository,
        Runner.Runner runner)
    {
        _settingsRepository = settingsRepository;
        _servicesRepository = servicesRepository;
        _forecastRepository = forecastRepository;
        _runner = runner;
    }

    [HttpGet("start")]
    public async Task<IActionResult> StartAutoscaler()
    {
        lock (_lockObject)
        {
            if (_isMainLoopRunning)
            {
                return Ok(new {message = "Autoscaler is already running"});
            }

            // Start the MainLoop in a background task
            _ = Task.Run(async () =>
            {
                _isMainLoopRunning = true;
                try
                {
                    await _runner.MainLoop();
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Console.WriteLine($"Error in MainLoop: {ex}");
                }
                finally
                {
                    _isMainLoopRunning = false;
                }
            });

            return Ok(new {message = "Autoscaler started successfully"});
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetServices()
    {
        var services = await _servicesRepository.GetAllServicesAsync();
        return Ok(services);
    }

    [HttpGet("{serviceId}")]
    public async Task<IActionResult> GetServiceById([FromRoute] Guid serviceId)
    {
        var service = await _servicesRepository.GetServiceByIdAsync(serviceId);
        return Ok(service);
    }

    [HttpGet("{serviceId}/settings")]
    public async Task<IActionResult> GetSettingsForServiceById([FromRoute] Guid serviceId)
    {
        var settings = await _settingsRepository.GetSettingsForServiceAsync(serviceId);
        return Ok(settings);
    }

    [HttpPost("{serviceId}/settings")]
    public async Task<IActionResult> UpsertSettingsForServiceById([FromRoute] Guid serviceId,
        [FromBody] SettingsEntity settings)
    {
        settings.ServiceId = serviceId;
        var result = await _settingsRepository.UpsertSettingsAsync(settings);
        return Ok(result);
    }

    [HttpPost("{serviceId}")]
    public async Task<IActionResult> UpsertServiceById([FromRoute] Guid serviceId, [FromBody] ServiceEntity service)
    {
        service.Id = serviceId;
        var result = await _servicesRepository.UpsertServiceAsync(service);
        return Ok(result);
    }

    [HttpGet("{serviceId}/forecast")]
    public async Task<IActionResult> GetForecastForServiceById([FromRoute] Guid serviceId)
    {
        var forecast = await _forecastRepository.GetForecastsByServiceIdAsync(serviceId);
        return Ok(forecast);
    }

    [HttpPost("{serviceId}/forecast")]
    public async Task<IActionResult> UpsertForecastForServiceById([FromRoute] Guid serviceId,
        [FromBody] ForecastEntity forecast)
    {
        forecast.ServiceId = serviceId;
        var result = await _forecastRepository.UpdateForecastAsync(forecast);
        return Ok(result);
    }
}