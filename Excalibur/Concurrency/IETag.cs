namespace Excalibur.Concurrency;

/// <summary>
///     Represents an ETag, commonly used for resource versioning in concurrency scenarios.
/// </summary>
public interface IETag
{
	/// <summary>
	///     Gets or sets the incoming ETag value, typically provided by the client to indicate the version of a resource they are working with.
	/// </summary>
	string? IncomingValue { get; set; }

	/// <summary>
	///     Gets or sets the outgoing ETag value, typically generated by the server to represent the current version of a resource.
	/// </summary>
	string? OutgoingValue { get; set; }
}
