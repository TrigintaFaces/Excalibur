namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Thrown by <c>AddMediatRCompat</c> when more than one handler is registered for the same request type
/// within the registered assemblies — a request must have exactly one handler (MediatR semantics).
/// Derives from <see cref="InvalidOperationException"/> for swap-only compatibility.
/// </summary>
public sealed class DuplicateRequestHandlerException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="DuplicateRequestHandlerException"/> class.</summary>
    public DuplicateRequestHandlerException()
        : base("Multiple handlers were registered for the same request type.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DuplicateRequestHandlerException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public DuplicateRequestHandlerException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DuplicateRequestHandlerException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public DuplicateRequestHandlerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates a <see cref="DuplicateRequestHandlerException"/> for the given request type.</summary>
    /// <param name="requestType">The request type with more than one handler.</param>
    /// <returns>A new <see cref="DuplicateRequestHandlerException"/>.</returns>
    public static DuplicateRequestHandlerException ForRequest(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return new DuplicateRequestHandlerException(
            $"Multiple handlers were registered for request type '{requestType.FullName}'. A request must have exactly one handler.");
    }
}
