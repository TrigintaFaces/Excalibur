using Excalibur.A3.Authentication;

using Shouldly;

namespace Excalibur.Tests.Unit.A3;

public class SimpleTokenExtensionsShould
{
	[Fact]
	public void ReturnTrueForAnonymousToken()
	{
		// Arrange
		var anonymousToken = CreateMockToken(AuthenticationState.Anonymous);

		// Act
		var result = anonymousToken.IsAnonymous();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForAuthenticatedToken()
	{
		// Arrange
		var authenticatedToken = CreateMockToken(AuthenticationState.Authenticated);

		// Act
		var result = authenticatedToken.IsAnonymous();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForAuthenticatedToken()
	{
		// Arrange
		var authenticatedToken = CreateMockToken(AuthenticationState.Authenticated);

		// Act
		var result = authenticatedToken.IsAuthenticated();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForAnonymousToken()
	{
		// Arrange
		var anonymousToken = CreateMockToken(AuthenticationState.Anonymous);

		// Act
		var result = anonymousToken.IsAuthenticated();

		// Assert
		result.ShouldBeFalse();
	}

	private IAuthenticationToken CreateMockToken(AuthenticationState state) =>
		new MockAuthenticationToken(state);

	private sealed class MockAuthenticationToken(AuthenticationState state) : IAuthenticationToken
	{
		public AuthenticationState AuthenticationState { get; } = state;
		public string? Jwt { get; set; }
		public string? FirstName => null;
		public string? LastName => null;
		public string FullName => string.Empty;
		public string? Login => null;
		public string? UserId => null;

		/// <inheritdoc />
		public bool IsAnonymous() => !IsAuthenticated();

		/// <inheritdoc />
		public bool IsAuthenticated() => AuthenticationState == AuthenticationState.Authenticated;
	}
}
