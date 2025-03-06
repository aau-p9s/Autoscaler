using Microsoft.AspNetCore.Mvc;

namespace Autoscaler.Controllers;

[ApiController]
[Route("/forecast")]
public class ForecastController : ControllerBase
{
    public ForecastController()
    {
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new Dictionary<DateTime, double>(
            //Database.Prediction(DateTime.Now.AddDays(7))
        ));
    }

    [HttpPost]
    public IActionResult ManualChange([FromBody] Dictionary<DateTime, double> data)
    {
        return Ok(new Dictionary<DateTime, double>(
        ));
    }
}