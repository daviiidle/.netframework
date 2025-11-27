namespace Models;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

public class RabbitMQQueue : IMessageQueue, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
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

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        // Declare both main queue and DLQ
        _channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null).GetAwaiter().GetResult();

        _channel.QueueDeclareAsync(
            queue: _dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null).GetAwaiter().GetResult();
    }

    public void Enqueue(Message message)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = message.MessageId.ToString(),
            Timestamp = new AmqpTimestamp(((DateTimeOffset)message.Timestamp).ToUnixTimeSeconds())
        };

        _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: false,
            basicProperties: properties,
            body: body).GetAwaiter().GetResult();
    }

    public Message? Dequeue()
    {
        var result = _channel.BasicGetAsync(_queueName, autoAck: true).GetAwaiter().GetResult();

        if (result == null)
            return null;

        var json = Encoding.UTF8.GetString(result.Body.ToArray());
        return JsonSerializer.Deserialize<Message>(json);
    }

    public int GetQueueDepth()
    {
        var queueDeclareResult = _channel.QueueDeclarePassiveAsync(_queueName).GetAwaiter().GetResult();
        return (int)queueDeclareResult.MessageCount;
    }

    public void EnqueueToDLQ(Message message)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = message.MessageId.ToString(),
            Timestamp = new AmqpTimestamp(((DateTimeOffset)message.Timestamp).ToUnixTimeSeconds())
        };

        _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _dlqName,
            mandatory: false,
            basicProperties: properties,
            body: body).GetAwaiter().GetResult();
    }

    public Message? DequeueDLQ()
    {
        var result = _channel.BasicGetAsync(_dlqName, autoAck: true).GetAwaiter().GetResult();

        if (result == null)
            return null;

        var json = Encoding.UTF8.GetString(result.Body.ToArray());
        return JsonSerializer.Deserialize<Message>(json);
    }

    public int GetDLQDepth()
    {
        var queueDeclareResult = _channel.QueueDeclarePassiveAsync(_dlqName).GetAwaiter().GetResult();
        return (int)queueDeclareResult.MessageCount;
    }

    public void Dispose()
    {
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _channel?.Dispose();
        _connection?.CloseAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
    }
}
