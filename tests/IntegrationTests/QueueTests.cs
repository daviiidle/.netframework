namespace IntegrationTests;

using Models;
using Xunit;

public class QueueTests
{
    [Fact]
    public void Enqueue_IncreasesQueueDepth()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var message = new Message("TestSystem", "Test payload");
        var initialDepth = queue.GetQueueDepth();

        // Act
        queue.Enqueue(message);
        var newDepth = queue.GetQueueDepth();

        // Assert
        Assert.Equal(initialDepth + 1, newDepth);
        Assert.Equal(1, newDepth);
    }

    [Fact]
    public void Dequeue_ReturnsMessage()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var message = new Message("TestSystem", "Test payload");
        queue.Enqueue(message);

        // Act
        var dequeuedMessage = queue.Dequeue();

        // Assert
        Assert.NotNull(dequeuedMessage);
        Assert.Equal(message.MessageId, dequeuedMessage.MessageId);
        Assert.Equal(message.SourceSystem, dequeuedMessage.SourceSystem);
        Assert.Equal(message.Payload, dequeuedMessage.Payload);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ReturnsNull()
    {
        // Arrange
        var queue = new InMemoryQueue();

        // Act
        var dequeuedMessage = queue.Dequeue();

        // Assert
        Assert.Null(dequeuedMessage);
    }

    [Fact]
    public void Enqueue_DuplicateMessageId_ThrowsDuplicateMessageException()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var message = new Message("TestSystem", "Test payload");
        queue.Enqueue(message);

        // Act & Assert
        var exception = Assert.Throws<DuplicateMessageException>(() => queue.Enqueue(message));
        Assert.Contains(message.MessageId.ToString(), exception.Message);
    }

    [Fact]
    public void EnqueueToDLQ_IncreasesDepth()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var message = new Message("TestSystem", "Test payload");
        var initialDepth = queue.GetDLQDepth();

        // Act
        queue.EnqueueToDLQ(message);
        var newDepth = queue.GetDLQDepth();

        // Assert
        Assert.Equal(initialDepth + 1, newDepth);
        Assert.Equal(1, newDepth);
    }

    [Fact]
    public void DequeueDLQ_ReturnsMessage()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var message = new Message("TestSystem", "Test payload");
        queue.EnqueueToDLQ(message);

        // Act
        var dequeuedMessage = queue.DequeueDLQ();

        // Assert
        Assert.NotNull(dequeuedMessage);
        Assert.Equal(message.MessageId, dequeuedMessage.MessageId);
    }

    [Fact]
    public void DequeueDLQ_EmptyQueue_ReturnsNull()
    {
        // Arrange
        var queue = new InMemoryQueue();

        // Act
        var dequeuedMessage = queue.DequeueDLQ();

        // Assert
        Assert.Null(dequeuedMessage);
    }

    [Fact]
    public async Task Queue_IsThreadSafe()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var messageCount = 100;
        var tasks = new List<Task>();

        // Act - Enqueue messages from multiple threads
        for (int i = 0; i < messageCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var message = new Message($"System{index}", $"Payload{index}");
                queue.Enqueue(message);
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert
        Assert.Equal(messageCount, queue.GetQueueDepth());

        // Dequeue all messages
        var dequeuedCount = 0;
        while (queue.Dequeue() != null)
        {
            dequeuedCount++;
        }
        Assert.Equal(messageCount, dequeuedCount);
    }

    [Fact]
    public void Dequeue_ReducesQueueDepth()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var message = new Message("TestSystem", "Test payload");
        queue.Enqueue(message);
        var depthBeforeDequeue = queue.GetQueueDepth();

        // Act
        queue.Dequeue();
        var depthAfterDequeue = queue.GetQueueDepth();

        // Assert
        Assert.Equal(depthBeforeDequeue - 1, depthAfterDequeue);
        Assert.Equal(0, depthAfterDequeue);
    }
}
