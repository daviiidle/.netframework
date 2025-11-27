namespace IntegrationTests;

using Models;
using Microsoft.Data.Sqlite;

/// <summary>
/// Test helper that wraps a DatabaseService and simulates failures for testing retry logic.
/// </summary>
public class FailingDatabaseService : DatabaseService
{
    private int _failuresRemaining;
    private readonly Exception _exceptionToThrow;

    public int SaveMessageCallCount { get; private set; }

    public FailingDatabaseService(SqliteConnection connection, int failureCount, Exception? exceptionToThrow = null)
        : base(connection)
    {
        _failuresRemaining = failureCount;
        _exceptionToThrow = exceptionToThrow ?? new InvalidOperationException("Simulated transient database failure");
    }

    public override void SaveMessage(ProcessedMessage message)
    {
        SaveMessageCallCount++;

        if (_failuresRemaining > 0)
        {
            _failuresRemaining--;
            throw _exceptionToThrow;
        }

        // Success - call the base implementation
        base.SaveMessage(message);
    }
}
