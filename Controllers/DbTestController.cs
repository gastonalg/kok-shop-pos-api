using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/dbtest")]
public class DbTestController : ControllerBase
{
    private readonly IConfiguration _config;

    public DbTestController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping()
    {
        var cs = _config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return Problem("Falta ConnectionStrings:Default en la configuración.");

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("SELECT 1;", conn);
        var result = await cmd.ExecuteScalarAsync();

        return Ok(new { ok = true, result });
    }
}
