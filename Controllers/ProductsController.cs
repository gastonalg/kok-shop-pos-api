// GET /api/products/search?q=bb&limit=20
[HttpGet("search")]
public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 20)
{
    // ===== Validaciones =====
    q = (q ?? "").Trim();
    if (string.IsNullOrEmpty(q))
        return BadRequest("Falta query param ?q=...");

    if (limit < 1) limit = 1;
    if (limit > 100) limit = 100;

    // ===== Connection string =====
    var cs = _config.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(cs))
        return Problem("Falta ConnectionStrings:Default.");

    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync();

    // ===== Obtener cotización USD =====
    decimal usdRate = 0;
    await using (var cmdRate = new MySqlCommand("SELECT usd_rate FROM settings LIMIT 1;", conn))
    {
        var obj = await cmdRate.ExecuteScalarAsync();
        usdRate = obj == null ? 0 : Convert.ToDecimal(obj);
    }

    // ===== Query productos =====
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

    // ===== Mapper =====
    decimal Round2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

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

    return Ok(new
    {
        q,
        usdRate,
        count = items.Count,
        items
    });
}
