namespace PosApi.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    public record LoginRequest(string user, string pass);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (req.user == "admin" && req.pass == "1234")
        {
            return Ok(new { token = "demo-token" });
        }

        return Unauthorized();
    }
}
