namespace Models;

public class DuplicateMessageException : Exception
{
    public Guid MessageId { get; }

    public DuplicateMessageException(Guid messageId)
        : base($"A message with ID {messageId} has already been enqueued.")
    {
        MessageId = messageId;
    }

    public DuplicateMessageException(Guid messageId, string message)
        : base(message)
    {
        MessageId = messageId;
    }

    public DuplicateMessageException(Guid messageId, string message, Exception innerException)
        : base(message, innerException)
    {
        MessageId = messageId;
    }
}
