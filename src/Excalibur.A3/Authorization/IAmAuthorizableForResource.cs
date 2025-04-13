using Excalibur.A3.Authorization.Requests;

using Newtonsoft.Json;

namespace Excalibur.A3.Authorization;

/// <summary>
///     Represents an interface for objects that require resource-specific authorization.
/// </summary>
/// <remarks>
///     Extends the <see cref="IAmAuthorizable" /> interface to include additional resource-specific information, such as the resource
///     identifier and its types, used to determine authorization permissions.
/// </remarks>
public interface IAmAuthorizableForResource : IAmAuthorizable
{
	/// <summary>
	///     Gets the identifier of the resource being accessed or manipulated.
	/// </summary>
	/// <value> A string representing the unique identifier of the resource. </value>
	public string ResourceId { get; }

	/// <summary>
	///     Gets the types of resources applicable for authorization.
	/// </summary>
	/// <value> An array of strings indicating the resource types for this object. </value>
	[JsonIgnore]
	public string[] ResourceTypes { get; }
}
