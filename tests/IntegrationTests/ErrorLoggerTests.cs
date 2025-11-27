namespace IntegrationTests;

using Models;
using Xunit;

public class ErrorLoggerTests : IDisposable
{
    private readonly string _testLogFile;
    private readonly ErrorLogger _logger;

    public ErrorLoggerTests()
    {
        _testLogFile = Path.Combine(Path.GetTempPath(), $"test_errors_{Guid.NewGuid()}.log");
        _logger = new ErrorLogger(_testLogFile);
    }

    public void Dispose()
    {
        if (File.Exists(_testLogFile))
        {
            try
            {
                File.Delete(_testLogFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void LogError_CreatesLogFile()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var exception = new Exception("Test error");

        // Act
        _logger.LogError(messageId, exception, 1);

        // Assert
        Assert.True(File.Exists(_testLogFile));
    }

    [Fact]
    public void LogError_WritesMessageId()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var exception = new Exception("Test error");

        // Act
        _logger.LogError(messageId, exception, 1);

        // Assert
        var content = File.ReadAllText(_testLogFile);
        Assert.Contains(messageId.ToString(), content);
    }

    [Fact]
    public void LogError_WritesAttemptNumber()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var exception = new Exception("Test error");

        // Act
        _logger.LogError(messageId, exception, 3);

        // Assert
        var content = File.ReadAllText(_testLogFile);
        Assert.Contains("Attempt: 3", content);
    }

    [Fact]
    public void LogError_WritesExceptionMessage()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var exception = new Exception("Custom error message");

        // Act
        _logger.LogError(messageId, exception, 1);

        // Assert
        var content = File.ReadAllText(_testLogFile);
        Assert.Contains("Custom error message", content);
    }

    [Fact]
    public void LogError_WritesTimestamp()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var exception = new Exception("Test error");
        var before = DateTime.UtcNow;

        // Act
        _logger.LogError(messageId, exception, 1);
        var after = DateTime.UtcNow;

        // Assert
        var content = File.ReadAllText(_testLogFile);
        // Check that content contains a date/time string
        Assert.Matches(@"\d{4}-\d{2}-\d{2}", content);
    }

    [Fact]
    public void LogError_WritesStackTrace()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        Exception exception;
        try
        {
            throw new InvalidOperationException("Test exception with stack");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        _logger.LogError(messageId, exception, 1);

        // Assert
        var content = File.ReadAllText(_testLogFile);
        Assert.Contains("Stack Trace:", content);
        Assert.Contains("InvalidOperationException", content);
    }

    [Fact]
    public void LogError_MultipleErrors_AppendsToFile()
    {
        // Arrange
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();
        var exception1 = new Exception("First error");
        var exception2 = new Exception("Second error");

        // Act
        _logger.LogError(messageId1, exception1, 1);
        _logger.LogError(messageId2, exception2, 2);

        // Assert
        var content = File.ReadAllText(_testLogFile);
        Assert.Contains(messageId1.ToString(), content);
        Assert.Contains(messageId2.ToString(), content);
        Assert.Contains("First error", content);
        Assert.Contains("Second error", content);
        Assert.Contains("Attempt: 1", content);
        Assert.Contains("Attempt: 2", content);
    }

    [Fact]
    public void LogError_NullException_DoesNotThrow()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        // Act & Assert - Should not throw
        _logger.LogError(messageId, null!, 1);

        // Verify file was created
        Assert.True(File.Exists(_testLogFile));
    }

    [Fact]
    public void LogError_IncludesExceptionType()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var exception = new InvalidOperationException("Test error");

        // Act
        _logger.LogError(messageId, exception, 1);

        // Assert
        var content = File.ReadAllText(_testLogFile);
        Assert.Contains("InvalidOperationException", content);
    }

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var subDir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid()}");
        var logFile = Path.Combine(subDir, "test.log");

        try
        {
            // Act
            var logger = new ErrorLogger(logFile);
            logger.LogError(Guid.NewGuid(), new Exception("Test"), 1);

            // Assert
            Assert.True(Directory.Exists(subDir));
            Assert.True(File.Exists(logFile));
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
