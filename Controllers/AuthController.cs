using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace pos.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration config, ILogger<AuthController> logger)
        {
            _config = config;
            _logger = logger;
        }

        public record LoginRequest(string user, string pass);

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            if (req is null)
                return BadRequest("Body requerido.");

            if (string.IsNullOrWhiteSpace(req.user) || string.IsNullOrWhiteSpace(req.pass))
                return BadRequest("user/pass requeridos.");

            // ✅ Credenciales demo (cambiá esto cuando quieras)
            // (lo dejo igual a lo que venías probando: admin / 1234)
            if (!(req.user.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase) && req.pass == "1234"))
                return Unauthorized("Credenciales inválidas.");

            // ✅ Leer secret desde Railway (Variables)
            // - Railway inyecta env vars, .NET las expone por IConfiguration
            var secret = _config["JWT_SECRET"];

            if (string.IsNullOrWhiteSpace(secret))
            {
                // Esto evita el 500 misterioso
                _logger.LogError("Falta JWT_SECRET. Configurá la variable de entorno JWT_SECRET en Railway.");
                return StatusCode(500, "Falta JWT_SECRET en variables de entorno (Railway).");
            }

            // Opcionales (si no los seteás, usa defaults)
            var issuer = _config["JWT_ISSUER"] ?? "kokshop-pos";
            var audience = _config["JWT_AUDIENCE"] ?? "kokshop-pos-clients";
            var minutesStr = _config["JWT_EXPIRES_MINUTES"];
            var expiresMinutes = int.TryParse(minutesStr, out var m) ? m : 720; // 12 horas

            var token = CreateJwtToken(
                secret: secret,
                issuer: issuer,
                audience: audience,
                expiresMinutes: expiresMinutes,
                username: req.user.Trim()
            );

            return Ok(new
            {
                token,
                tokenType = "Bearer",
                expiresMinutes
            });
        }

        private static string CreateJwtToken(string secret, string issuer, string audience, int expiresMinutes, string username)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim("role", "admin")
            };

            var jwt = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow.AddSeconds(-5),
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
    }
}
