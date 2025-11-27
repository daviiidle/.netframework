namespace IntegrationTests;

using Models;
using Xunit;

public class MessageTests
{
    [Fact]
    public void Message_WhenCreated_HasValidGuid()
    {
        // Arrange & Act
        var message = new Message("TestSystem", "Test payload");

        // Assert
        Assert.NotEqual(Guid.Empty, message.MessageId);
    }

    [Fact]
    public void Message_WhenCreated_HasRecentTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var message = new Message("TestSystem", "Test payload");
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.InRange(message.Timestamp, beforeCreation, afterCreation);
    }

    [Fact]
    public void Message_WhenCreated_StatusIsCreated()
    {
        // Arrange & Act
        var message = new Message("TestSystem", "Test payload");

        // Assert
        Assert.Equal(MessageStatus.Created, message.Status);
    }

    [Fact]
    public void Message_EmptySourceSystem_IsNotValid()
    {
        // Arrange
        var message = new Message("", "Test payload");

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Message_EmptyPayload_IsNotValid()
    {
        // Arrange
        var message = new Message("TestSystem", "");

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Message_ValidData_IsValid()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.True(isValid);
    }
}
