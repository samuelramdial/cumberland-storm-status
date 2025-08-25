using System.ComponentModel.DataAnnotations;

public class RequestUpdate
{
    public int Id { get; set; }
    public int DebrisRequestId { get; set; }

    [MaxLength(300)]
    public string Note { get; set; } = "";

    public string CreatedBy { get; set; } = "system";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DebrisRequest? DebrisRequest { get; set; }
}
