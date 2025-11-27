namespace Models;

public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private readonly object _lockObject = new object();

    private int _failureCount;
    private CircuitState _state;
    private DateTime _lastFailureTime;

    public CircuitState State
    {
        get
        {
            lock (_lockObject)
            {
                return _state;
            }
        }
    }

    public CircuitBreaker(int failureThreshold, TimeSpan timeout)
    {
        if (failureThreshold <= 0)
            throw new ArgumentException("Failure threshold must be greater than zero.", nameof(failureThreshold));

        if (timeout < TimeSpan.Zero)
            throw new ArgumentException("Timeout cannot be negative.", nameof(timeout));

        _failureThreshold = failureThreshold;
        _timeout = timeout;
        _failureCount = 0;
        _state = CircuitState.Closed;
        _lastFailureTime = DateTime.MinValue;
    }

    public T Execute<T>(Func<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        lock (_lockObject)
        {
            // Check if we should transition from Open to HalfOpen
            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime >= _timeout)
                {
                    _state = CircuitState.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException();
                }
            }

            try
            {
                var result = action();

                // Success - reset failure count and close circuit if needed
                OnSuccess();

                return result;
            }
            catch (Exception)
            {
                // Failure - increment count and potentially open circuit
                OnFailure();
                throw;
            }
        }
    }

    public void Execute(Action action)
    {
        Execute(() =>
        {
            action();
            return true;
        });
    }

    private void OnSuccess()
    {
        _failureCount = 0;
        _state = CircuitState.Closed;
    }

    private void OnFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        if (_failureCount >= _failureThreshold)
        {
            _state = CircuitState.Open;
        }
    }

    public void Reset()
    {
        lock (_lockObject)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
            _lastFailureTime = DateTime.MinValue;
        }
    }
}
