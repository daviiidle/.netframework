namespace Models;

public class RetryPolicy
{
    private readonly int _maxRetries;
    public Action<int, TimeSpan>? OnRetry { get; set; }

    public RetryPolicy(int maxRetries)
    {
        if (maxRetries < 0)
            throw new ArgumentException("Max retries cannot be negative.", nameof(maxRetries));

        _maxRetries = maxRetries;
    }

    public T Execute<T>(Func<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Don't retry if we've exhausted all retries
                if (attempt >= _maxRetries)
                    throw;

                // Calculate exponential backoff: 1s, 2s, 4s, 8s, etc.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                // Notify via callback if set
                OnRetry?.Invoke(attempt + 1, delay);

                // Wait before retrying
                Thread.Sleep(delay);
            }
        }

        // Should never reach here, but if it does, throw the last exception
        throw lastException ?? new InvalidOperationException("Retry policy failed unexpectedly.");
    }

    public void Execute(Action action)
    {
        Execute(() =>
        {
            action();
            return true;
        });
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Don't retry if we've exhausted all retries
                if (attempt >= _maxRetries)
                    throw;

                // Calculate exponential backoff: 1s, 2s, 4s, 8s, etc.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                // Notify via callback if set
                OnRetry?.Invoke(attempt + 1, delay);

                // Wait before retrying
                await Task.Delay(delay);
            }
        }

        // Should never reach here, but if it does, throw the last exception
        throw lastException ?? new InvalidOperationException("Retry policy failed unexpectedly.");
    }

    public async Task ExecuteAsync(Func<Task> action)
    {
        await ExecuteAsync(async () =>
        {
            await action();
            return true;
        });
    }
}
