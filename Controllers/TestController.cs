using Microsoft.AspNetCore.Mvc;

namespace pos.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { ok = true, msg = "API funcionando" });
    }
}
