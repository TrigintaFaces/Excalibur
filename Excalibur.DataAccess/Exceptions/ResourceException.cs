using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.Exceptions;

/// <summary>
///     Represents a base exception for errors that occur during operations on a specific resource.
/// </summary>
/// <remarks>
///     This class serves as a foundation for more specific resource-related exceptions, such as concurrency conflicts or resource existence checks.
/// </remarks>
[Serializable]
public class ResourceException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ResourceException" /> class.
	/// </summary>
	/// <param name="resource"> The name or type of the resource associated with the error. </param>
	/// <param name="statusCode">
	///     The HTTP status code representing the error. Defaults to <c> 500 Internal Server Error </c> if not specified.
	/// </param>
	/// <param name="message"> A custom error message describing the error. Defaults to a generic message using the resource name. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if applicable. </param>
	protected ResourceException(string resource, int? statusCode, string? message = null, Exception? innerException = null)
		: base(statusCode ?? 500, message ?? $"Operation failed for resource {resource}", innerException)
	{
		ArgumentNullException.ThrowIfNull(resource, nameof(resource));

		Resource = resource;
	}

	/// <summary>
	///     Gets or sets the name or type of the resource that caused the error.
	/// </summary>
	/// <value> A string representing the resource associated with the exception. </value>
	public string Resource { get; protected set; }
}
