namespace IntegrationTests;

using Models;
using Xunit;

public class TransformerTests
{
    [Fact]
    public void Transform_AddsProcessedPrefix()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message = new Message("TestSystem", "Test payload");

        // Act
        var processed = transformer.Transform(message);

        // Assert
        Assert.NotNull(processed);
        Assert.Equal("PROCESSED_Test payload", processed.Payload);
    }

    [Fact]
    public void Transform_PreservesMessageId()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message = new Message("TestSystem", "Test payload");

        // Act
        var processed = transformer.Transform(message);

        // Assert
        Assert.Equal(message.MessageId, processed.MessageId);
    }

    [Fact]
    public void Transform_PreservesSourceSystem()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message = new Message("TestSystem", "Test payload");

        // Act
        var processed = transformer.Transform(message);

        // Assert
        Assert.Equal(message.SourceSystem, processed.SourceSystem);
    }

    [Fact]
    public void Transform_PreservesOriginalTimestamp()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message = new Message("TestSystem", "Test payload");

        // Act
        var processed = transformer.Transform(message);

        // Assert
        Assert.Equal(message.Timestamp, processed.Timestamp);
    }

    [Fact]
    public void Transform_SetsProcessedAtTimestamp()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message = new Message("TestSystem", "Test payload");
        var beforeTransform = DateTime.UtcNow;

        // Act
        var processed = transformer.Transform(message);
        var afterTransform = DateTime.UtcNow;

        // Assert
        Assert.InRange(processed.ProcessedAt, beforeTransform, afterTransform);
    }

    [Fact]
    public void Transform_UpdatesStatusToProcessing()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message = new Message("TestSystem", "Test payload");
        message.Status = MessageStatus.Received;

        // Act
        var processed = transformer.Transform(message);

        // Assert
        Assert.Equal(MessageStatus.Processing, processed.Status);
    }

    [Fact]
    public void Transform_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var transformer = new MessageTransformer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => transformer.Transform(null!));
    }

    [Fact]
    public void Transform_EmptyPayload_AddsPrefix()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message = new Message("TestSystem", "");

        // Act
        var processed = transformer.Transform(message);

        // Assert
        Assert.Equal("PROCESSED_", processed.Payload);
    }

    [Fact]
    public void Transform_MultipleMessages_EachGetsProcessedAt()
    {
        // Arrange
        var transformer = new MessageTransformer();
        var message1 = new Message("System1", "Payload1");
        var message2 = new Message("System2", "Payload2");

        // Act
        var processed1 = transformer.Transform(message1);
        var processed2 = transformer.Transform(message2);

        // Assert
        Assert.NotEqual(message1.MessageId, message2.MessageId);
        Assert.True(processed1.ProcessedAt > DateTime.MinValue);
        Assert.True(processed2.ProcessedAt > DateTime.MinValue);
    }
}
