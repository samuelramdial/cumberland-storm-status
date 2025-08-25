using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public record DebrisRequestCreateDto(
    string FullName,
    string Address,
    string? Email,
    string? Phone,
    int? ZoneId,
    string? Notes,
    double? Lat,
    double? Lng
);

[ApiController]
[Route("api/debris-requests")]
public class DebrisRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    public DebrisRequestsController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DebrisRequestCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Address))
            return ValidationProblem("FullName and Address are required.");

        var req = new DebrisRequest
        {
            FullName = dto.FullName.Trim(),
            Address = dto.Address.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            ZoneId = dto.ZoneId,
            Notes = dto.Notes?.Trim(),
            Lat = dto.Lat,
            Lng = dto.Lng
        };

        _db.DebrisRequests.Add(req);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Timeline), new { id = req.Id }, new { id = req.Id });
    }

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> Timeline(int id)
    {
        var req = await _db.DebrisRequests
            .Include(r => r.Zone)
            .Include(r => r.Updates.OrderByDescending(u => u.CreatedAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req is null) return NotFound();

        return Ok(new
        {
            request = new { req.Id, req.FullName, req.Address, req.Status, Zone = req.Zone?.Name },
            updates = req.Updates.Select(u => new { u.Note, u.CreatedBy, u.CreatedAt })
        });
    }
}
