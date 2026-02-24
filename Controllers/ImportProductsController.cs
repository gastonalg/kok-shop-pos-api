using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/setup")]
public class ImportProductsController : ControllerBase
{
    private readonly IConfiguration _config;
    public ImportProductsController(IConfiguration config) => _config = config;

    public record ImportStatementsRequest(List<string> Statements, bool TruncateFirst = false);

    [HttpPost("import-products")]
    public async Task<IActionResult> ImportProducts([FromBody] ImportStatementsRequest req)
    {
        if (req?.Statements is null || req.Statements.Count == 0)
            return BadRequest("Enviá { \"statements\": [\"INSERT ...;\"], \"truncateFirst\": true|false }");

        var cs = _config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return Problem("Falta ConnectionStrings:Default.");

        int executed = 0;

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            if (req.TruncateFirst)
            {
                // ✅ limpiar respetando FK (orden: hijos -> padres)
                await using (var cmd1 = new MySqlCommand("DELETE FROM sale_items;", conn, tx))
                    await cmd1.ExecuteNonQueryAsync();

                await using (var cmd2 = new MySqlCommand("DELETE FROM sales;", conn, tx))
                    await cmd2.ExecuteNonQueryAsync();

                await using (var cmd3 = new MySqlCommand("DELETE FROM products;", conn, tx))
                    await cmd3.ExecuteNonQueryAsync();
            }

            foreach (var raw in req.Statements)
            {
                var stmt = (raw ?? "").Trim();
                if (stmt.Length == 0) continue;
                if (!stmt.EndsWith(";")) stmt += ";";

                await using var cmd = new MySqlCommand(stmt, conn, tx);
                await cmd.ExecuteNonQueryAsync();
                executed++;
            }

            await tx.CommitAsync();
            return Ok(new { ok = true, statements = executed, cleaned = req.TruncateFirst });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Problem("Falló importación: " + ex.Message);
        }
    }
}
