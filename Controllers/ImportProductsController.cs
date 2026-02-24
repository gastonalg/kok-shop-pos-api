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
            return BadRequest("Enviá { \"statements\": [\"INSERT ...;\", \"INSERT ...;\"], \"truncateFirst\": true|false }");

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
                await using var truncate = new MySqlCommand("TRUNCATE TABLE products;", conn, tx);
                await truncate.ExecuteNonQueryAsync();
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
            return Ok(new { ok = true, statements = executed, truncated = req.TruncateFirst });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Problem("Falló importación: " + ex.Message);
        }
    }
}
