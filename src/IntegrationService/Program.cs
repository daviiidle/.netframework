using Models;
using Microsoft.Data.Sqlite;

Console.WriteLine("Integration Service starting...");
Console.WriteLine();

// Parse command-line arguments
var useRabbitMQ = args.Contains("--rabbitmq");

// Create appropriate queue based on flag
IMessageQueue queue;
if (useRabbitMQ)
{
    Console.WriteLine("Using RabbitMQ queue");
    try
    {
        queue = new RabbitMQQueue("localhost", "government-framework-queue");
        Console.WriteLine("Connected to RabbitMQ at localhost:5672");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to connect to RabbitMQ: {ex.Message}");
        Console.WriteLine("Make sure RabbitMQ is running: docker compose up -d");
        return;
    }
}
else
{
    Console.WriteLine("Using In-Memory queue");
    queue = new InMemoryQueue();
}

Console.WriteLine();

// Create SQLite connection for audit service
var auditConnection = new SqliteConnection("Data Source=audit.db");
auditConnection.Open();
var auditService = new AuditService(auditConnection);

// Create services
var transformer = new MessageTransformer();
var databaseService = new DatabaseService(new SqliteConnection("Data Source=messages.db"));
var retryPolicy = new RetryPolicy(maxAttempts: 3, delayMs: 100);
var errorLogger = new ErrorLogger("errors.log");
var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeoutMs: 5000);

// Create worker
var worker = new IntegrationWorker(
    queue,
    transformer,
    databaseService,
    retryPolicy,
    errorLogger,
    circuitBreaker,
    auditService);

Console.WriteLine("Processing queued messages...");
Console.WriteLine("=".PadRight(50, '='));

// Check initial queue depth
var initialDepth = queue.GetQueueDepth();
Console.WriteLine($"Messages in queue: {initialDepth}");
Console.WriteLine();

// Process all messages in the queue
int processedCount = 0;
int failedCount = 0;

while (queue.GetQueueDepth() > 0)
{
    try
    {
        var success = worker.ProcessMessage();
        if (success)
        {
            processedCount++;
            Console.WriteLine($"✓ Processed message {processedCount}");
        }
        else
        {
            failedCount++;
            Console.WriteLine($"✗ Failed to process message {failedCount}");
        }
    }
    catch (Exception ex)
    {
        failedCount++;
        Console.WriteLine($"✗ Error processing message: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine($"Processing complete!");
Console.WriteLine($"  Processed: {processedCount} message(s)");
Console.WriteLine($"  Failed: {failedCount} message(s)");
Console.WriteLine($"  Remaining in queue: {queue.GetQueueDepth()}");
Console.WriteLine($"  Dead letter queue: {queue.GetDLQDepth()} message(s)");

Console.WriteLine();
Console.WriteLine("Audit Statistics:");
Console.WriteLine("-".PadRight(50, '-'));

// Display statistics from audit service
var stats = auditService.GetStatistics();
Console.WriteLine($"  Total processed: {stats.TotalProcessed}");
Console.WriteLine($"  Success count: {stats.SuccessCount}");
Console.WriteLine($"  Failure count: {stats.FailureCount}");
Console.WriteLine($"  Success rate: {stats.SuccessRate:F2}%");

if (stats.TotalProcessed > 0)
{
    Console.WriteLine($"  Avg duration: {stats.AverageDurationMs:F2} ms");
    Console.WriteLine($"  Min duration: {stats.MinDurationMs:F2} ms");
    Console.WriteLine($"  Max duration: {stats.MaxDurationMs:F2} ms");
}

Console.WriteLine();

// Show recent audit logs
Console.WriteLine("Recent Audit Logs:");
Console.WriteLine("-".PadRight(50, '-'));
var auditLogs = auditService.GetAllAuditLogs();
foreach (var log in auditLogs.Take(5))
{
    var duration = log.DurationMs.HasValue ? $"{log.DurationMs.Value:F2}ms" : "N/A";
    Console.WriteLine($"  [{log.Status}] {log.MessageId} - {duration}");
    if (!string.IsNullOrEmpty(log.ErrorMessage))
    {
        Console.WriteLine($"    Error: {log.ErrorMessage}");
    }
}

Console.WriteLine();
Console.WriteLine("Integration Service completed successfully!");

// Clean up resources
auditConnection.Close();
auditConnection.Dispose();
databaseService.Close();

if (queue is IDisposable disposable)
{
    disposable.Dispose();
}
