namespace Models;

public interface IMessageQueue
{
    void Enqueue(Message message);
    Message? Dequeue();
    int GetQueueDepth();
    void EnqueueToDLQ(Message message);
    Message? DequeueDLQ();
    int GetDLQDepth();
}
