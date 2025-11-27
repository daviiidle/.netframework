namespace IntegrationTests;

using Models;
using Xunit;

public class PublisherTests
{
    [Fact]
    public void PublishValid_CreatesMessageInQueue()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);
        var sourceSystem = "TestSystem";
        var payload = "Test payload";

        // Act
        var message = publisher.Publish(sourceSystem, payload);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(sourceSystem, message.SourceSystem);
        Assert.Equal(payload, message.Payload);
        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.Equal(1, queue.GetQueueDepth());

        // Verify message is actually in the queue
        var dequeuedMessage = queue.Dequeue();
        Assert.NotNull(dequeuedMessage);
        Assert.Equal(message.MessageId, dequeuedMessage.MessageId);
    }

    [Fact]
    public void PublishInvalid_EmptySourceSystem_ThrowsValidationException()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            publisher.Publish("", "Test payload"));

        Assert.Contains("Invalid message", exception.Message);
        Assert.Equal(0, queue.GetQueueDepth());
    }

    [Fact]
    public void PublishInvalid_EmptyPayload_ThrowsValidationException()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            publisher.Publish("TestSystem", ""));

        Assert.Contains("Invalid message", exception.Message);
        Assert.Equal(0, queue.GetQueueDepth());
    }

    [Fact]
    public void PublishInvalid_NullSourceSystem_ThrowsValidationException()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            publisher.Publish(null!, "Test payload"));

        Assert.Contains("Invalid message", exception.Message);
        Assert.Equal(0, queue.GetQueueDepth());
    }

    [Fact]
    public void PublishInvalid_NullPayload_ThrowsValidationException()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            publisher.Publish("TestSystem", null!));

        Assert.Contains("Invalid message", exception.Message);
        Assert.Equal(0, queue.GetQueueDepth());
    }

    [Fact]
    public void Publish_DuplicateMessage_ThrowsDuplicateMessageException()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);
        var message = new Message("TestSystem", "Test payload");

        // Manually enqueue the message first
        queue.Enqueue(message);

        // Act & Assert - Try to publish the same message
        var exception = Assert.Throws<DuplicateMessageException>(() =>
            publisher.Publish(message));

        Assert.Equal(message.MessageId, exception.MessageId);
        Assert.Equal(1, queue.GetQueueDepth()); // Should still be 1
    }

    [Fact]
    public void Publish_WithExistingMessage_UpdatesStatus()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);
        var message = new Message("TestSystem", "Test payload");
        Assert.Equal(MessageStatus.Created, message.Status);

        // Act
        var publishedMessage = publisher.Publish(message);

        // Assert
        Assert.Equal(MessageStatus.Sent, publishedMessage.Status);
        Assert.Equal(message.MessageId, publishedMessage.MessageId);
        Assert.Equal(1, queue.GetQueueDepth());
    }

    [Fact]
    public void Publish_MultipleValidMessages_AllEnqueued()
    {
        // Arrange
        var queue = new InMemoryQueue();
        var publisher = new MessagePublisher(queue);

        // Act
        var message1 = publisher.Publish("System1", "Payload1");
        var message2 = publisher.Publish("System2", "Payload2");
        var message3 = publisher.Publish("System3", "Payload3");

        // Assert
        Assert.Equal(3, queue.GetQueueDepth());
        Assert.NotEqual(message1.MessageId, message2.MessageId);
        Assert.NotEqual(message2.MessageId, message3.MessageId);
    }
}
