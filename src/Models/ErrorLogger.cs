namespace Models;

public class ErrorLogger
{
    private readonly string _logFilePath;
    private readonly object _lockObject = new object();

    public ErrorLogger(string logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
            throw new ArgumentException("Log file path cannot be null or empty.", nameof(logFilePath));

        _logFilePath = logFilePath;

        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void LogError(Guid messageId, Exception exception, int attemptNumber)
    {
        var logEntry = FormatLogEntry(messageId, exception, attemptNumber);

        // Thread-safe file writing
        lock (_lockObject)
        {
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine + Environment.NewLine);
        }
    }

    private string FormatLogEntry(Guid messageId, Exception? exception, int attemptNumber)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var exceptionType = exception?.GetType().Name ?? "Unknown";
        var exceptionMessage = exception?.Message ?? "No exception details";
        var stackTrace = exception?.StackTrace ?? "No stack trace available";

        return $@"[{timestamp}]
Message ID: {messageId}
Attempt: {attemptNumber}
Exception Type: {exceptionType}
Error Message: {exceptionMessage}
Stack Trace:
{stackTrace}
{new string('-', 80)}";
    }
}
