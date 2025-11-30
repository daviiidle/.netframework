namespace IntegrationTests;

using Models;
using Xunit;

[Collection("RabbitMQ")]
public class RabbitMQTests : IDisposable
{
    private readonly RabbitMQQueue? _queue;
    private readonly string _testQueueName;

    public RabbitMQTests()
    {
        // Use unique queue name per test run to avoid conflicts
        _testQueueName = $"test-queue-{Guid.NewGuid()}";

        // Try to connect to RabbitMQ
        try
        {
            _queue = new RabbitMQQueue("localhost", _testQueueName);
            // Clean any existing messages
            _queue.PurgeQueue();
            _queue.PurgeDLQ();
        }
        catch
        {
            // RabbitMQ not available - tests will be skipped
            _queue = null;
        }
    }

    public void Dispose()
    {
        // Clean up queues after tests
        _queue?.DeleteQueues();
        _queue?.Dispose();
    }

    private void SkipIfUnavailable()
    {
        if (_queue == null)
        {
            // This will cause xUnit to fail the test with a helpful message
            Assert.Fail("RabbitMQ is not available. Start with: docker compose up -d");
        }
    }

    [Fact]
    public void Enqueue_AddsMessageToQueue()
    {
        SkipIfUnavailable();

        // Arrange
        var message = new Message("TestSystem", "Test payload");

        // Act
        _queue!.Enqueue(message);

        // Assert
        var depth = _queue.GetQueueDepth();
        Assert.True(depth > 0);
    }

    [Fact]
    public void Dequeue_ReturnsEnqueuedMessage()
    {
        SkipIfUnavailable();

        // Arrange
        var message = new Message("TestSystem", "Test payload");
        _queue!.Enqueue(message);

        // Act
        var dequeuedMessage = _queue.Dequeue();

        // Assert
        Assert.NotNull(dequeuedMessage);
        Assert.Equal(message.MessageId, dequeuedMessage.MessageId);
        Assert.Equal(message.SourceSystem, dequeuedMessage.SourceSystem);
        Assert.Equal(message.Payload, dequeuedMessage.Payload);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ReturnsNull()
    {
        SkipIfUnavailable();

        // Arrange - ensure queue is empty
        while (_queue!.Dequeue() != null) { }

        // Act
        var message = _queue.Dequeue();

        // Assert
        Assert.Null(message);
    }

    [Fact]
    public void GetQueueDepth_ReturnsCorrectCount()
    {
        SkipIfUnavailable();

        // Arrange - clear queue first
        while (_queue!.Dequeue() != null) { }

        var message1 = new Message("TestSystem", "Payload 1");
        var message2 = new Message("TestSystem", "Payload 2");
        var message3 = new Message("TestSystem", "Payload 3");

        // Act
        _queue.Enqueue(message1);
        _queue.Enqueue(message2);
        _queue.Enqueue(message3);

        // Assert
        var depth = _queue.GetQueueDepth();
        Assert.Equal(3, depth);
    }

    [Fact]
    public void EnqueueToDLQ_AddsMessageToDLQ()
    {
        SkipIfUnavailable();

        // Arrange
        var message = new Message("TestSystem", "Failed message");

        // Act
        _queue!.EnqueueToDLQ(message);

        // Assert
        var dlqDepth = _queue.GetDLQDepth();
        Assert.True(dlqDepth > 0);
    }

    [Fact]
    public void DequeueDLQ_ReturnsMessageFromDLQ()
    {
        SkipIfUnavailable();

        // Arrange
        var message = new Message("TestSystem", "Failed message");
        _queue!.EnqueueToDLQ(message);

        // Act
        var dequeuedMessage = _queue.DequeueDLQ();

        // Assert
        Assert.NotNull(dequeuedMessage);
        Assert.Equal(message.MessageId, dequeuedMessage.MessageId);
        Assert.Equal(message.SourceSystem, dequeuedMessage.SourceSystem);
        Assert.Equal(message.Payload, dequeuedMessage.Payload);
    }

    [Fact]
    public void DequeueDLQ_EmptyDLQ_ReturnsNull()
    {
        SkipIfUnavailable();

        // Arrange - ensure DLQ is empty
        while (_queue!.DequeueDLQ() != null) { }

        // Act
        var message = _queue.DequeueDLQ();

        // Assert
        Assert.Null(message);
    }

    [Fact]
    public void GetDLQDepth_ReturnsCorrectCount()
    {
        SkipIfUnavailable();

        // Arrange - clear DLQ first
        while (_queue!.DequeueDLQ() != null) { }

        var message1 = new Message("TestSystem", "Failed 1");
        var message2 = new Message("TestSystem", "Failed 2");

        // Act
        _queue.EnqueueToDLQ(message1);
        _queue.EnqueueToDLQ(message2);

        // Assert
        var dlqDepth = _queue.GetDLQDepth();
        Assert.Equal(2, dlqDepth);
    }

    [Fact]
    public void MultipleEnqueueDequeue_MaintainsFIFOOrder()
    {
        SkipIfUnavailable();

        // Arrange - clear queue first
        while (_queue!.Dequeue() != null) { }

        var message1 = new Message("TestSystem", "First");
        var message2 = new Message("TestSystem", "Second");
        var message3 = new Message("TestSystem", "Third");

        // Act
        _queue.Enqueue(message1);
        _queue.Enqueue(message2);
        _queue.Enqueue(message3);

        var dequeued1 = _queue.Dequeue();
        var dequeued2 = _queue.Dequeue();
        var dequeued3 = _queue.Dequeue();

        // Assert
        Assert.NotNull(dequeued1);
        Assert.NotNull(dequeued2);
        Assert.NotNull(dequeued3);
        Assert.Equal(message1.MessageId, dequeued1.MessageId);
        Assert.Equal(message2.MessageId, dequeued2.MessageId);
        Assert.Equal(message3.MessageId, dequeued3.MessageId);
    }

    [Fact]
    public void QueueAndDLQ_AreIndependent()
    {
        SkipIfUnavailable();

        // Arrange - clear both queues
        while (_queue!.Dequeue() != null) { }
        while (_queue.DequeueDLQ() != null) { }

        var regularMessage = new Message("TestSystem", "Regular");
        var dlqMessage = new Message("TestSystem", "DLQ");

        // Act
        _queue.Enqueue(regularMessage);
        _queue.EnqueueToDLQ(dlqMessage);

        // Assert
        Assert.Equal(1, _queue.GetQueueDepth());
        Assert.Equal(1, _queue.GetDLQDepth());

        var dequeuedRegular = _queue.Dequeue();
        var dequeuedDLQ = _queue.DequeueDLQ();

        Assert.NotNull(dequeuedRegular);
        Assert.NotNull(dequeuedDLQ);
        Assert.Equal(regularMessage.MessageId, dequeuedRegular.MessageId);
        Assert.Equal(dlqMessage.MessageId, dequeuedDLQ.MessageId);
    }
}
