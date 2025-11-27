namespace Models;

public class IntegrationWorker
{
    private readonly IMessageQueue _queue;
    private readonly MessageTransformer _transformer;
    private readonly DatabaseService _databaseService;
    private readonly RetryPolicy? _retryPolicy;
    private readonly ErrorLogger? _errorLogger;
    private readonly CircuitBreaker? _circuitBreaker;
    private readonly AuditService? _auditService;

    // Constructor with retry, error logging, circuit breaker, and audit support
    public IntegrationWorker(
        IMessageQueue queue,
        MessageTransformer transformer,
        DatabaseService databaseService,
        RetryPolicy? retryPolicy = null,
        ErrorLogger? errorLogger = null,
        CircuitBreaker? circuitBreaker = null,
        AuditService? auditService = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _retryPolicy = retryPolicy;
        _errorLogger = errorLogger;
        _circuitBreaker = circuitBreaker;
        _auditService = auditService;
    }

    /// <summary>
    /// Processes a single message from the queue: dequeue, transform, and save to database.
    /// Includes retry logic and error handling.
    /// </summary>
    /// <returns>True if a message was processed successfully, false if queue was empty or processing failed</returns>
    public bool ProcessMessage()
    {
        // Dequeue message
        var message = _queue.Dequeue();
        if (message == null)
            return false;

        // Validate message before processing
        if (!message.IsValid())
        {
            // Validation failures go directly to DLQ (no retry)
            message.Status = MessageStatus.Failed;
            _queue.EnqueueToDLQ(message);
            _auditService?.LogProcessingStart(message.MessageId);
            _auditService?.LogProcessingEnd(message.MessageId, success: false, errorMessage: "Validation failed");
            return false;
        }

        // Log processing start
        _auditService?.LogProcessingStart(message.MessageId);

        try
        {
            // Transform message
            var processedMessage = _transformer.Transform(message);

            // Save to database with circuit breaker and retry protection
            if (_circuitBreaker != null)
            {
                // Use circuit breaker to protect database
                _circuitBreaker.Execute(() =>
                {
                    SaveWithRetry(processedMessage, message);
                });
            }
            else
            {
                // No circuit breaker - use retry directly
                SaveWithRetry(processedMessage, message);
            }

            // Log successful processing
            _auditService?.LogProcessingEnd(message.MessageId, success: true);
            return true;
        }
        catch (CircuitBreakerOpenException ex)
        {
            // Circuit is open - send directly to DLQ without retrying
            message.Status = MessageStatus.Failed;
            _queue.EnqueueToDLQ(message);
            _errorLogger?.LogError(message.MessageId, ex, 0);
            _auditService?.LogProcessingEnd(message.MessageId, success: false, errorMessage: "Circuit breaker open");
            return false;
        }
        catch (Exception ex)
        {
            // Max retries exceeded or non-retryable error - send to DLQ
            message.Status = MessageStatus.Failed;
            _queue.EnqueueToDLQ(message);
            _errorLogger?.LogError(message.MessageId, ex, 0);
            _auditService?.LogProcessingEnd(message.MessageId, success: false, errorMessage: ex.Message);
            return false;
        }
    }

    private void SaveWithRetry(ProcessedMessage processedMessage, Message originalMessage)
    {
        // Try to save with retry logic if available
        if (_retryPolicy != null)
        {
            int currentAttempt = 0;

            _retryPolicy.OnRetry = (attempt, delay) =>
            {
                // Log retry attempts (this is called BEFORE the retry)
                _errorLogger?.LogError(originalMessage.MessageId,
                    new Exception($"Retry attempt {attempt} scheduled after delay {delay}"),
                    attempt);
            };

            _retryPolicy.Execute(() =>
            {
                currentAttempt++;
                try
                {
                    // Update status to Completed and save
                    processedMessage.Status = MessageStatus.Completed;
                    _databaseService.SaveMessage(processedMessage);
                }
                catch (Exception ex)
                {
                    _errorLogger?.LogError(originalMessage.MessageId, ex, currentAttempt);
                    throw;
                }
            });
        }
        else
        {
            // No retry policy - simple save
            processedMessage.Status = MessageStatus.Completed;
            _databaseService.SaveMessage(processedMessage);
        }
    }

    /// <summary>
    /// Processes multiple messages from the queue up to the specified maximum count.
    /// </summary>
    /// <param name="maxCount">Maximum number of messages to process</param>
    /// <returns>Number of messages actually processed successfully</returns>
    public int ProcessMessages(int maxCount)
    {
        if (maxCount <= 0)
            throw new ArgumentException("Max count must be greater than zero.", nameof(maxCount));

        int processedCount = 0;
        for (int i = 0; i < maxCount; i++)
        {
            // Check if queue is empty before processing
            if (_queue.GetQueueDepth() == 0)
                break;

            if (ProcessMessage())
                processedCount++;
        }

        return processedCount;
    }

    /// <summary>
    /// Processes all messages currently in the queue.
    /// </summary>
    /// <returns>Number of messages processed successfully</returns>
    public int ProcessAllMessages()
    {
        int processedCount = 0;
        int queueDepth = _queue.GetQueueDepth();

        for (int i = 0; i < queueDepth; i++)
        {
            if (ProcessMessage())
                processedCount++;
        }

        return processedCount;
    }
}
