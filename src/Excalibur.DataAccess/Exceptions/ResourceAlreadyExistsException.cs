namespace Excalibur.DataAccess.Exceptions;

/// <summary>
///     Represents an exception that is thrown when attempting to create a resource that already exists.
/// </summary>
/// <remarks>
///     This exception is typically used to indicate that an operation could not proceed because a resource with the same key already exists
///     in the system.
/// </remarks>
[Serializable]
public class ResourceAlreadyExistsException : ResourceException
{
	/// <summary>
	///     The default HTTP status code for resource already exists exceptions.
	/// </summary>
	[NonSerialized] public const int DefaultStatusCode = 404;

	/// <summary>
	///     The default error message for resource already exists exceptions.
	/// </summary>
	[NonSerialized] public const string DefaultMessage = "The specified resource was not found.";

	/// <summary>
	///     Initializes a new instance of the <see cref="ResourceAlreadyExistsException" /> class.
	/// </summary>
	/// <param name="resourceKey"> The unique identifier for the resource that already exists. </param>
	/// <param name="resource"> The name or type of the resource that caused the conflict. </param>
	/// <param name="statusCode"> The HTTP status code associated with the conflict. Defaults to <see cref="DefaultStatusCode" />. </param>
	/// <param name="message"> A custom error message describing the conflict. Defaults to <see cref="DefaultMessage" />. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if applicable. </param>
	public ResourceAlreadyExistsException(string resourceKey, string resource, int? statusCode = null, string? message = null,
		Exception? innerException = null) : base(resource, statusCode ?? DefaultStatusCode, message ?? DefaultMessage, innerException)
	{
		ResourceKey = resourceKey;
	}

	/// <summary>
	///     Gets or sets the unique identifier for the resource that caused the conflict.
	/// </summary>
	/// <value> A string representing the resource key. </value>
	public string ResourceKey { get; set; }
}
