using Excalibur.Exceptions;

namespace Excalibur.Domain.Exceptions;

/// <summary>
///     Represents a domain-level exception that occurs within the application's business logic.
/// </summary>
[Serializable]
public class DomainException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DomainException" /> class.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="message"> A message describing the exception. If not provided, a default message is used. </param>
	/// <param name="innerException"> The exception that caused the current exception, if any. </param>
	public DomainException(int statusCode, string? message = null, Exception? innerException = null)
		: base(statusCode, message ?? "Exception within application logic.", innerException)
	{
	}

	/// <summary>
	///     Throws a <see cref="DomainException" /> if the specified condition evaluates to <c> true </c>.
	/// </summary>
	/// <param name="condition"> The condition that determines whether to throw the exception. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="message"> A message describing the exception. </param>
	/// <exception cref="DomainException"> Thrown if <paramref name="condition" /> evaluates to <c> true </c>. </exception>
	public static void ThrowIf(bool condition, int statusCode, string message)
	{
		if (condition)
		{
			throw new DomainException(statusCode, message);
		}
	}

	/// <summary>
	///     Throws a <see cref="DomainException" /> if the specified condition evaluates to <c> true </c>.
	/// </summary>
	/// <param name="condition"> The condition that determines whether to throw the exception. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. </param>
	/// <param name="message"> A message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	/// <exception cref="DomainException"> Thrown if <paramref name="condition" /> evaluates to <c> true </c>. </exception>
	public static void ThrowIf(bool condition, int statusCode, string message, Exception innerException)
	{
		if (condition)
		{
			throw new DomainException(statusCode, message, innerException);
		}
	}
}
