namespace Excalibur.A3.Authorization.Requests;

/// <summary>
///     Represents an interface for objects that require authorization.
/// </summary>
/// <remarks>
///     Objects implementing this interface use an <see cref="IAccessToken" /> to perform authorization checks. The token contains identity
///     and claims information for determining access rights.
/// </remarks>
public interface IAmAuthorizable
{
	/// <summary>
	///     Gets or sets the access token used to authorize the object.
	/// </summary>
	/// <value> The access token containing user identity and claims information for authorization. </value>
	IAccessToken? AccessToken { get; set; }
}
