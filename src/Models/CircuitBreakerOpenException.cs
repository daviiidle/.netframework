namespace Models;

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException()
        : base("Circuit breaker is open. Service is temporarily unavailable.")
    {
    }

    public CircuitBreakerOpenException(string message)
        : base(message)
    {
    }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
