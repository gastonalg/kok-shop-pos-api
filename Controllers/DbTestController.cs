using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/db")]
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
        var host = _config["DB_HOST"];
        var port = _config["DB_PORT"];
        var user = _config["DB_USER"];
        var pass = _config["DB_PASS"];
        var db   = _config["DB_NAME"];

        var cs = $"Server={host};Port={port};User ID={user};Password={pass};Database={db};SslMode=Required;";

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("SELECT 1;", conn);
        var result = await cmd.ExecuteScalarAsync();

        return Ok(new { ok = true, result, host, port, db, user });
    }
}
