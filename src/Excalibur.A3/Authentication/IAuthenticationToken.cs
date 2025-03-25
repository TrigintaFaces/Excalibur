namespace Excalibur.A3.Authentication;

/// <summary>
///     Represents an authentication token containing identity and claims information.
/// </summary>
public interface IAuthenticationToken
{
	/// <summary>
	///     Gets the current authentication state of the token. Indicates whether the user is anonymous, authenticated, or fully identified.
	/// </summary>
	AuthenticationState AuthenticationState { get; }

	/// <summary>
	///     Gets or sets the raw JWT (JSON Web Token) string associated with the authentication token. This property can be null if the
	///     token has not been issued or is not applicable.
	/// </summary>
	string? Jwt { get; set; }

	/// <summary>
	///     Gets the first name of the authenticated user. Returns null if the user's identity is not available or not set.
	/// </summary>
	string? FirstName { get; }

	/// <summary>
	///     Gets the last name of the authenticated user. Returns null if the user's identity is not available or not set.
	/// </summary>
	string? LastName { get; }

	/// <summary>
	///     Gets the full name of the authenticated user, constructed from <see cref="FirstName" /> and <see cref="LastName" />. Returns an
	///     empty string if neither property is set.
	/// </summary>
	string FullName { get; }

	/// <summary>
	///     Gets the login identifier (e.g., email address or username) of the authenticated user. Returns null if the user's login
	///     information is not available.
	/// </summary>
	string? Login { get; }

	/// <summary>
	///     Gets the unique user identifier (e.g., GUID or system-specific identifier) associated with the authentication token. Returns
	///     null if the user ID is not available or applicable.
	/// </summary>
	string? UserId { get; }
}
