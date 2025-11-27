namespace IntegrationTests;

using Models;
using Microsoft.Data.Sqlite;
using Xunit;

public class DatabaseTests : IDisposable
{
    private readonly DatabaseService _dbService;
    private readonly SqliteConnection _connection;

    public DatabaseTests()
    {
        // Create in-memory database for testing
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _dbService = new DatabaseService(_connection);
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Fact]
    public void SaveMessage_ValidMessage_SavesSuccessfully()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");
        var processedMessage = new ProcessedMessage(message);

        // Act
        _dbService.SaveMessage(processedMessage);

        // Assert
        var retrieved = _dbService.GetMessageById(message.MessageId);
        Assert.NotNull(retrieved);
        Assert.Equal(message.MessageId, retrieved.MessageId);
        Assert.Equal(message.SourceSystem, retrieved.SourceSystem);
        Assert.Equal(message.Payload, retrieved.Payload);
        Assert.Equal(message.Status, retrieved.Status);
    }

    [Fact]
    public void GetMessageById_ExistingMessage_RetrievesCorrectly()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");
        var processedMessage = new ProcessedMessage(message);
        _dbService.SaveMessage(processedMessage);

        // Act
        var retrieved = _dbService.GetMessageById(message.MessageId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(message.MessageId, retrieved.MessageId);
        Assert.Equal("TestSystem", retrieved.SourceSystem);
        Assert.Equal("Test payload", retrieved.Payload);
        Assert.Equal(message.Timestamp, retrieved.Timestamp);
        Assert.True(retrieved.ProcessedAt > DateTime.MinValue);
    }

    [Fact]
    public void GetMessageById_NonExistentMessage_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var retrieved = _dbService.GetMessageById(nonExistentId);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void SaveMessage_DuplicateMessageId_ThrowsSqliteException()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");
        var processedMessage1 = new ProcessedMessage(message);
        var processedMessage2 = new ProcessedMessage(message);

        _dbService.SaveMessage(processedMessage1);

        // Act & Assert
        Assert.Throws<SqliteException>(() => _dbService.SaveMessage(processedMessage2));
    }

    [Fact]
    public void GetAllMessages_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var messages = _dbService.GetAllMessages();

        // Assert
        Assert.NotNull(messages);
        Assert.Empty(messages);
    }

    [Fact]
    public void GetAllMessages_WithMessages_ReturnsAllMessages()
    {
        // Arrange
        var message1 = new Message("System1", "Payload1");
        var message2 = new Message("System2", "Payload2");
        var message3 = new Message("System3", "Payload3");

        _dbService.SaveMessage(new ProcessedMessage(message1));
        _dbService.SaveMessage(new ProcessedMessage(message2));
        _dbService.SaveMessage(new ProcessedMessage(message3));

        // Act
        var messages = _dbService.GetAllMessages();

        // Assert
        Assert.NotNull(messages);
        Assert.Equal(3, messages.Count);
        Assert.Contains(messages, m => m.MessageId == message1.MessageId);
        Assert.Contains(messages, m => m.MessageId == message2.MessageId);
        Assert.Contains(messages, m => m.MessageId == message3.MessageId);
    }

    [Fact]
    public void SaveMessage_PreservesAllFields()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");
        message.Status = MessageStatus.Processing;
        var processedMessage = new ProcessedMessage(message);

        // Act
        _dbService.SaveMessage(processedMessage);
        var retrieved = _dbService.GetMessageById(message.MessageId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(message.MessageId, retrieved.MessageId);
        Assert.Equal(message.Timestamp, retrieved.Timestamp);
        Assert.Equal(message.SourceSystem, retrieved.SourceSystem);
        Assert.Equal(message.Payload, retrieved.Payload);
        Assert.Equal(MessageStatus.Processing, retrieved.Status);
        Assert.True(retrieved.ProcessedAt <= DateTime.UtcNow);
        Assert.True(retrieved.ProcessedAt >= message.Timestamp);
    }

    [Fact]
    public void DatabaseService_CreatesTableAutomatically()
    {
        // Arrange & Act
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var dbService = new DatabaseService(connection);

        // Assert - If table wasn't created, SaveMessage would throw
        var message = new Message("TestSystem", "Test payload");
        var processedMessage = new ProcessedMessage(message);

        // This should not throw if table was created
        dbService.SaveMessage(processedMessage);
        var retrieved = dbService.GetMessageById(message.MessageId);
        Assert.NotNull(retrieved);
    }
}
