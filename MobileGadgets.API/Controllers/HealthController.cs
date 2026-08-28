using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGadgets.Infrastructure.Persistence;

namespace MobileGadgets.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly MobileGadgetsDbContext _db;

    public HealthController(MobileGadgetsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });

    [HttpGet("db")]
    public async Task<IActionResult> GetDb()
    {
        var canConnect = await _db.Database.CanConnectAsync();
        return Ok(new { status = canConnect ? "ok" : "unreachable", database = _db.Database.GetDbConnection().Database });
    }
}
