namespace Models;

public class ValidationException : Exception
{
    public ValidationException()
        : base("Invalid message: validation failed.")
    {
    }

    public ValidationException(string message)
        : base(message)
    {
    }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
