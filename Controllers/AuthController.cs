
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace pos.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (req.User != "admin" || req.Pass != "1234")
            return Unauthorized();

        var secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_secret";
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret));

        var token = new JwtSecurityToken(
            claims: new[] { new Claim(ClaimTypes.Name, req.User) },
            expires: DateTime.UtcNow.AddHours(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }

    public record LoginRequest(string User, string Pass);
}
