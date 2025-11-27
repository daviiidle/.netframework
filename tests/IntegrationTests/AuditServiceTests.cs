namespace IntegrationTests;

using Models;
using Microsoft.Data.Sqlite;
using Xunit;

public class AuditServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AuditService _auditService;

    public AuditServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _auditService = new AuditService(_connection);
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Fact]
    public void LogProcessingStart_CreatesRecord()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        // Act
        _auditService.LogProcessingStart(messageId);

        // Assert
        var audit = _auditService.GetAuditLog(messageId);
        Assert.NotNull(audit);
        Assert.Equal(messageId, audit.MessageId);
        Assert.Equal("Processing", audit.Status);
        Assert.True(audit.StartTime > DateTime.MinValue);
        Assert.Null(audit.EndTime);
        Assert.Null(audit.DurationMs);
    }

    [Fact]
    public void LogProcessingEnd_UpdatesWithDuration()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        _auditService.LogProcessingStart(messageId);

        // Wait a bit to ensure duration > 0
        Thread.Sleep(10);

        // Act
        _auditService.LogProcessingEnd(messageId, success: true);

        // Assert
        var audit = _auditService.GetAuditLog(messageId);
        Assert.NotNull(audit);
        Assert.Equal("Completed", audit.Status);
        Assert.NotNull(audit.EndTime);
        Assert.NotNull(audit.DurationMs);
        Assert.True(audit.DurationMs > 0);
        Assert.Null(audit.ErrorMessage);
    }

    [Fact]
    public void LogProcessingEnd_Failure_SetsFailedStatus()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        _auditService.LogProcessingStart(messageId);

        // Act
        _auditService.LogProcessingEnd(messageId, success: false, errorMessage: "Test error");

        // Assert
        var audit = _auditService.GetAuditLog(messageId);
        Assert.NotNull(audit);
        Assert.Equal("Failed", audit.Status);
        Assert.Equal("Test error", audit.ErrorMessage);
    }

    [Fact]
    public void GetStatistics_CalculatesMetrics()
    {
        // Arrange
        var msg1 = Guid.NewGuid();
        var msg2 = Guid.NewGuid();
        var msg3 = Guid.NewGuid();

        _auditService.LogProcessingStart(msg1);
        Thread.Sleep(5);
        _auditService.LogProcessingEnd(msg1, success: true);

        _auditService.LogProcessingStart(msg2);
        Thread.Sleep(10);
        _auditService.LogProcessingEnd(msg2, success: true);

        _auditService.LogProcessingStart(msg3);
        Thread.Sleep(5);
        _auditService.LogProcessingEnd(msg3, success: false);

        // Act
        var stats = _auditService.GetStatistics();

        // Assert
        Assert.Equal(3, stats.TotalProcessed);
        Assert.Equal(2, stats.SuccessCount);
        Assert.Equal(1, stats.FailureCount);
        Assert.True(stats.AverageDurationMs > 0);
        Assert.True(stats.MinDurationMs > 0);
        Assert.True(stats.MaxDurationMs > 0);
        Assert.True(stats.MaxDurationMs >= stats.MinDurationMs);
        Assert.InRange(stats.SuccessRate, 66.0, 67.0); // 2/3 * 100 ≈ 66.67%
    }

    [Fact]
    public void GetStatistics_EmptyDatabase_ReturnsZeros()
    {
        // Act
        var stats = _auditService.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalProcessed);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(0, stats.FailureCount);
        Assert.Equal(0, stats.AverageDurationMs);
        Assert.Equal(0, stats.SuccessRate);
    }

    [Fact]
    public void GetAuditLog_NonExistent_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var audit = _auditService.GetAuditLog(nonExistentId);

        // Assert
        Assert.Null(audit);
    }

    [Fact]
    public void GetAllAuditLogs_ReturnsAll()
    {
        // Arrange
        var msg1 = Guid.NewGuid();
        var msg2 = Guid.NewGuid();

        _auditService.LogProcessingStart(msg1);
        _auditService.LogProcessingEnd(msg1, success: true);

        _auditService.LogProcessingStart(msg2);
        _auditService.LogProcessingEnd(msg2, success: false);

        // Act
        var logs = _auditService.GetAllAuditLogs();

        // Assert
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.MessageId == msg1 && l.Status == "Completed");
        Assert.Contains(logs, l => l.MessageId == msg2 && l.Status == "Failed");
    }

    [Fact]
    public void LogProcessingEnd_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        // Act & Assert - Should not throw
        _auditService.LogProcessingEnd(messageId, success: true);

        // Verify no record was created/updated
        var audit = _auditService.GetAuditLog(messageId);
        Assert.Null(audit);
    }

    [Fact]
    public void MultipleMessages_TrackedIndependently()
    {
        // Arrange
        var msg1 = Guid.NewGuid();
        var msg2 = Guid.NewGuid();

        // Act
        _auditService.LogProcessingStart(msg1);
        _auditService.LogProcessingStart(msg2);

        Thread.Sleep(5);
        _auditService.LogProcessingEnd(msg1, success: true);

        Thread.Sleep(5);
        _auditService.LogProcessingEnd(msg2, success: false);

        // Assert
        var audit1 = _auditService.GetAuditLog(msg1);
        var audit2 = _auditService.GetAuditLog(msg2);

        Assert.NotNull(audit1);
        Assert.NotNull(audit2);
        Assert.Equal("Completed", audit1.Status);
        Assert.Equal("Failed", audit2.Status);
        Assert.NotEqual(audit1.DurationMs, audit2.DurationMs);
    }

    [Fact]
    public void GetStatistics_OnlyCompletedMessages_IncludedInDuration()
    {
        // Arrange
        var msg1 = Guid.NewGuid();
        var msg2 = Guid.NewGuid();

        _auditService.LogProcessingStart(msg1);
        Thread.Sleep(10);
        _auditService.LogProcessingEnd(msg1, success: true);

        _auditService.LogProcessingStart(msg2);
        // Don't complete msg2

        // Act
        var stats = _auditService.GetStatistics();

        // Assert
        Assert.Equal(1, stats.TotalProcessed); // Only completed messages counted
        Assert.True(stats.AverageDurationMs > 0);
    }
}
