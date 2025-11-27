namespace Models;

public class Message
{
    public Guid MessageId { get; set; }
    public DateTime Timestamp { get; set; }
    public string SourceSystem { get; set; }
    public string Payload { get; set; }
    public MessageStatus Status { get; set; }

    // Parameterless constructor for JSON serialization
    public Message()
    {
        MessageId = Guid.Empty;
        Timestamp = DateTime.MinValue;
        SourceSystem = string.Empty;
        Payload = string.Empty;
        Status = MessageStatus.Created;
    }

    // Constructor with parameters that auto-generates ID and timestamp
    public Message(string sourceSystem, string payload)
    {
        MessageId = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
        SourceSystem = sourceSystem;
        Payload = payload;
        Status = MessageStatus.Created;
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(SourceSystem) &&
               !string.IsNullOrWhiteSpace(Payload);
    }
}
