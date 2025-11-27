namespace Models;

using Microsoft.Data.Sqlite;

public class DatabaseService
{
    private readonly SqliteConnection _connection;

    public DatabaseService(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        CreateTableIfNotExists();
    }

    public DatabaseService(string connectionString)
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
            CREATE TABLE IF NOT EXISTS ProcessedMessages (
                MessageId TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                SourceSystem TEXT NOT NULL,
                Payload TEXT NOT NULL,
                Status INTEGER NOT NULL,
                ProcessedAt TEXT NOT NULL
            )";

        using var command = _connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    public virtual void SaveMessage(ProcessedMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var insertSql = @"
            INSERT INTO ProcessedMessages (MessageId, Timestamp, SourceSystem, Payload, Status, ProcessedAt)
            VALUES (@MessageId, @Timestamp, @SourceSystem, @Payload, @Status, @ProcessedAt)";

        using var command = _connection.CreateCommand();
        command.CommandText = insertSql;
        command.Parameters.AddWithValue("@MessageId", message.MessageId.ToString());
        command.Parameters.AddWithValue("@Timestamp", message.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("@SourceSystem", message.SourceSystem);
        command.Parameters.AddWithValue("@Payload", message.Payload);
        command.Parameters.AddWithValue("@Status", (int)message.Status);
        command.Parameters.AddWithValue("@ProcessedAt", message.ProcessedAt.ToString("o"));

        command.ExecuteNonQuery();
    }

    public ProcessedMessage? GetMessageById(Guid messageId)
    {
        var selectSql = @"
            SELECT MessageId, Timestamp, SourceSystem, Payload, Status, ProcessedAt
            FROM ProcessedMessages
            WHERE MessageId = @MessageId";

        using var command = _connection.CreateCommand();
        command.CommandText = selectSql;
        command.Parameters.AddWithValue("@MessageId", messageId.ToString());

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new ProcessedMessage
            {
                MessageId = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTime.Parse(reader.GetString(1)).ToUniversalTime(),
                SourceSystem = reader.GetString(2),
                Payload = reader.GetString(3),
                Status = (MessageStatus)reader.GetInt32(4),
                ProcessedAt = DateTime.Parse(reader.GetString(5)).ToUniversalTime()
            };
        }

        return null;
    }

    public List<ProcessedMessage> GetAllMessages()
    {
        var messages = new List<ProcessedMessage>();
        var selectSql = @"
            SELECT MessageId, Timestamp, SourceSystem, Payload, Status, ProcessedAt
            FROM ProcessedMessages
            ORDER BY ProcessedAt DESC";

        using var command = _connection.CreateCommand();
        command.CommandText = selectSql;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new ProcessedMessage
            {
                MessageId = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTime.Parse(reader.GetString(1)).ToUniversalTime(),
                SourceSystem = reader.GetString(2),
                Payload = reader.GetString(3),
                Status = (MessageStatus)reader.GetInt32(4),
                ProcessedAt = DateTime.Parse(reader.GetString(5)).ToUniversalTime()
            });
        }

        return messages;
    }

    public void Close()
    {
        _connection?.Close();
    }
}
