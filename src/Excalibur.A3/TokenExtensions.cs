using Excalibur.A3.Authentication;

namespace Excalibur.A3;

/// <summary>
///     Provides extension methods for <see cref="IAuthenticationToken" /> to simplify authentication state checks.
/// </summary>
public static class TokenExtensions
{
	/// <summary>
	///     Determines whether the provided authentication token represents an anonymous user.
	/// </summary>
	/// <param name="token"> The <see cref="IAuthenticationToken" /> to evaluate. </param>
	/// <returns> <c> true </c> if the token represents an anonymous user; otherwise, <c> false </c>. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="token" /> is <c> null </c>. </exception>
	public static bool IsAnonymous(this IAuthenticationToken token)
	{
		ArgumentNullException.ThrowIfNull(token, nameof(token));
		return !token.IsAuthenticated();
	}

	/// <summary>
	///     Determines whether the provided authentication token represents an authenticated user.
	/// </summary>
	/// <param name="token"> The <see cref="IAuthenticationToken" /> to evaluate. </param>
	/// <returns> <c> true </c> if the token represents an authenticated user; otherwise, <c> false </c>. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="token" /> is <c> null </c>. </exception>
	public static bool IsAuthenticated(this IAuthenticationToken token)
	{
		ArgumentNullException.ThrowIfNull(token, nameof(token));
		return token.AuthenticationState == AuthenticationState.Authenticated;
	}
}
