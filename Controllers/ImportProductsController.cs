using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/setup")]
public class ImportProductsController : ControllerBase
{
    private readonly IConfiguration _config;
    public ImportProductsController(IConfiguration config) => _config = config;

    // ✅ Endpoint recomendado: pegás el SQL directo (sin JSON)
    [HttpPost("import-products")]
    [Consumes("text/plain")]
    public async Task<IActionResult> ImportProductsPlain([FromBody] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return BadRequest("Pegá el SQL en el body como text/plain.");

        var cs = _config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return Problem("Falta ConnectionStrings:Default.");

        var parts = sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        int executed = 0;

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            foreach (var p in parts)
            {
                var stmt = p.Trim();
                if (stmt.Length == 0) continue;

                await using var cmd = new MySqlCommand(stmt + ";", conn, tx);
                await cmd.ExecuteNonQueryAsync();
                executed++;
            }

            await tx.CommitAsync();
            return Ok(new { ok = true, statements = executed });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Problem("Falló importación: " + ex.Message);
        }
    }
}
