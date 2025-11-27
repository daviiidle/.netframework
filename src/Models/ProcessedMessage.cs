namespace Models;

public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public DateTime Timestamp { get; set; }
    public string SourceSystem { get; set; }
    public string Payload { get; set; }
    public MessageStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }

    // Parameterless constructor for deserialization
    public ProcessedMessage()
    {
        MessageId = Guid.Empty;
        Timestamp = DateTime.MinValue;
        SourceSystem = string.Empty;
        Payload = string.Empty;
        Status = MessageStatus.Created;
        ProcessedAt = DateTime.MinValue;
    }

    // Constructor from Message
    public ProcessedMessage(Message message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        MessageId = message.MessageId;
        Timestamp = message.Timestamp;
        SourceSystem = message.SourceSystem;
        Payload = message.Payload;
        Status = message.Status;
        ProcessedAt = DateTime.UtcNow;
    }
}
