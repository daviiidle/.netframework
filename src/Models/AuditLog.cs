namespace Models;

public class AuditLog
{
    public int Id { get; set; }
    public Guid MessageId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? DurationMs { get; set; }
    public string Status { get; set; }
    public string? ErrorMessage { get; set; }

    public AuditLog()
    {
        MessageId = Guid.Empty;
        StartTime = DateTime.MinValue;
        Status = string.Empty;
    }

    public AuditLog(Guid messageId)
    {
        MessageId = messageId;
        StartTime = DateTime.UtcNow;
        Status = "Processing";
    }
}
