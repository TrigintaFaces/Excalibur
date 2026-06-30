namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Thrown when a request is sent through the MediatR compatibility surface but no handler is registered
/// for it. Derives from <see cref="InvalidOperationException"/> so that code written against the MediatR
/// API — where an unregistered request surfaces as an <see cref="InvalidOperationException"/> from the
/// service provider — continues to catch it after a namespace swap.
/// </summary>
public sealed class HandlerNotFoundException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="HandlerNotFoundException"/> class.</summary>
    public HandlerNotFoundException()
        : base("No handler was registered for the request.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HandlerNotFoundException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public HandlerNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HandlerNotFoundException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public HandlerNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a <see cref="HandlerNotFoundException"/> describing the unregistered request type.
    /// </summary>
    /// <param name="requestType">The request type that had no registered handler.</param>
    /// <returns>A new <see cref="HandlerNotFoundException"/>.</returns>
    public static HandlerNotFoundException ForRequest(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return new HandlerNotFoundException(
            $"No handler was registered for request type '{requestType.FullName}'. " +
            "Ensure the handler's assembly is passed to RegisterServicesFromAssembly(...) in AddMediatRCompat.");
    }
}
