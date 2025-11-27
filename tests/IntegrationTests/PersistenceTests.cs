namespace IntegrationTests;

using Models;
using Xunit;
using System.Text.Json;

public class PersistenceTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly PersistenceService _persistenceService;

    public PersistenceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_persistence_{Guid.NewGuid()}.json");
        _persistenceService = new PersistenceService(_testFilePath);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            try
            {
                File.Delete(_testFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void SaveUnprocessedMessages_CreatesJsonFile()
    {
        // Arrange
        var messages = new List<Message>
        {
            new Message("System1", "Payload1"),
            new Message("System2", "Payload2")
        };

        // Act
        _persistenceService.SaveUnprocessedMessages(messages);

        // Assert
        Assert.True(File.Exists(_testFilePath));
    }

    [Fact]
    public void SaveUnprocessedMessages_WritesValidJson()
    {
        // Arrange
        var messages = new List<Message>
        {
            new Message("System1", "Payload1")
        };

        // Act
        _persistenceService.SaveUnprocessedMessages(messages);

        // Assert
        var json = File.ReadAllText(_testFilePath);
        Assert.NotEmpty(json);

        // Verify it's valid JSON
        var deserialized = JsonSerializer.Deserialize<List<Message>>(json);
        Assert.NotNull(deserialized);
    }

    [Fact]
    public void LoadUnprocessedMessages_ReturnsCorrectMessages()
    {
        // Arrange
        var message1 = new Message("System1", "Payload1");
        var message2 = new Message("System2", "Payload2");
        var messages = new List<Message> { message1, message2 };

        _persistenceService.SaveUnprocessedMessages(messages);

        // Act
        var loadedMessages = _persistenceService.LoadUnprocessedMessages();

        // Assert
        Assert.NotNull(loadedMessages);
        Assert.Equal(2, loadedMessages.Count);

        Assert.Equal(message1.SourceSystem, loadedMessages[0].SourceSystem);
        Assert.Equal(message1.Payload, loadedMessages[0].Payload);

        Assert.Equal(message2.SourceSystem, loadedMessages[1].SourceSystem);
        Assert.Equal(message2.Payload, loadedMessages[1].Payload);
    }

    [Fact]
    public void LoadUnprocessedMessages_PreservesMessageIds()
    {
        // Arrange
        var message1 = new Message("System1", "Payload1");
        var message2 = new Message("System2", "Payload2");
        var originalId1 = message1.MessageId;
        var originalId2 = message2.MessageId;
        var messages = new List<Message> { message1, message2 };

        _persistenceService.SaveUnprocessedMessages(messages);

        // Act
        var loadedMessages = _persistenceService.LoadUnprocessedMessages();

        // Assert
        Assert.Equal(originalId1, loadedMessages[0].MessageId);
        Assert.Equal(originalId2, loadedMessages[1].MessageId);
    }

    [Fact]
    public void LoadUnprocessedMessages_PreservesTimestamps()
    {
        // Arrange
        var message = new Message("System1", "Payload1");
        var originalTimestamp = message.Timestamp;
        var messages = new List<Message> { message };

        _persistenceService.SaveUnprocessedMessages(messages);

        // Act
        var loadedMessages = _persistenceService.LoadUnprocessedMessages();

        // Assert
        Assert.Equal(originalTimestamp, loadedMessages[0].Timestamp);
    }

    [Fact]
    public void LoadUnprocessedMessages_PreservesStatus()
    {
        // Arrange
        var message = new Message("System1", "Payload1");
        message.Status = MessageStatus.Sent;
        var messages = new List<Message> { message };

        _persistenceService.SaveUnprocessedMessages(messages);

        // Act
        var loadedMessages = _persistenceService.LoadUnprocessedMessages();

        // Assert
        Assert.Equal(MessageStatus.Sent, loadedMessages[0].Status);
    }

    [Fact]
    public void LoadUnprocessedMessages_FileNotExists_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
        var service = new PersistenceService(nonExistentPath);

        // Act
        var loadedMessages = service.LoadUnprocessedMessages();

        // Assert
        Assert.NotNull(loadedMessages);
        Assert.Empty(loadedMessages);
    }

    [Fact]
    public void SaveUnprocessedMessages_EmptyList_CreatesEmptyJsonArray()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        _persistenceService.SaveUnprocessedMessages(messages);

        // Assert
        Assert.True(File.Exists(_testFilePath));
        var loadedMessages = _persistenceService.LoadUnprocessedMessages();
        Assert.Empty(loadedMessages);
    }

    [Fact]
    public void SaveUnprocessedMessages_OverwritesExistingFile()
    {
        // Arrange
        var firstMessages = new List<Message>
        {
            new Message("System1", "Payload1"),
            new Message("System2", "Payload2")
        };
        var secondMessages = new List<Message>
        {
            new Message("System3", "Payload3")
        };

        // Act
        _persistenceService.SaveUnprocessedMessages(firstMessages);
        _persistenceService.SaveUnprocessedMessages(secondMessages);

        // Assert
        var loadedMessages = _persistenceService.LoadUnprocessedMessages();
        Assert.Single(loadedMessages);
        Assert.Equal("System3", loadedMessages[0].SourceSystem);
    }

    [Fact]
    public void SaveAndLoad_LargeNumberOfMessages_WorksCorrectly()
    {
        // Arrange
        var messages = new List<Message>();
        for (int i = 0; i < 100; i++)
        {
            messages.Add(new Message($"System{i}", $"Payload{i}"));
        }

        // Act
        _persistenceService.SaveUnprocessedMessages(messages);
        var loadedMessages = _persistenceService.LoadUnprocessedMessages();

        // Assert
        Assert.Equal(100, loadedMessages.Count);
        Assert.Equal("System0", loadedMessages[0].SourceSystem);
        Assert.Equal("System99", loadedMessages[99].SourceSystem);
    }

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var subDir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid()}");
        var filePath = Path.Combine(subDir, "test.json");

        try
        {
            // Act
            var service = new PersistenceService(filePath);
            service.SaveUnprocessedMessages(new List<Message> { new Message("Test", "Test") });

            // Assert
            Assert.True(Directory.Exists(subDir));
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(subDir))
            {
                Directory.Delete(subDir, true);
            }
        }
    }
}
