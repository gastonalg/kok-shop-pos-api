using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/setup")]
public class MigrateController : ControllerBase
{
    private readonly IConfiguration _config;
    public MigrateController(IConfiguration config) => _config = config;

    [HttpPost("migrate")]
    public async Task<IActionResult> Migrate()
    {
        var cs = _config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return Problem("Falta ConnectionStrings:Default.");

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        // Aumentar tamaño de name para soportar nombres largos
        await using (var cmd = new MySqlCommand(
            "ALTER TABLE products MODIFY name VARCHAR(512) NOT NULL;",
            conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok(new { ok = true, message = "OK: products.name => VARCHAR(512)" });
    }
}
