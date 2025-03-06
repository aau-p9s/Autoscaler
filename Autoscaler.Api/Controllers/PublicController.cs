using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Microsoft.AspNetCore.Mvc;

namespace Autoscaler.Controllers;

[ApiController]
[Route("services")]
public class PublicController : ControllerBase
{
    private readonly IServicesRepository _servicesRepository;
    private readonly ISettingsRepository _settingsRepository;

    public PublicController(ISettingsRepository settingsRepository, IServicesRepository servicesRepository)
    {
        _settingsRepository = settingsRepository;
        _servicesRepository = servicesRepository;
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

    [HttpGet ("{serviceId}/settings")]
    public async Task<IActionResult> GetSettingsForServiceById([FromRoute] Guid serviceId)
    {
        var settings = await _settingsRepository.GetSettingsForServiceAsync(serviceId);
        Console.WriteLine(settings);
        return Ok(settings);
    }
    
    [HttpPost("{serviceId}/settings")]
    public async Task<IActionResult> UpsertSettingsForServiceById([FromRoute] Guid serviceId, [FromBody] SettingsEntity settings)
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
}