using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IConfiguration _config;
    public ProductsController(IConfiguration config) => _config = config;

    private string Cs => _config.GetConnectionString("Default") ?? "";

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private async Task<decimal> GetUsdRate(MySqlConnection conn)
    {
        await using var cmd = new MySqlCommand("SELECT usd_rate FROM settings WHERE id=1;", conn);
        var val = await cmd.ExecuteScalarAsync();
        if (val is null) throw new InvalidOperationException("No existe settings id=1 (usd_rate).");
        return Convert.ToDecimal(val);
    }

    // ---------- DTOs ----------
    public record CreateProductRequest(
        string Name,
        string? Sku,
        decimal CostUsd,
        decimal PriceUsd,
        int Stock
    );

    public record UpdateStockRequest(int Delta);

    // ---------- Endpoints ----------

    // Alta producto
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
    {
        if (req is null) return BadRequest("Body requerido.");
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name requerido.");
        if (req.CostUsd < 0 || req.PriceUsd < 0) return BadRequest("CostUsd/PriceUsd no pueden ser negativos.");
        if (req.Stock < 0) return BadRequest("Stock no puede ser negativo.");

        await using var conn = new MySqlConnection(Cs);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO products (sku, name, cost_usd, price_usd, stock, is_active)
VALUES (@sku, @name, @cost, @price, @stock, 1);
SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sku", (object?)req.Sku ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@name", req.Name.Trim());
        cmd.Parameters.AddWithValue("@cost", req.CostUsd);
        cmd.Parameters.AddWithValue("@price", req.PriceUsd);
        cmd.Parameters.AddWithValue("@stock", req.Stock);

        var idObj = await cmd.ExecuteScalarAsync();
        var id = Convert.ToInt32(idObj);

        return Created($"/api/products/{id}", new { id });
    }

    // Listado (incluye precio en ARS calculado con la cotización actual)
    [HttpGet]
    public async Task<IActionResult> List()
    {
        await using var conn = new MySqlConnection(Cs);
        await conn.OpenAsync();

        var usdRate = await GetUsdRate(conn);

        const string sql = @"
SELECT id, sku, name, cost_usd, price_usd, stock, is_active, created_at
FROM products
ORDER BY id DESC;";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var rd = await cmd.ExecuteReaderAsync();

        // Ordinals (índices) seguros para MySqlDataReader
        int oId = rd.GetOrdinal("id");
        int oSku = rd.GetOrdinal("sku");
        int oName = rd.GetOrdinal("name");
        int oCostUsd = rd.GetOrdinal("cost_usd");
        int oPriceUsd = rd.GetOrdinal("price_usd");
        int oStock = rd.GetOrdinal("stock");
        int oIsActive = rd.GetOrdinal("is_active");
        int oCreatedAt = rd.GetOrdinal("created_at");

        var items = new List<object>();

        while (await rd.ReadAsync())
        {
            var priceUsd = rd.GetDecimal(oPriceUsd);
            var costUsd = rd.GetDecimal(oCostUsd);

            items.Add(new
            {
                id = rd.GetInt32(oId),
                sku = rd.IsDBNull(oSku) ? null : rd.GetString(oSku),
                name = rd.GetString(oName),
                costUsd,
                priceUsd,
                priceArs = Round2(priceUsd * usdRate),
                costArs = Round2(costUsd * usdRate),
                stock = rd.GetInt32(oStock),
                isActive = rd.GetBoolean(oIsActive),
                createdAt = rd.GetDateTime(oCreatedAt)
            });
        }

        return Ok(new { usdRate, items });
    }

    // Ajuste simple de stock (+/-)
    // Body: { "delta": 10 } o { "delta": -1 }
    [HttpPost("{id:int}/stock")]
    public async Task<IActionResult> AdjustStock(int id, [FromBody] UpdateStockRequest req)
    {
        await using var conn = new MySqlConnection(Cs);
        await conn.OpenAsync();

        const string sql = @"UPDATE products SET stock = stock + @d WHERE id = @id;";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@d", req.Delta);
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return NotFound("Producto no encontrado.");

        return Ok(new { ok = true });
    }
    // Buscar por nombre o SKU
// GET /api/products/search?q=bb&limit=20
[HttpGet("search")]
public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 20)
{
    q = (q ?? "").Trim();
    if (q.Length == 0) return BadRequest("Falta query param ?q=...");
    if (limit < 1) limit = 1;
    if (limit > 100) limit = 100;

    await using var conn = new MySqlConnection(Cs);
    await conn.OpenAsync();

    var usdRate = await GetUsdRate(conn);

    const string sql = @"
SELECT id, sku, name, cost_usd, price_usd, stock, is_active, created_at
FROM products
WHERE is_active = 1
  AND (
        name LIKE CONCAT('%', @q, '%')
        OR (sku IS NOT NULL AND sku LIKE CONCAT('%', @q, '%'))
      )
ORDER BY
  CASE WHEN sku = @q THEN 0 ELSE 1 END,
  CASE WHEN name LIKE CONCAT(@q, '%') THEN 0 ELSE 1 END,
  name
LIMIT @limit;";

    await using var cmd = new MySqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@q", q);
    cmd.Parameters.AddWithValue("@limit", limit);

    await using var rd = await cmd.ExecuteReaderAsync();

    int oId = rd.GetOrdinal("id");
    int oSku = rd.GetOrdinal("sku");
    int oName = rd.GetOrdinal("name");
    int oCostUsd = rd.GetOrdinal("cost_usd");
    int oPriceUsd = rd.GetOrdinal("price_usd");
    int oStock = rd.GetOrdinal("stock");
    int oIsActive = rd.GetOrdinal("is_active");
    int oCreatedAt = rd.GetOrdinal("created_at");

    var items = new List<object>();

    while (await rd.ReadAsync())
    {
        var priceUsd = rd.GetDecimal(oPriceUsd);
        var costUsd = rd.GetDecimal(oCostUsd);

        items.Add(new
        {
            id = rd.GetInt32(oId),
            sku = rd.IsDBNull(oSku) ? null : rd.GetString(oSku),
            name = rd.GetString(oName),
            costUsd,
            priceUsd,
            priceArs = Round2(priceUsd * usdRate),
            costArs = Round2(costUsd * usdRate),
            stock = rd.GetInt32(oStock),
            isActive = rd.GetBoolean(oIsActive),
            createdAt = rd.GetDateTime(oCreatedAt)
        });
    }

    return Ok(new { q, usdRate, items });
}
