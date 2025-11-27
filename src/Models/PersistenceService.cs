namespace Models;

using System.Text.Json;

public class PersistenceService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public PersistenceService(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        _filePath = filePath;

        // Configure JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Saves unprocessed messages to a JSON file.
    /// </summary>
    /// <param name="messages">List of messages to save</param>
    public void SaveUnprocessedMessages(List<Message> messages)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var json = JsonSerializer.Serialize(messages, _jsonOptions);
        File.WriteAllText(_filePath, json);
    }

    /// <summary>
    /// Loads unprocessed messages from a JSON file.
    /// </summary>
    /// <returns>List of messages, or empty list if file doesn't exist</returns>
    public List<Message> LoadUnprocessedMessages()
    {
        if (!File.Exists(_filePath))
            return new List<Message>();

        try
        {
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<Message>();

            var messages = JsonSerializer.Deserialize<List<Message>>(json, _jsonOptions);
            return messages ?? new List<Message>();
        }
        catch (JsonException)
        {
            // If JSON is invalid, return empty list
            return new List<Message>();
        }
    }
}
