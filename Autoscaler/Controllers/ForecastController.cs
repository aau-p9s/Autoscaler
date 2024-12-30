using Autoscaler.Lib.Database;
using Microsoft.AspNetCore.Mvc;

namespace Autoscaler.Controllers;

[ApiController]
[Route("/forecast")]
public class ForecastController : ControllerBase
{
    private readonly ILogger<ForecastController> _logger;
    readonly Database Database;

    public ForecastController(Database database)
    {
        Database = database;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(new Dictionary<DateTime, double>(
            Database.Prediction(DateTime.Now.AddDays(7))
        ));
    }

    [HttpPost]
    public async Task<IActionResult> ManualChange([FromBody] Dictionary<DateTime, double> data)
    {
        Database.ManualChange(data);
        return Ok(new Dictionary<DateTime, double>(
            Database.Prediction(DateTime.Now.AddDays(-7))
        ));
    }
}