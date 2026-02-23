using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace pos.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    public record LoginRequest(string User, string Pass);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        // usuario fake (demo)
        if (req.User != "admin" || req.Pass != "1234")
            return Unauthorized();

        var secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_secret";
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret));

        var token = new JwtSecurityToken(
            claims: new[] { new Claim(ClaimTypes.Name, req.User) },
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new { token = jwt });
    }
}
