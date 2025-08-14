namespace Excalibur.A3.Authentication;

/// <summary>
///     Provides functionality to retrieve an authentication token representing the current user's identity and claims.
/// </summary>
public interface IAuthenticationTokenProvider
{
	/// <summary>
	///     Asynchronously retrieves the current authentication token.
	/// </summary>
	/// <returns>
	///     A <see cref="Task{TResult}" /> that resolves to an instance of <see cref="IAuthenticationToken" />, representing the current
	///     user's authentication details.
	/// </returns>
	/// <remarks>
	///     This method can be used to obtain information about the user's authentication state, identity, claims, and other related details
	///     through an implementation of <see cref="IAuthenticationToken" />.
	/// </remarks>
	public Task<IAuthenticationToken> GetAuthenticationTokenAsync();
}
