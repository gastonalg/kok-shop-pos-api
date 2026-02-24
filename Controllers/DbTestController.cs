using Microsoft.AspNetCore.Mvc;

namespace pos.Controllers;

[ApiController]
[Route("api/dbtest")]
public class DbTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Test()
    {
        return Ok("API OK");
    }
}
