namespace Models;

public class MessageTransformer
{
    public ProcessedMessage Transform(Message message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var processedMessage = new ProcessedMessage(message)
        {
            Payload = $"PROCESSED_{message.Payload}",
            Status = MessageStatus.Processing
        };

        return processedMessage;
    }
}
