using Excalibur.A3;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Domain.Exceptions;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.A3;

public class AccessTokenShould
{
	[Fact]
	public void ImplementIAccessTokenInterface()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		_ = A.CallTo(() => authToken.UserId).Returns("user123");
		_ = A.CallTo(() => authPolicy.UserId).Returns("user123");

		// Act
		var accessToken = new AccessToken(authToken, authPolicy);

		// Assert
		_ = accessToken.ShouldBeAssignableTo<IAccessToken>();
		_ = accessToken.ShouldBeAssignableTo<IAuthenticationToken>();
		_ = accessToken.ShouldBeAssignableTo<IAuthorizationPolicy>();
	}

	[Fact]
	public void ThrowDomainExceptionWhenUserIdsMismatch()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		_ = A.CallTo(() => authToken.UserId).Returns("user123");
		_ = A.CallTo(() => authPolicy.UserId).Returns("differentUser456");

		// Act & Assert
		var exception = Should.Throw<DomainException>(() => new AccessToken(authToken, authPolicy));

		exception.StatusCode.ShouldBe(400);
		exception.Message.ShouldContain("do not match");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenAuthTokenIsNull()
	{
		// Arrange
		IAuthenticationToken authToken = null;
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => new AccessToken(authToken, authPolicy));
		exception.ParamName.ShouldBe("authenticationToken");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenAuthPolicyIsNull()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		IAuthorizationPolicy authPolicy = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => new AccessToken(authToken, authPolicy));
		exception.ParamName.ShouldBe("authorizationPolicy");
	}

	[Fact]
	public void DelegateAuthenticationPropertiesToAuthToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		var expectedState = AuthenticationState.Identified;
		var expectedJwt = "jwt.token.string";
		var expectedFirstName = "John";
		var expectedLastName = "Doe";
		var expectedFullName = "John Doe";
		var expectedLogin = "john.doe@example.com";
		var expectedUserId = "user123";

		_ = A.CallTo(() => authToken.AuthenticationState).Returns(expectedState);
		_ = A.CallTo(() => authToken.Jwt).Returns(expectedJwt);
		_ = A.CallTo(() => authToken.FirstName).Returns(expectedFirstName);
		_ = A.CallTo(() => authToken.LastName).Returns(expectedLastName);
		_ = A.CallTo(() => authToken.FullName).Returns(expectedFullName);
		_ = A.CallTo(() => authToken.Login).Returns(expectedLogin);
		_ = A.CallTo(() => authToken.UserId).Returns(expectedUserId);

		_ = A.CallTo(() => authPolicy.UserId).Returns(expectedUserId);

		// Act
		var accessToken = new AccessToken(authToken, authPolicy);

		// Assert
		accessToken.AuthenticationState.ShouldBe(expectedState);
		accessToken.Jwt.ShouldBe(expectedJwt);
		accessToken.FirstName.ShouldBe(expectedFirstName);
		accessToken.LastName.ShouldBe(expectedLastName);
		accessToken.FullName.ShouldBe(expectedFullName);
		accessToken.Login.ShouldBe(expectedLogin);
		accessToken.UserId.ShouldBe(expectedUserId);
	}

	[Fact]
	public void SetJwtOnUnderlyingAuthToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		_ = A.CallTo(() => authToken.UserId).Returns("user123");
		_ = A.CallTo(() => authPolicy.UserId).Returns("user123");

		var accessToken = new AccessToken(authToken, authPolicy);
		var newJwt = "new.jwt.token";

		// Act
		_ = A.CallToSet(() => authToken.Jwt).To(newJwt);
		accessToken.Jwt = newJwt;

		// Assert
		_ = A.CallToSet(() => authToken.Jwt).To(newJwt).MustHaveHappened();
	}

	[Fact]
	public void DelegateAuthorizationMethodsToAuthPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		var expectedTenantId = "tenant123";

		_ = A.CallTo(() => authToken.UserId).Returns("user123");
		_ = A.CallTo(() => authPolicy.UserId).Returns("user123");
		_ = A.CallTo(() => authPolicy.TenantId).Returns(expectedTenantId);

		// Setup authorization check responses
		_ = A.CallTo(() => authPolicy.IsAuthorized("TestActivity", null)).Returns(true);
		_ = A.CallTo(() => authPolicy.IsAuthorized("TestActivity", "resource1")).Returns(false);
		_ = A.CallTo(() => authPolicy.HasGrant("AdminAccess")).Returns(true);
		_ = A.CallTo(() => authPolicy.HasGrant("UserData", "user123")).Returns(true);

		// Act
		var accessToken = new AccessToken(authToken, authPolicy);

		// Assert
		accessToken.TenantId.ShouldBe(expectedTenantId);

		// Test authorization methods
		accessToken.IsAuthorized("TestActivity").ShouldBeTrue();
		accessToken.IsAuthorized("TestActivity", "resource1").ShouldBeFalse();
		accessToken.HasGrant("AdminAccess").ShouldBeTrue();
		accessToken.HasGrant("UserData", "user123").ShouldBeTrue();

		// Verify calls are delegated
		_ = A.CallTo(() => authPolicy.IsAuthorized("TestActivity", null)).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => authPolicy.IsAuthorized("TestActivity", "resource1")).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => authPolicy.HasGrant("AdminAccess")).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => authPolicy.HasGrant("UserData", "user123")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DelegateGenericAuthorizationMethodsToAuthPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		_ = A.CallTo(() => authToken.UserId).Returns("user123");

		var authPolicy = new TestAuthorizationPolicyStub("user123");

		// Act
		var accessToken = new AccessToken(authToken, authPolicy);

		// Assert
		accessToken.HasGrant<TestActivity>().ShouldBeTrue(); // hardcoded in stub
		accessToken.HasGrant<TestResource>("resource123").ShouldBeTrue(); // now matched by stub
	}

	// Test types for generic HasGrant methods
	private sealed class TestActivity
	{
	}

	private sealed class TestResource
	{
	}

	private sealed class TestAuthorizationPolicyStub : IAuthorizationPolicy
	{
		public TestAuthorizationPolicyStub(string userId) => UserId = userId;

		public string TenantId => "tenant123";
		public string? UserId { get; }

		public bool IsAuthorized(string activityName, string? resourceId = null) => false;

		public bool HasGrant(string activityName) => false;

		public bool HasGrant(string resourceType, string resourceId) => false;

		public bool HasGrant<TActivity>() => typeof(TActivity) == typeof(TestActivity);

		public bool HasGrant<TResourceType>(string resourceId)
		{
			// ✅ This is the call that matters for your test
			return typeof(TResourceType) == typeof(TestResource) && resourceId == "resource123";
		}
	}
}
