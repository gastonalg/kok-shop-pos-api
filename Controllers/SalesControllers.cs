using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly IConfiguration _config;

    public SalesController(IConfiguration config)
    {
        _config = config;
    }

    private async Task<decimal> GetUsdRate(MySqlConnection conn)
    {
        await using var cmd = new MySqlCommand("SELECT usd_rate FROM settings LIMIT 1;", conn);
        var obj = await cmd.ExecuteScalarAsync();
        return obj == null ? 0 : Convert.ToDecimal(obj);
    }

    private decimal Round2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

    // =========================================
    // POST /api/sales
    // Body:
    // {
    //   "seller":"Facu",
    //   "items":[{"productId":1,"qty":2},{"productId":5,"qty":1}]
    // }
    // =========================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleRequest req)
    {
        if (req == null || req.Items == null || req.Items.Count == 0)
            return BadRequest("Body inválido. Debe venir items[]");

        foreach (var it in req.Items)
        {
            if (it.ProductId <= 0) return BadRequest("productId inválido.");
            if (it.Qty <= 0) return BadRequest("qty inválida.");
        }

        var cs = _config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return Problem("Falta ConnectionStrings:Default.");

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var usdRate = await GetUsdRate(conn);

            // 1) Validar stock + leer precios/costos
            // Armamos una lista consolidada por producto
            var grouped = req.Items
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Qty) })
                .ToList();

            var productRows = new List<ProductRow>();

            foreach (var g in grouped)
            {
                const string sqlProd = @"
SELECT id, name, cost_usd, price_usd, stock, is_active
FROM products
WHERE id=@id
LIMIT 1;";

                await using var cmd = new MySqlCommand(sqlProd, conn, (MySqlTransaction)tx);
                cmd.Parameters.AddWithValue("@id", g.ProductId);

                await using var rd = await cmd.ExecuteReaderAsync();

                if (!await rd.ReadAsync())
                    return NotFound($"No existe productId={g.ProductId}");

                var isActive = rd.GetBoolean(rd.GetOrdinal("is_active"));
                if (!isActive)
                    return BadRequest($"Producto inactivo: {rd.GetString(rd.GetOrdinal("name"))}");

                var stock = rd.GetInt32(rd.GetOrdinal("stock"));
                if (stock < g.Qty)
                    return BadRequest($"Stock insuficiente para {rd.GetString(rd.GetOrdinal("name"))}. Stock={stock}, pedido={g.Qty}");

                productRows.Add(new ProductRow
                {
                    Id = rd.GetInt32(rd.GetOrdinal("id")),
                    Name = rd.GetString(rd.GetOrdinal("name")),
                    CostUsd = rd.GetDecimal(rd.GetOrdinal("cost_usd")),
                    PriceUsd = rd.GetDecimal(rd.GetOrdinal("price_usd")),
                    Stock = stock,
                    Qty = g.Qty
                });
            }

            // 2) Calcular totales
            decimal totalUsd = 0;
            decimal profitUsd = 0;

            foreach (var p in productRows)
            {
                totalUsd += p.PriceUsd * p.Qty;
                profitUsd += (p.PriceUsd - p.CostUsd) * p.Qty;
            }

            var totalArs = totalUsd * usdRate;
            var profitArs = profitUsd * usdRate;

            // 3) Insert sale
            const string sqlSale = @"
INSERT INTO sales (seller, total_usd, total_ars, profit_usd, profit_ars, usd_rate, created_at)
VALUES (@seller, @totalUsd, @totalArs, @profitUsd, @profitArs, @usdRate, NOW());
SELECT LAST_INSERT_ID();";

            await using var cmdSale = new MySqlCommand(sqlSale, conn, (MySqlTransaction)tx);
            cmdSale.Parameters.AddWithValue("@seller", req.Seller);
            cmdSale.Parameters.AddWithValue("@totalUsd", Round2(totalUsd));
            cmdSale.Parameters.AddWithValue("@totalArs", Round2(totalArs));
            cmdSale.Parameters.AddWithValue("@profitUsd", Round2(profitUsd));
            cmdSale.Parameters.AddWithValue("@profitArs", Round2(profitArs));
            cmdSale.Parameters.AddWithValue("@usdRate", Round2(usdRate));

            var saleId = Convert.ToInt32(await cmdSale.ExecuteScalarAsync());

            // 4) Insert items + update stock
            foreach (var p in productRows)
            {
                const string sqlItem = @"
INSERT INTO sale_items (sale_id, product_id, qty, unit_price_usd, unit_cost_usd)
VALUES (@saleId, @pid, @qty, @price, @cost);";

                await using (var cmdItem = new MySqlCommand(sqlItem, conn, (MySqlTransaction)tx))
                {
                    cmdItem.Parameters.AddWithValue("@saleId", saleId);
                    cmdItem.Parameters.AddWithValue("@pid", p.Id);
                    cmdItem.Parameters.AddWithValue("@qty", p.Qty);
                    cmdItem.Parameters.AddWithValue("@price", Round2(p.PriceUsd
