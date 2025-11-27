namespace Models;

using Microsoft.Data.Sqlite;

public class AuditService
{
    private readonly SqliteConnection _connection;

    public AuditService(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        CreateTableIfNotExists();
    }

    public AuditService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        CreateTableIfNotExists();
    }

    private void CreateTableIfNotExists()
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MessageId TEXT NOT NULL UNIQUE,
                StartTime TEXT NOT NULL,
                EndTime TEXT,
                DurationMs REAL,
                Status TEXT NOT NULL,
                ErrorMessage TEXT
            )";

        using var command = _connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    public void LogProcessingStart(Guid messageId)
    {
        var insertSql = @"
            INSERT INTO AuditLogs (MessageId, StartTime, Status)
            VALUES (@MessageId, @StartTime, @Status)";

        using var command = _connection.CreateCommand();
        command.CommandText = insertSql;
        command.Parameters.AddWithValue("@MessageId", messageId.ToString());
        command.Parameters.AddWithValue("@StartTime", DateTime.UtcNow.ToString("o"));
        command.Parameters.AddWithValue("@Status", "Processing");

        command.ExecuteNonQuery();
    }

    public void LogProcessingEnd(Guid messageId, bool success, string? errorMessage = null)
    {
        // First check if record exists
        var existsSql = "SELECT COUNT(*) FROM AuditLogs WHERE MessageId = @MessageId";
        using (var checkCommand = _connection.CreateCommand())
        {
            checkCommand.CommandText = existsSql;
            checkCommand.Parameters.AddWithValue("@MessageId", messageId.ToString());
            var count = Convert.ToInt32(checkCommand.ExecuteScalar());
            if (count == 0)
                return; // Record doesn't exist, nothing to update
        }

        // Get start time to calculate duration
        var getStartSql = "SELECT StartTime FROM AuditLogs WHERE MessageId = @MessageId";
        DateTime startTime;
        using (var getCommand = _connection.CreateCommand())
        {
            getCommand.CommandText = getStartSql;
            getCommand.Parameters.AddWithValue("@MessageId", messageId.ToString());
            var startTimeString = (string)getCommand.ExecuteScalar()!;
            startTime = DateTime.Parse(startTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        var endTime = DateTime.UtcNow;
        var duration = (endTime - startTime).TotalMilliseconds;

        var updateSql = @"
            UPDATE AuditLogs
            SET EndTime = @EndTime,
                DurationMs = @DurationMs,
                Status = @Status,
                ErrorMessage = @ErrorMessage
            WHERE MessageId = @MessageId";

        using var command = _connection.CreateCommand();
        command.CommandText = updateSql;
        command.Parameters.AddWithValue("@MessageId", messageId.ToString());
        command.Parameters.AddWithValue("@EndTime", endTime.ToString("o"));
        command.Parameters.AddWithValue("@DurationMs", duration);
        command.Parameters.AddWithValue("@Status", success ? "Completed" : "Failed");
        command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
    }

    public AuditLog? GetAuditLog(Guid messageId)
    {
        var selectSql = @"
            SELECT Id, MessageId, StartTime, EndTime, DurationMs, Status, ErrorMessage
            FROM AuditLogs
            WHERE MessageId = @MessageId";

        using var command = _connection.CreateCommand();
        command.CommandText = selectSql;
        command.Parameters.AddWithValue("@MessageId", messageId.ToString());

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new AuditLog
            {
                Id = reader.GetInt32(0),
                MessageId = Guid.Parse(reader.GetString(1)),
                StartTime = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                EndTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DurationMs = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                Status = reader.GetString(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6)
            };
        }

        return null;
    }

    public List<AuditLog> GetAllAuditLogs()
    {
        var logs = new List<AuditLog>();
        var selectSql = @"
            SELECT Id, MessageId, StartTime, EndTime, DurationMs, Status, ErrorMessage
            FROM AuditLogs
            ORDER BY StartTime DESC";

        using var command = _connection.CreateCommand();
        command.CommandText = selectSql;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(new AuditLog
            {
                Id = reader.GetInt32(0),
                MessageId = Guid.Parse(reader.GetString(1)),
                StartTime = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                EndTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DurationMs = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                Status = reader.GetString(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return logs;
    }

    public AuditStatistics GetStatistics()
    {
        var statsSql = @"
            SELECT
                COUNT(*) as TotalProcessed,
                SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) as SuccessCount,
                SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) as FailureCount,
                AVG(DurationMs) as AverageDurationMs,
                MIN(DurationMs) as MinDurationMs,
                MAX(DurationMs) as MaxDurationMs
            FROM AuditLogs
            WHERE DurationMs IS NOT NULL";

        using var command = _connection.CreateCommand();
        command.CommandText = statsSql;

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new AuditStatistics
            {
                TotalProcessed = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                SuccessCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                FailureCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                AverageDurationMs = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                MinDurationMs = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                MaxDurationMs = reader.IsDBNull(5) ? 0 : reader.GetDouble(5)
            };
        }

        return new AuditStatistics();
    }

    public void Close()
    {
        _connection?.Close();
    }
}
