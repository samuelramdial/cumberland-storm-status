
public class RoadClosure
{
    public int Id { get; set; }
    public string RoadName { get; set; } = "";
    public string Status { get; set; } = "OPEN"; // OPEN | PARTIAL | CLOSED
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Optional map coords (nullable so list-only still works)
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}
