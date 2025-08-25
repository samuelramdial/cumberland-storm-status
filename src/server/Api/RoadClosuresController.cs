using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/closures")]
public class RoadClosuresController : ControllerBase
{
    private readonly AppDbContext _db;
    public RoadClosuresController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = _db.RoadClosures.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status.ToUpperInvariant());

        var data = await q.OrderByDescending(r => r.UpdatedAt).ToListAsync();
        return Ok(data);
    }
}
