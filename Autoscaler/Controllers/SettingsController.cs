using Autoscaler.Lib.Database;
using Microsoft.AspNetCore.Mvc;

namespace Autoscaler.Controllers;

[ApiController]
[Route("settings")]
public class SettingsController : ControllerBase
{
    readonly Database Database;

    public SettingsController(Database database)
    {
        Database = database;
    }

    [HttpPost]
    public async Task<IActionResult> Set([FromBody] Settings settings)
    {
        Database.SetSettings(settings);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = Database.GetSettings();
        return Ok(settings);
    }
}