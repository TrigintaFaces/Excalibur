using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;

namespace Excalibur.A3;

/// <summary>
///     Represents an access token that combines authentication and authorization capabilities.
/// </summary>
/// <remarks>
///     This interface serves as a unified abstraction for both authentication and authorization functionalities. It provides user identity
///     and access control details required for secured operations.
/// </remarks>
public interface IAccessToken : IAuthenticationToken, IAuthorizationPolicy
{
	/// <summary>
	///     Gets the user identifier associated with the access token.
	/// </summary>
	/// <remarks>
	///     This property overrides the <see cref="IAuthenticationToken.UserId" /> to provide a unified user identifier in contexts
	///     requiring both authentication and authorization.
	/// </remarks>
	new string? UserId { get; }
}
