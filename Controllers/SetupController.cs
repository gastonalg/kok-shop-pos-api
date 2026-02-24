using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly IConfiguration _config;
    public SetupController(IConfiguration config) => _config = config;

    [HttpPost("init")]
    public async Task<IActionResult> Init()
    {
        var cs = _config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return Problem("Falta ConnectionStrings:Default.");

        const string sql = @"
CREATE TABLE IF NOT EXISTS settings (
  id INT PRIMARY KEY,
  usd_rate DECIMAL(10,2) NOT NULL,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

INSERT INTO settings (id, usd_rate)
VALUES (1, 1200.00)
ON DUPLICATE KEY UPDATE usd_rate = usd_rate;

CREATE TABLE IF NOT EXISTS products (
  id INT AUTO_INCREMENT PRIMARY KEY,
  sku VARCHAR(64) NULL,
  name VARCHAR(150) NOT NULL,
  cost_usd DECIMAL(10,2) NOT NULL DEFAULT 0,
  price_usd DECIMAL(10,2) NOT NULL DEFAULT 0,
  stock INT NOT NULL DEFAULT 0,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_products_name ON products(name);

CREATE TABLE IF NOT EXISTS sales (
  id INT AUTO_INCREMENT PRIMARY KEY,
  sold_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  seller VARCHAR(80) NULL,
  usd_rate DECIMAL(10,2) NOT NULL,
  total_ars DECIMAL(12,2) NOT NULL DEFAULT 0,
  total_usd DECIMAL(12,2) NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS sale_items (
  id INT AUTO_INCREMENT PRIMARY KEY,
  sale_id INT NOT NULL,
  product_id INT NOT NULL,
  qty INT NOT NULL,
  unit_price_usd DECIMAL(10,2) NOT NULL,
  unit_price_ars DECIMAL(10,2) NOT NULL,
  unit_cost_usd DECIMAL(10,2) NOT NULL,
  FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE,
  FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE INDEX idx_sale_items_sale_id ON sale_items(sale_id);
";

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();

        return Ok(new { ok = true, message = "Tablas creadas/actualizadas. settings.usd_rate inicial=1200." });
    }
}
