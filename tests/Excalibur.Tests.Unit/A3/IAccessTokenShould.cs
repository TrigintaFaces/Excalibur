using Excalibur.A3;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;

using Shouldly;

namespace Excalibur.Tests.Unit.A3;

public class IAccessTokenShould
{
	[Fact]
	public void BeImplementedByAccessToken()
	{
		// Arrange
		var accessToken = CreateAccessToken();

		// Assert
		_ = accessToken.ShouldNotBeNull();
		_ = accessToken.ShouldBeAssignableTo<IAuthenticationToken>();
		_ = accessToken.ShouldBeAssignableTo<IAuthorizationPolicy>();
	}

	[Fact]
	public void HaveRequiredProperties()
	{
		// Arrange
		var accessToken = CreateAccessToken();

		// Assert
		_ = accessToken.UserId.ShouldNotBeNull();
		accessToken.AuthenticationState.ShouldNotBe(AuthenticationState.Anonymous);
		_ = accessToken.TenantId.ShouldNotBeNull();
	}

	[Fact]
	public void SupportAuthorizationMethods()
	{
		// Arrange
		var accessToken = CreateAccessToken();

		// Act & Assert
		accessToken.IsAuthorized("TestActivity").ShouldBeFalse();
		accessToken.HasGrant("TestActivity").ShouldBeFalse();
	}

	private IAccessToken CreateAccessToken() => new MockAccessToken();

	private sealed class MockAccessToken : IAccessToken
	{
		public AuthenticationState AuthenticationState => AuthenticationState.Authenticated;
		public string? Jwt { get; set; }
		public string? FirstName => "John";
		public string? LastName => "Doe";
		public string FullName => "John Doe";
		public string? Login => "johndoe";
		public string TenantId => "tenant1";
		public string? UserId => "user1";

		public bool IsAuthorized(string activityName, string? resourceId = null) => false;

		public bool HasGrant(string activityName) => false;

		public bool HasGrant<TActivity>() => false;

		public bool HasGrant(string resourceType, string resourceId) => false;

		public bool HasGrant<TResourceType>(string resourceId) => false;

		/// <inheritdoc />
		public bool IsAnonymous() => !IsAuthenticated();

		/// <inheritdoc />
		public bool IsAuthenticated() => AuthenticationState == AuthenticationState.Authenticated;
	}
}
