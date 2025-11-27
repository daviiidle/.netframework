namespace Models;

public class MessagePublisher
{
    private readonly IMessageQueue _queue;

    public MessagePublisher(IMessageQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    /// <summary>
    /// Publishes a new message to the queue by creating it from source system and payload.
    /// </summary>
    /// <param name="sourceSystem">The source system identifier</param>
    /// <param name="payload">The message payload</param>
    /// <returns>The published message with status set to Sent</returns>
    /// <exception cref="ValidationException">Thrown when the message is invalid</exception>
    /// <exception cref="DuplicateMessageException">Thrown when a duplicate message is detected</exception>
    public Message Publish(string sourceSystem, string payload)
    {
        var message = new Message(sourceSystem, payload);
        return Publish(message);
    }

    /// <summary>
    /// Publishes an existing message to the queue.
    /// </summary>
    /// <param name="message">The message to publish</param>
    /// <returns>The published message with status set to Sent</returns>
    /// <exception cref="ValidationException">Thrown when the message is invalid</exception>
    /// <exception cref="DuplicateMessageException">Thrown when a duplicate message is detected</exception>
    public Message Publish(Message message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        // Validate the message
        if (!message.IsValid())
        {
            throw new ValidationException("Invalid message: SourceSystem and Payload must not be empty.");
        }

        // Update status to Sent
        message.Status = MessageStatus.Sent;

        // Enqueue the message (may throw DuplicateMessageException)
        _queue.Enqueue(message);

        return message;
    }
}
