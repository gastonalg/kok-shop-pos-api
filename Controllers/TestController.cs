
using Microsoft.AspNetCore.Mvc;

namespace POS.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { ok=true, msg="API funcionando" });
}
