namespace Excalibur.Core.Exceptions;

/// <summary>
///     Represents an exception specifically designed for API-related errors.
/// </summary>
[Serializable]
public class ApiException : Exception
{
	[NonSerialized] private const int DefaultStatusCode = 500;

	[NonSerialized] private const string DefaultMessage = "An unexpected error occurred";

	/// <summary>
	///     Initializes a new instance of the <see cref="ApiException" /> class with a default error message.
	/// </summary>
	public ApiException()
		: base(DefaultMessage)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="ApiException" /> class with a specified error message and an inner exception that
	///     caused this exception.
	/// </summary>
	/// <param name="message"> The error message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public ApiException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="ApiException" /> class with a specified status code, error message, and inner exception.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="message"> The error message describing the exception. If null, a default message is used. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown if the <paramref name="statusCode" /> is outside the valid range (100-599).
	/// </exception>
	public ApiException(int statusCode, string? message, Exception? innerException)
		: base(message ?? DefaultMessage, innerException)
	{
		if (statusCode is < 100 or > 599)
		{
			throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "The status code must be between 100 and 599");
		}

		StatusCode = statusCode;
	}

	/// <summary>
	///     Gets a unique identifier for the error instance.
	/// </summary>
	public Guid Id { get; } = Guid.NewGuid();

	/// <summary>
	///     Gets the HTTP status code associated with this exception.
	/// </summary>
	public int StatusCode { get; init; } = DefaultStatusCode;

	/// <summary>
	///     Retrieves the HTTP status code from an exception.
	/// </summary>
	/// <param name="ex"> The exception from which to extract the status code. </param>
	/// <returns> The status code if the exception is an <see cref="ApiException" />; otherwise, returns 500. </returns>
	public static int GetStatusCode(Exception ex) => (ex as ApiException)?.StatusCode ?? 500;
}
