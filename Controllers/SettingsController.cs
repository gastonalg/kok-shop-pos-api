using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _config;
    public SettingsController(IConfiguration config) => _config = config;

    private string Cs => _config.GetConnectionString("Default") ?? "";

    [HttpGet("usd-rate")]
    public async Task<IActionResult> GetUsdRate()
    {
        await using var conn = new MySqlConnection(Cs);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("SELECT usd_rate FROM settings WHERE id=1;", conn);
        var val = await cmd.ExecuteScalarAsync();

        if (val is null) return NotFound("No existe settings id=1");
        return Ok(new { usdRate = Convert.ToDecimal(val) });
    }

    public record UpdateUsdRateRequest(decimal UsdRate);

    [HttpPut("usd-rate")]
    public async Task<IActionResult> UpdateUsdRate([FromBody] UpdateUsdRateRequest req)
    {
        if (req.UsdRate <= 0) return BadRequest("UsdRate debe ser > 0.");

        await using var conn = new MySqlConnection(Cs);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO settings (id, usd_rate)
VALUES (1, @r)
ON DUPLICATE KEY UPDATE usd_rate=@r;";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@r", req.UsdRate);
        await cmd.ExecuteNonQueryAsync();

        return Ok(new { ok = true, usdRate = req.UsdRate });
    }
}
