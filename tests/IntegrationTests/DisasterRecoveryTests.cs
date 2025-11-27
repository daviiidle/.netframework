namespace IntegrationTests;

using Models;
using Microsoft.Data.Sqlite;
using Xunit;

public class DisasterRecoveryTests : IDisposable
{
    private readonly string _persistenceFile;
    private readonly string _logFile;

    public DisasterRecoveryTests()
    {
        _persistenceFile = Path.Combine(Path.GetTempPath(), $"recovery_{Guid.NewGuid()}.json");
        _logFile = Path.Combine(Path.GetTempPath(), $"errors_{Guid.NewGuid()}.log");
    }

    public void Dispose()
    {
        if (File.Exists(_persistenceFile))
            File.Delete(_persistenceFile);
        if (File.Exists(_logFile))
            File.Delete(_logFile);
    }

    [Fact]
    public void ServiceCrash_PersistsUnprocessedMessages()
    {
        // Arrange - Simulate service with messages in queue
        var queue = new InMemoryQueue();
        var persistenceService = new PersistenceService(_persistenceFile);

        var message1 = new Message("System1", "Payload1");
        var message2 = new Message("System2", "Payload2");
        var message3 = new Message("System3", "Payload3");

        queue.Enqueue(message1);
        queue.Enqueue(message2);
        queue.Enqueue(message3);

        Assert.Equal(3, queue.GetQueueDepth());

        // Act - Simulate crash by persisting unprocessed messages
        var unprocessedMessages = new List<Message>();
        while (queue.GetQueueDepth() > 0)
        {
            var msg = queue.Dequeue();
            if (msg != null)
                unprocessedMessages.Add(msg);
        }
        persistenceService.SaveUnprocessedMessages(unprocessedMessages);

        // Assert
        Assert.True(File.Exists(_persistenceFile));
        Assert.Equal(0, queue.GetQueueDepth()); // Queue is empty (simulating crash)

        var savedMessages = persistenceService.LoadUnprocessedMessages();
        Assert.Equal(3, savedMessages.Count);
        Assert.Contains(savedMessages, m => m.MessageId == message1.MessageId);
        Assert.Contains(savedMessages, m => m.MessageId == message2.MessageId);
        Assert.Contains(savedMessages, m => m.MessageId == message3.MessageId);
    }

    [Fact]
    public void ServiceRestart_RecoversMessages()
    {
        // Arrange - Save messages to persistence file (simulating previous crash)
        var originalMessage1 = new Message("System1", "Payload1");
        var originalMessage2 = new Message("System2", "Payload2");
        var originalMessages = new List<Message> { originalMessage1, originalMessage2 };

        var persistenceService = new PersistenceService(_persistenceFile);
        persistenceService.SaveUnprocessedMessages(originalMessages);

        // Act - Simulate service restart by loading messages
        var newQueue = new InMemoryQueue();
        var recoveredMessages = persistenceService.LoadUnprocessedMessages();

        foreach (var message in recoveredMessages)
        {
            newQueue.Enqueue(message);
        }

        // Assert
        Assert.Equal(2, newQueue.GetQueueDepth());

        var msg1 = newQueue.Dequeue();
        var msg2 = newQueue.Dequeue();

        Assert.NotNull(msg1);
        Assert.NotNull(msg2);
        Assert.Equal(originalMessage1.MessageId, msg1.MessageId);
        Assert.Equal(originalMessage2.MessageId, msg2.MessageId);
    }

    [Fact]
    public void RecoveredMessages_CanBeProcessed()
    {
        // Arrange - Persist messages
        var originalMessage = new Message("GovSystem", "Citizen data");
        var persistenceService = new PersistenceService(_persistenceFile);
        persistenceService.SaveUnprocessedMessages(new List<Message> { originalMessage });

        // Simulate restart - recover and process
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var dbService = new DatabaseService(connection);
        var queue = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var worker = new IntegrationWorker(queue, transformer, dbService);

        // Act - Load recovered messages and process
        var recoveredMessages = persistenceService.LoadUnprocessedMessages();
        foreach (var message in recoveredMessages)
        {
            queue.Enqueue(message);
        }

        var processed = worker.ProcessMessage();

        // Assert
        Assert.True(processed);
        Assert.Equal(0, queue.GetQueueDepth());

        var savedMessage = dbService.GetMessageById(originalMessage.MessageId);
        Assert.NotNull(savedMessage);
        Assert.Equal("PROCESSED_Citizen data", savedMessage.Payload);
        Assert.Equal(MessageStatus.Completed, savedMessage.Status);

        connection.Close();
        connection.Dispose();
    }

