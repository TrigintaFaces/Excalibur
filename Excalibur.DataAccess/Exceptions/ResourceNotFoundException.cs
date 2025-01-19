namespace Excalibur.DataAccess.Exceptions;

/// <summary>
///     Represents an exception that is thrown when a specified resource cannot be found.
/// </summary>
/// <remarks>
///     This exception provides context about the missing resource, including its unique identifier and a default HTTP status code of 404.
/// </remarks>
[Serializable]
public class ResourceNotFoundException : ResourceException
{
	/// <summary>
	///     The default HTTP status code for resource not found exceptions.
	/// </summary>
	[NonSerialized] public const int DefaultStatusCode = 404;

	/// <summary>
	///     The default error message for resource not found exceptions.
	/// </summary>
	[NonSerialized] public const string DefaultMessage = "The specified resource was not found.";

	/// <summary>
	///     Initializes a new instance of the <see cref="ResourceNotFoundException" /> class.
	/// </summary>
	/// <param name="resourceKey"> The unique identifier for the resource. </param>
	/// <param name="resource"> The name or type of the resource that could not be found. </param>
	/// <param name="statusCode"> The HTTP status code for the exception. Defaults to <c> 404 Not Found </c>. </param>
	/// <param name="message"> A custom error message describing the error. Defaults to <see cref="DefaultMessage" />. </param>
	/// <param name="innerException"> An optional inner exception that provides additional details about the root cause of the error. </param>
	public ResourceNotFoundException(string resourceKey, string resource, int? statusCode = null, string? message = null,
		Exception? innerException = null) : base(resource, statusCode ?? DefaultStatusCode, message ?? DefaultMessage, innerException)
	{
		ArgumentNullException.ThrowIfNull(resourceKey, nameof(resourceKey));

		ResourceKey = resourceKey;
	}

	/// <summary>
	///     Gets or sets the unique identifier for the resource that could not be found.
	/// </summary>
	/// <value> A string representing the resource key. </value>
	public string ResourceKey { get; set; }
}
