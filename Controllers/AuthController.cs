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
    public record LoginRequest(string user, string pass);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (req.user != "admin" || req.pass != "1234")
            return Unauthorized();

        var secret = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(500, "JWT_SECRET no configurado en variables de entorno.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, req.user),
            new Claim(ClaimTypes.Role, "admin")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { token = jwt });
    }
}