    [Fact]
    public void CircuitBreaker_ProtectsDatabaseFailures()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var failingDb = new FailingDatabaseService(connection, failureCount: 100); // Always fails
        var queue = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var errorLogger = new ErrorLogger(_logFile);
        var retryPolicy = new RetryPolicy(maxRetries: 0); // No retries for faster test
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(10));

        var worker = new IntegrationWorker(queue, transformer, failingDb, retryPolicy, errorLogger, circuitBreaker);

        // Add messages
        queue.Enqueue(new Message("System1", "Payload1"));
        queue.Enqueue(new Message("System2", "Payload2"));
        queue.Enqueue(new Message("System3", "Payload3"));
        queue.Enqueue(new Message("System4", "Payload4"));

        // Act - Process messages until circuit opens
        var result1 = worker.ProcessMessage(); // Message 1 - fails, circuit failure count = 1
        var result2 = worker.ProcessMessage(); // Message 2 - fails, circuit failure count = 2
        var result3 = worker.ProcessMessage(); // Message 3 - fails, circuit failure count = 3, OPENS
        var result4 = worker.ProcessMessage(); // Message 4 - circuit open, immediate DLQ

        // Assert
        Assert.False(result1);
        Assert.False(result2);
        Assert.False(result3);
        Assert.False(result4);

        // Circuit should be open after 3 message failures
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
        Assert.Equal(4, queue.GetDLQDepth()); // All 4 messages went to DLQ

        // Verify circuit breaker blocks further attempts
        queue.Enqueue(new Message("System5", "Payload5"));
        var result5 = worker.ProcessMessage();

        Assert.False(result5); // Should fail due to circuit breaker
        Assert.Equal(5, queue.GetDLQDepth()); // Additional message sent to DLQ due to circuit breaker

        connection.Close();
        connection.Dispose();
    }

    [Fact]
    public void EndToEnd_CrashAndRecovery()
    {
        // Arrange - Initial service with messages
        var queue1 = new InMemoryQueue();
        var persistenceService = new PersistenceService(_persistenceFile);

        var message1 = new Message("System1", "Payload1");
        var message2 = new Message("System2", "Payload2");

        queue1.Enqueue(message1);
        queue1.Enqueue(message2);

        // Simulate crash - persist unprocessed messages
        var unprocessed = new List<Message>();
        while (queue1.GetQueueDepth() > 0)
        {
            var msg = queue1.Dequeue();
            if (msg != null)
                unprocessed.Add(msg);
        }
        persistenceService.SaveUnprocessedMessages(unprocessed);

        // Act - Simulate restart
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var dbService = new DatabaseService(connection);
        var queue2 = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var worker = new IntegrationWorker(queue2, transformer, dbService);

        // Recover messages
        var recovered = persistenceService.LoadUnprocessedMessages();
        foreach (var msg in recovered)
        {
            queue2.Enqueue(msg);
        }

        // Process recovered messages
        worker.ProcessAllMessages();

        // Assert
        Assert.Equal(0, queue2.GetQueueDepth());
        Assert.Equal(2, dbService.GetAllMessages().Count);

        var saved1 = dbService.GetMessageById(message1.MessageId);
        var saved2 = dbService.GetMessageById(message2.MessageId);

        Assert.NotNull(saved1);
        Assert.NotNull(saved2);
        Assert.Equal(MessageStatus.Completed, saved1.Status);
        Assert.Equal(MessageStatus.Completed, saved2.Status);

        connection.Close();
        connection.Dispose();
    }

    [Fact]
    public void CircuitBreaker_RecoveryAfterTimeout()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var failingDb = new FailingDatabaseService(connection, failureCount: 3); // Fails 3 times then succeeds
        var queue = new InMemoryQueue();
        var transformer = new MessageTransformer();
        var errorLogger = new ErrorLogger(_logFile);
        var retryPolicy = new RetryPolicy(maxRetries: 0); // No retries for faster test
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromMilliseconds(100));

        var worker = new IntegrationWorker(queue, transformer, failingDb, retryPolicy, errorLogger, circuitBreaker);

        // Open circuit with 3 failures
        queue.Enqueue(new Message("System1", "Payload1"));
        queue.Enqueue(new Message("System2", "Payload2"));
        queue.Enqueue(new Message("System3", "Payload3"));

        for (int i = 0; i < 3; i++)
        {
            try { worker.ProcessMessage(); } catch { }
        }

        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Act - Wait for circuit breaker timeout
        Thread.Sleep(150);

        // Enqueue new message and try processing
        queue.Enqueue(new Message("System4", "Payload4"));
        var result = worker.ProcessMessage();

        // Assert - Circuit should close after successful request in half-open state
        Assert.True(result);
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);

        connection.Close();
        connection.Dispose();
    }
}
