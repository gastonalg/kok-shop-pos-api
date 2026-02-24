using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace pos.Controllers;

[ApiController]
[Route("api/dbtest")]
public class DbTestController : ControllerBase
{
    private readonly IConfiguration _config;

    public DbTestController(IConfiguration config) => _config = config;

    [HttpGet]
    public async Task<IActionResult> Test()
    {
        var cs = _config.GetConnectionString("Default");
        return Ok(cs);
    }
}
