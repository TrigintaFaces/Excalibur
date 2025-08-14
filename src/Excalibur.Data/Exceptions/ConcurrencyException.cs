namespace Excalibur.DataAccess.Exceptions;

/// <summary>
///     Represents an exception that occurs when a concurrency conflict is detected while interacting with a resource.
/// </summary>
/// <remarks>
///     Typically thrown when an operation cannot proceed due to concurrent modifications of a resource, ensuring consistency and data integrity.
/// </remarks>
[Serializable]
public class ConcurrencyException : ResourceException
{
	/// <summary>
	///     The default HTTP status code for concurrency exceptions.
	/// </summary>
	[NonSerialized] public const int DefaultStatusCode = 412;

	/// <summary>
	///     The default error message for concurrency exceptions.
	/// </summary>
	[NonSerialized] public const string DefaultMessage = "A concurrency conflict occured.";

	/// <summary>
	///     Initializes a new instance of the <see cref="ConcurrencyException" /> class.
	/// </summary>
	/// <param name="resourceKey"> The unique identifier for the resource that caused the concurrency conflict. </param>
	/// <param name="resource"> The name or type of the resource where the conflict occurred. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. Defaults to <see cref="DefaultStatusCode" />. </param>
	/// <param name="message"> A custom error message. Defaults to <see cref="DefaultMessage" />. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if applicable. </param>
	public ConcurrencyException(string resourceKey, string resource, int? statusCode = null, string? message = null,
		Exception? innerException = null) : base(resource, statusCode ?? DefaultStatusCode, message ?? DefaultMessage, innerException)
	{
		ArgumentNullException.ThrowIfNull(resourceKey, nameof(resourceKey));
		ArgumentNullException.ThrowIfNull(resource, nameof(resource));

		ResourceKey = resourceKey;
	}

	/// <summary>
	///     Gets or sets the unique identifier for the resource that caused the concurrency conflict.
	/// </summary>
	/// <value> A string representing the resource key. </value>
	public string ResourceKey { get; set; }
}
