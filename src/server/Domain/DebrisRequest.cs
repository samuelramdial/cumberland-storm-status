using System.ComponentModel.DataAnnotations;

public class DebrisRequest
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string FullName { get; set; } = "";

    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required, MaxLength(160)]
    public string Address { get; set; } = "";

    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int? ZoneId { get; set; }
    public string Status { get; set; } = "NEW"; // NEW | SCHEDULED | COMPLETE
    public int Priority { get; set; } = 0;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Zone? Zone { get; set; }
    public List<RequestUpdate> Updates { get; set; } = new();
}
