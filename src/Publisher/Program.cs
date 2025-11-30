using Models;

Console.WriteLine("Publisher starting...");
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
Console.WriteLine("Publishing messages...");
Console.WriteLine("=".PadRight(50, '='));

// Publish 5 valid messages
for (int i = 1; i <= 5; i++)
{
    var message = new Message($"System{i}", $"Valid payload {i}");
    queue.Enqueue(message);
    Console.WriteLine($"✓ Published message {i}: {message.MessageId} from {message.SourceSystem}");
}

Console.WriteLine();

// Try publishing an invalid message
Console.WriteLine("Attempting to publish invalid message...");
try
{
    var invalidMessage = new Message("", "");  // Invalid: empty source and payload
    if (!invalidMessage.IsValid())
    {
        Console.WriteLine("✗ Message validation failed: Source and Payload cannot be empty");
    }
    else
    {
        queue.Enqueue(invalidMessage);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Failed to publish invalid message: {ex.Message}");
}

Console.WriteLine();

// Try publishing a duplicate message
Console.WriteLine("Attempting to publish duplicate message...");
try
{
    var duplicateMessage = new Message("DuplicateSystem", "Duplicate payload")
    {
        MessageId = Guid.Parse("00000000-0000-0000-0000-000000000001")
    };

    queue.Enqueue(duplicateMessage);
    Console.WriteLine($"✓ Published message: {duplicateMessage.MessageId}");

    // Try to publish the same message again
    var duplicateMessage2 = new Message("DuplicateSystem", "Duplicate payload")
    {
        MessageId = Guid.Parse("00000000-0000-0000-0000-000000000001")
    };

    queue.Enqueue(duplicateMessage2);
    Console.WriteLine($"✓ Published duplicate (this shouldn't happen!)");
}
catch (DuplicateMessageException ex)
{
    Console.WriteLine($"✗ Duplicate message rejected: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=".PadRight(50, '='));

// Show queue depth
var queueDepth = queue.GetQueueDepth();
var dlqDepth = queue.GetDLQDepth();

Console.WriteLine($"Queue depth: {queueDepth} message(s)");
Console.WriteLine($"DLQ depth: {dlqDepth} message(s)");

Console.WriteLine();
Console.WriteLine("Publisher completed successfully!");

// Clean up
if (queue is IDisposable disposable)
{
    disposable.Dispose();
}
