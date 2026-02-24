using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IConfiguration _config;

    public ProductsController(IConfiguration config)
    {
        _config = config;
    }

    // ===== Helper interno =====
    private async Task<decimal> GetUsdRate(MySqlConnection conn)
    {
        await using var cmd = new MySqlCommand("SELECT usd_rate FROM settings LIMIT 1;", conn);
        var obj = await cmd.ExecuteScalarAsync();
        return obj == null ? 0 : Convert.ToDecimal(obj);
    }

    private decimal Round2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

    // =========================================
    // GET /api/products
    // =========================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cs = _config.GetConnectionString("Default");
        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        var usdRate = await GetUsdRate(conn);

        const string sql = @"SELECT id, sku, name, cost_usd, price_usd, stock, is_active, created_at
                             FROM products
                             ORDER BY name;";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var rd = await cmd.ExecuteReaderAsync();

        var items = new List<object>();

        while (await rd.ReadAsync())
        {
            var priceUsd = rd.GetDecimal(rd.GetOrdinal("price_usd"));
            var costUsd = rd.GetDecimal(rd.GetOrdinal("cost_usd"));

            items.Add(new
            {
                id = rd.GetInt32(rd.GetOrdinal("id")),
                sku = rd.IsDBNull(rd.GetOrdinal("sku")) ? null : rd.GetString(rd.GetOrdinal("sku")),
                name = rd.GetString(rd.GetOrdinal("name")),
                costUsd,
                priceUsd,
                costArs = Round2(costUsd * usdRate),
                priceArs = Round2(priceUsd * usdRate),
                stock = rd.GetInt32(rd.GetOrdinal("stock")),
                isActive = rd.GetBoolean(rd.GetOrdinal("is_active")),
                createdAt = rd.GetDateTime(rd.GetOrdinal("created_at"))
            });
        }

        return Ok(new { usdRate, items });
    }

    // =========================================
    // POST /api/products
    // =========================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
    {
        var cs = _config.GetConnectionString("Default");
        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO products (sku, name, cost_usd, price_usd, stock, is_active, created_at)
VALUES (@sku, @name, @cost, @price, @stock, 1, NOW());
SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sku", dto.Sku);
        cmd.Parameters.AddWithValue("@name", dto.Name);
        cmd.Parameters.AddWithValue("@cost", dto.CostUsd);
        cmd.Parameters.AddWithValue("@price", dto.PriceUsd);
        cmd.Parameters.AddWithValue("@stock", dto.Stock);

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return Created($"/api/products/{id}", new { id });
    }

    // =========================================
    // GET /api/products/search?q=bb
    // =========================================
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 20)
    {
        q = (q ?? "").Trim();
        if (string.IsNullOrEmpty(q))
            return BadRequest("Falta query param ?q=...");

        if (limit < 1) limit = 1;
        if (limit > 100) limit = 100;

        var cs = _config.GetConnectionString("Default");
        await using var conn = new MySqlConnection(cs);
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
ORDER BY name
LIMIT @limit;";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", q);
        cmd.Parameters.AddWithValue("@limit", limit);

        await using var rd = await cmd.ExecuteReaderAsync();

        var items = new List<object>();

        while (await rd.ReadAsync())
        {
            var priceUsd = rd.GetDecimal(rd.GetOrdinal("price_usd"));
            var costUsd = rd.GetDecimal(rd.GetOrdinal("cost_usd"));

            items.Add(new
            {
                id = rd.GetInt32(rd.GetOrdinal("id")),
                sku = rd.IsDBNull(rd.GetOrdinal("sku")) ? null : rd.GetString(rd.GetOrdinal("sku")),
                name = rd.GetString(rd.GetOrdinal("name")),
                costUsd,
                priceUsd,
                costArs = Round2(costUsd * usdRate),
                priceArs = Round2(priceUsd * usdRate),
                stock = rd.GetInt32(rd.GetOrdinal("stock"))
            });
        }

        return Ok(new { q, usdRate, count = items.Count, items });
    }
}

// =========================================
// DTO
// =========================================
public class ProductCreateDto
{
    public string? Sku { get; set; }
    public string Name { get; set; } = "";
    public decimal CostUsd { get; set; }
    public decimal PriceUsd { get; set; }
    public int Stock { get; set; }
}
