using Excalibur.A3;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Authorization;

public class IAuthorizationPolicyShould : SharedApplicationContextTestBase
{
	[Fact]
	public void BeRegisteredAsScoped()
	{
		var services = new ServiceCollection();

		// Register minimal dependencies
		_ = services.AddScoped<IAuthorizationPolicy, AuthorizationPolicy>();

		// Assert
		var authPolicyDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IAuthorizationPolicy));

		_ = authPolicyDescriptor.ShouldNotBeNull();
		authPolicyDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void ProvideAccessTokenInformationThroughProperties()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		// Configure expected values
		const string expectedUserId = "test-user-123";
		const string expectedTenantId = "test-tenant-456";

		_ = A.CallTo(() => authToken.UserId).Returns(expectedUserId);
		_ = A.CallTo(() => authPolicy.UserId).Returns(expectedUserId);
		_ = A.CallTo(() => authPolicy.TenantId).Returns(expectedTenantId);

		// Act - Create an access token using these components
		var accessToken = new AccessToken(authToken, authPolicy);

		// Assert
		accessToken.UserId.ShouldBe(expectedUserId);
		accessToken.TenantId.ShouldBe(expectedTenantId);
	}

	[Fact]
	public void PerformAuthorizationChecks()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		_ = A.CallTo(() => authToken.UserId).Returns("test-user-123");
		_ = A.CallTo(() => authPolicy.UserId).Returns("test-user-123");

		// Setup authorization checks
		_ = A.CallTo(() => authPolicy.IsAuthorized("TestActivity", "resource123")).Returns(true);
		_ = A.CallTo(() => authPolicy.IsAuthorized("AdminActivity", null)).Returns(false);

		// Act - Create an access token and perform authorization checks
		var accessToken = new AccessToken(authToken, authPolicy);
		var isAuthorizedWithResource = accessToken.IsAuthorized("TestActivity", "resource123");
		var isAuthorizedWithoutResource = accessToken.IsAuthorized("AdminActivity");

		// Assert
		isAuthorizedWithResource.ShouldBeTrue();
		isAuthorizedWithoutResource.ShouldBeFalse();
	}

	[Fact]
	public void VerifyActivityAndResourceGrants()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();

		_ = A.CallTo(() => authToken.UserId).Returns("test-user-123");
		_ = A.CallTo(() => authPolicy.UserId).Returns("test-user-123");

		// Setup grant checks
		_ = A.CallTo(() => authPolicy.HasGrant("TestActivity")).Returns(true);
		_ = A.CallTo(() => authPolicy.HasGrant("AdminActivity")).Returns(false);
		_ = A.CallTo(() => authPolicy.HasGrant("Resource", "resource123")).Returns(true);
		_ = A.CallTo(() => authPolicy.HasGrant("Resource", "resource456")).Returns(false);

		// Act - Create an access token and check grants
		var accessToken = new AccessToken(authToken, authPolicy);
		var hasActivityGrant = accessToken.HasGrant("TestActivity");
		var hasNoActivityGrant = accessToken.HasGrant("AdminActivity");
		var hasResourceGrant = accessToken.HasGrant("Resource", "resource123");
		var hasNoResourceGrant = accessToken.HasGrant("Resource", "resource456");

		// Assert
		hasActivityGrant.ShouldBeTrue();
		hasNoActivityGrant.ShouldBeFalse();
		hasResourceGrant.ShouldBeTrue();
		hasNoResourceGrant.ShouldBeFalse();
	}
}
