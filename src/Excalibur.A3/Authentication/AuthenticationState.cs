namespace Excalibur.A3.Authentication;

/// <summary>
///     Represents the possible states of user authentication within an application.
/// </summary>
public enum AuthenticationState
{
	/// <summary>
	///     Indicates that the user is not authenticated and is browsing anonymously.
	/// </summary>
	Anonymous,

	/// <summary>
	///     Indicates that the user has been authenticated, but their identity has not been fully verified.
	/// </summary>
	Authenticated,

	/// <summary>
	///     Indicates that the user has been authenticated and their identity has been fully verified.
	/// </summary>
	Identified
}
