using Microsoft.AspNetCore.Mvc;

namespace Autoscaler.Controllers;

[ApiController]
[Route("settings")]
public class SettingsController : ControllerBase
{
    public SettingsController()
    {
    }

    [HttpPost]
    public async Task<IActionResult> Set()
    {
        //Database.SetSettings(settings);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        //var settings = Database.GetSettings();
        //var settings = new Settings(1, 50, 20, 5000);
        return Ok();
    }
}