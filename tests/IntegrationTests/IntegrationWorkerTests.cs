namespace IntegrationTests;

using Models;
using Microsoft.Data.Sqlite;
using Xunit;

public class IntegrationWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatabaseService _dbService;
    private readonly IMessageQueue _queue;
    private readonly MessageTransformer _transformer;
    private readonly IntegrationWorker _worker;

    public IntegrationWorkerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _dbService = new DatabaseService(_connection);
        _queue = new InMemoryQueue();
        _transformer = new MessageTransformer();
        _worker = new IntegrationWorker(_queue, _transformer, _dbService);
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Fact]
    public void ProcessMessage_ValidMessage_SavesToDatabase()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");
        _queue.Enqueue(message);

        // Act
        var result = _worker.ProcessMessage();

        // Assert
        Assert.True(result);
        var saved = _dbService.GetMessageById(message.MessageId);
        Assert.NotNull(saved);
        Assert.Equal("PROCESSED_Test payload", saved.Payload);
        Assert.Equal(MessageStatus.Completed, saved.Status);
    }

    [Fact]
    public void ProcessMessage_EmptyQueue_ReturnsFalse()
    {
        // Act
        var result = _worker.ProcessMessage();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ProcessMessage_ReducesQueueDepth()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");
        _queue.Enqueue(message);
        Assert.Equal(1, _queue.GetQueueDepth());

        // Act
        _worker.ProcessMessage();

        // Assert
        Assert.Equal(0, _queue.GetQueueDepth());
    }

    [Fact]
    public void ProcessMessage_PreservesMessageId()
    {
        // Arrange
        var message = new Message("TestSystem", "Test payload");
        var originalId = message.MessageId;
        _queue.Enqueue(message);

        // Act
        _worker.ProcessMessage();

        // Assert
        var saved = _dbService.GetMessageById(originalId);
        Assert.NotNull(saved);
        Assert.Equal(originalId, saved.MessageId);
    }

    [Fact]
    public void EndToEnd_PublishProcessSave()
    {
        // Arrange
        var publisher = new MessagePublisher(_queue);

        // Act - Publish
        var message = publisher.Publish("GovSystem", "Citizen data");
        Assert.Equal(1, _queue.GetQueueDepth());
        Assert.Equal(MessageStatus.Sent, message.Status);

        // Act - Process
        var processed = _worker.ProcessMessage();
        Assert.True(processed);
        Assert.Equal(0, _queue.GetQueueDepth());

        // Assert - Verify in database
        var saved = _dbService.GetMessageById(message.MessageId);
        Assert.NotNull(saved);
        Assert.Equal("PROCESSED_Citizen data", saved.Payload);
        Assert.Equal("GovSystem", saved.SourceSystem);
        Assert.Equal(MessageStatus.Completed, saved.Status);
        Assert.True(saved.ProcessedAt > saved.Timestamp);
    }

    [Fact]
    public void ProcessMessage_MultipleMessages_ProcessesInOrder()
    {
        // Arrange
        var message1 = new Message("System1", "Payload1");
        var message2 = new Message("System2", "Payload2");
        var message3 = new Message("System3", "Payload3");
        _queue.Enqueue(message1);
        _queue.Enqueue(message2);
        _queue.Enqueue(message3);

        // Act
        _worker.ProcessMessage(); // Process first
        _worker.ProcessMessage(); // Process second
        _worker.ProcessMessage(); // Process third

        // Assert
        var all = _dbService.GetAllMessages();
        Assert.Equal(3, all.Count);

        var saved1 = _dbService.GetMessageById(message1.MessageId);
        var saved2 = _dbService.GetMessageById(message2.MessageId);
        var saved3 = _dbService.GetMessageById(message3.MessageId);

        Assert.NotNull(saved1);
        Assert.NotNull(saved2);
        Assert.NotNull(saved3);
        Assert.Equal("PROCESSED_Payload1", saved1.Payload);
        Assert.Equal("PROCESSED_Payload2", saved2.Payload);
        Assert.Equal("PROCESSED_Payload3", saved3.Payload);
    }

    [Fact]
    public void ProcessMessages_ProcessesMultiple()
    {
        // Arrange
        _queue.Enqueue(new Message("System1", "Payload1"));
        _queue.Enqueue(new Message("System2", "Payload2"));
        _queue.Enqueue(new Message("System3", "Payload3"));

        // Act
        var count = _worker.ProcessMessages(3);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(0, _queue.GetQueueDepth());
        Assert.Equal(3, _dbService.GetAllMessages().Count);
    }

    [Fact]
    public void ProcessMessages_MaxCountLimits()
    {
        // Arrange
        _queue.Enqueue(new Message("System1", "Payload1"));
        _queue.Enqueue(new Message("System2", "Payload2"));
        _queue.Enqueue(new Message("System3", "Payload3"));

        // Act - Only process 2 messages
        var count = _worker.ProcessMessages(2);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(1, _queue.GetQueueDepth());
        Assert.Equal(2, _dbService.GetAllMessages().Count);
    }

    [Fact]
    public void ProcessMessage_DatabaseFailure_Retries3TimesThenSucceeds()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var failingDb = new FailingDatabaseService(connection, failureCount: 3);
        var queue = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var errorLogger = new ErrorLogger(Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.log"));
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var worker = new IntegrationWorker(queue, transformer, failingDb, retryPolicy, errorLogger);

        var message = new Message("TestSystem", "Test payload");
        queue.Enqueue(message);

        // Act
        var result = worker.ProcessMessage();

        // Assert
        Assert.True(result);
        Assert.Equal(4, failingDb.SaveMessageCallCount); // 1 initial + 3 retries
        var saved = failingDb.GetMessageById(message.MessageId);
        Assert.NotNull(saved);
        Assert.Equal(MessageStatus.Completed, saved.Status);

        connection.Close();
        connection.Dispose();
    }

    [Fact]
    public void ProcessMessage_MaxRetriesExceeded_SendsToDLQ()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var failingDb = new FailingDatabaseService(connection, failureCount: 10); // More failures than max retries
        var queue = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var errorLogger = new ErrorLogger(Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.log"));
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var worker = new IntegrationWorker(queue, transformer, failingDb, retryPolicy, errorLogger);

        var message = new Message("TestSystem", "Test payload");
        queue.Enqueue(message);

        // Act
        var result = worker.ProcessMessage();

        // Assert
        Assert.False(result); // Processing failed
        Assert.Equal(4, failingDb.SaveMessageCallCount); // 1 initial + 3 retries
        Assert.Equal(1, queue.GetDLQDepth()); // Message sent to DLQ
        Assert.Equal(0, queue.GetQueueDepth()); // Main queue is empty

        var dlqMessage = queue.DequeueDLQ();
        Assert.NotNull(dlqMessage);
        Assert.Equal(message.MessageId, dlqMessage.MessageId);
        Assert.Equal(MessageStatus.Failed, dlqMessage.Status);

        connection.Close();
        connection.Dispose();
    }

    [Fact]
    public void ProcessMessage_ValidationFailure_GoesDirectlyToDLQ()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var dbService = new DatabaseService(connection);
        var queue = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var errorLogger = new ErrorLogger(Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.log"));
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var worker = new IntegrationWorker(queue, transformer, dbService, retryPolicy, errorLogger);

        // Create invalid message (empty source system)
        var message = new Message("", "Test payload");
        queue.Enqueue(message);

        // Act
        var result = worker.ProcessMessage();

        // Assert
        Assert.False(result); // Processing failed
        Assert.Equal(1, queue.GetDLQDepth()); // Message sent to DLQ immediately
        Assert.Equal(0, queue.GetQueueDepth()); // Main queue is empty
        Assert.Empty(dbService.GetAllMessages()); // Not saved to DB

        var dlqMessage = queue.DequeueDLQ();
        Assert.NotNull(dlqMessage);
        Assert.Equal(message.MessageId, dlqMessage.MessageId);
        Assert.Equal(MessageStatus.Failed, dlqMessage.Status);

        connection.Close();
        connection.Dispose();
    }

    [Fact]
    public void ProcessMessage_TransientFailure_LogsErrors()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var failingDb = new FailingDatabaseService(connection, failureCount: 2);
        var queue = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var logFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.log");
        var errorLogger = new ErrorLogger(logFile);
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var worker = new IntegrationWorker(queue, transformer, failingDb, retryPolicy, errorLogger);

        var message = new Message("TestSystem", "Test payload");
        queue.Enqueue(message);

        // Act
        worker.ProcessMessage();

        // Assert - Check that errors were logged
        Assert.True(File.Exists(logFile));
        var logContent = File.ReadAllText(logFile);
        Assert.Contains(message.MessageId.ToString(), logContent);
        Assert.Contains("Attempt:", logContent);

        // Cleanup
        connection.Close();
        connection.Dispose();
        if (File.Exists(logFile))
            File.Delete(logFile);
    }
}
