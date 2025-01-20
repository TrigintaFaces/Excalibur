using Excalibur.Core.Exceptions;

namespace Excalibur.A3.Exceptions;

/// <summary>
///     Represents an exception that is thrown when authentication fails.
/// </summary>
/// <remarks> Inherits from <see cref="ApiException" /> to provide a more specific exception type for authentication failures. </remarks>
[Serializable]
public class NotAuthenticatedException(int? statusCode = null, string? message = null, Exception? innerException = null)
	: ApiException(statusCode ?? DefaultStatusCode, message ?? DefaultMessage, innerException)
{
	/// <summary>
	///     The default HTTP status code for authentication failure (401).
	/// </summary>
	[NonSerialized] public const int DefaultStatusCode = 401;

	/// <summary>
	///     The default error message for authentication failure.
	/// </summary>
	[NonSerialized] public const string DefaultMessage = "Authentication failed.";

	/// <summary>
	///     Creates a <see cref="NotAuthenticatedException" /> with a message indicating a missing claim.
	/// </summary>
	/// <param name="claim"> The name of the missing claim that caused the authentication failure. </param>
	/// <returns> A <see cref="NotAuthenticatedException" /> instance with a detailed message about the missing claim. </returns>
	public static NotAuthenticatedException BecauseMissingClaim(string claim) =>
		new(DefaultStatusCode, $"Authentication failed because the '{claim}' claim was missing.");

	/// <summary>
	///     Throws a <see cref="NotAuthenticatedException" /> if the specified condition is true.
	/// </summary>
	/// <param name="condition"> A boolean value indicating whether the exception should be thrown. </param>
	/// <param name="statusCode"> An optional status code to include in the exception. </param>
	/// <param name="message"> An optional message to include in the exception. </param>
	/// <param name="innerException"> An optional inner exception that provides additional context. </param>
	/// <exception cref="NotAuthenticatedException"> Thrown if <paramref name="condition" /> is <c> true </c>. </exception>
	public static void ThrowIf(bool condition, int? statusCode = null, string? message = null, Exception? innerException = null)
	{
		if (condition)
		{
			throw new NotAuthenticatedException(statusCode, message, innerException);
		}
	}
}
