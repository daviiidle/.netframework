namespace Models;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

public class RabbitMQQueue : IMessageQueue, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName;
    private readonly string _dlqName;

    public RabbitMQQueue(string hostname, string queueName, int port = 5672, string username = "guest", string password = "guest")
    {
        _queueName = queueName;
        _dlqName = $"{queueName}-dlq";

        var factory = new ConnectionFactory
        {
            HostName = hostname,
            Port = port,
            UserName = username,
            Password = password
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Enable publisher confirms for reliable message delivery
        _channel.ConfirmSelect();

        // Declare both main queue and DLQ
        _channel.QueueDeclare(
            queue: _queueName,
            durable: false,  // Changed to false for testing
            exclusive: false,
            autoDelete: true,  // Changed to true for automatic cleanup
            arguments: null);

        _channel.QueueDeclare(
            queue: _dlqName,
            durable: false,  // Changed to false for testing
            exclusive: false,
            autoDelete: true,  // Changed to true for automatic cleanup
            arguments: null);
    }

    public void Enqueue(Message message)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.MessageId = message.MessageId.ToString();
        properties.Timestamp = new AmqpTimestamp(((DateTimeOffset)message.Timestamp).ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _queueName,
            basicProperties: properties,
            body: body);

        // Wait for confirmation that message was received by broker
        _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
    }

    public Message? Dequeue()
    {
        var result = _channel.BasicGet(_queueName, autoAck: true);

        if (result == null)
            return null;

        var json = Encoding.UTF8.GetString(result.Body.ToArray());
        return JsonSerializer.Deserialize<Message>(json);
    }

    public int GetQueueDepth()
    {
        var queueDeclareResult = _channel.QueueDeclarePassive(_queueName);
        return (int)queueDeclareResult.MessageCount;
    }

    public void EnqueueToDLQ(Message message)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.MessageId = message.MessageId.ToString();
        properties.Timestamp = new AmqpTimestamp(((DateTimeOffset)message.Timestamp).ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _dlqName,
            basicProperties: properties,
            body: body);

        // Wait for confirmation that message was received by broker
        _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
    }

    public Message? DequeueDLQ()
    {
        var result = _channel.BasicGet(_dlqName, autoAck: true);

        if (result == null)
            return null;

        var json = Encoding.UTF8.GetString(result.Body.ToArray());
        return JsonSerializer.Deserialize<Message>(json);
    }

    public int GetDLQDepth()
    {
        var queueDeclareResult = _channel.QueueDeclarePassive(_dlqName);
        return (int)queueDeclareResult.MessageCount;
    }

    public void PurgeQueue()
    {
        _channel.QueuePurge(_queueName);
    }

    public void PurgeDLQ()
    {
        _channel.QueuePurge(_dlqName);
    }

    public void DeleteQueues()
    {
        try
        {
            _channel.QueueDelete(_queueName);
            _channel.QueueDelete(_dlqName);
        }
        catch
        {
            // Ignore errors if queues don't exist
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
