using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/setup")]
public class ImportProductsController : ControllerBase
{
    private readonly IConfiguration _config;
    public ImportProductsController(IConfiguration config) => _config = config;

    public record ImportSqlRequest(string Sql);

    [HttpPost("import-products")]
    public async Task<IActionResult> ImportProducts([FromBody] ImportSqlRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Sql))
            return BadRequest("Body inválido. Enviá { \"sql\": \"...\" }");

        var cs = _config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return Problem("Falta ConnectionStrings:Default.");

        // Split simple por ';' para ejecutar muchos INSERT
        var parts = req.Sql
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
