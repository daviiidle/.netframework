namespace Models;

using System.Collections.Concurrent;

public class InMemoryQueue : IMessageQueue
{
    private readonly ConcurrentQueue<Message> _mainQueue;
    private readonly ConcurrentQueue<Message> _deadLetterQueue;
    private readonly ConcurrentDictionary<Guid, bool> _messageIds;
    private readonly object _lockObject = new object();

    public InMemoryQueue()
    {
        _mainQueue = new ConcurrentQueue<Message>();
        _deadLetterQueue = new ConcurrentQueue<Message>();
        _messageIds = new ConcurrentDictionary<Guid, bool>();
    }

    public void Enqueue(Message message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        // Check for duplicate MessageId
        if (!_messageIds.TryAdd(message.MessageId, true))
        {
            throw new DuplicateMessageException(message.MessageId);
        }

        _mainQueue.Enqueue(message);
    }

    public Message? Dequeue()
    {
        if (_mainQueue.TryDequeue(out var message))
        {
            // Remove from tracking dictionary when dequeued
            _messageIds.TryRemove(message.MessageId, out _);
            return message;
        }

        return null;
    }

    public int GetQueueDepth()
    {
        return _mainQueue.Count;
    }

    public void EnqueueToDLQ(Message message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _deadLetterQueue.Enqueue(message);
    }

    public Message? DequeueDLQ()
    {
        if (_deadLetterQueue.TryDequeue(out var message))
        {
            return message;
        }

        return null;
    }

    public int GetDLQDepth()
    {
        return _deadLetterQueue.Count;
    }
}
