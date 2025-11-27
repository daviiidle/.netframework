namespace Models;

public enum CircuitState
{
    Closed,      // Normal operation, requests allowed
    Open,        // Circuit is open, requests blocked
    HalfOpen     // Testing if service recovered, limited requests allowed
}
