using Excalibur.A3.Authorization;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Authorization;

public class MockAuthorizationPolicyProviderShould
{
	private readonly IAuthorizationPolicyProvider _policyProvider;
	private readonly IAuthorizationPolicy _mockPolicy;

	public MockAuthorizationPolicyProviderShould()
	{
		_mockPolicy = A.Fake<IAuthorizationPolicy>();
		_policyProvider = new MockAuthorizationPolicyProvider(_mockPolicy);

		// Setup mock policy
		_ = A.CallTo(() => _mockPolicy.TenantId).Returns("tenant-123");
		_ = A.CallTo(() => _mockPolicy.UserId).Returns("user-456");
	}

	[Fact]
	public async Task ReturnPolicyWhenGetPolicyAsyncIsCalled()
	{
		// Act
		var policy = await _policyProvider.GetPolicyAsync().ConfigureAwait(true);

		// Assert
		policy.ShouldBeSameAs(_mockPolicy);
		policy.TenantId.ShouldBe("tenant-123");
		policy.UserId.ShouldBe("user-456");
	}

	[Fact]
	public async Task DelegateAuthorizationCallsToPolicy()
	{
		// Arrange
		var policy = await _policyProvider.GetPolicyAsync().ConfigureAwait(true);

		// Setup policy behavior
		_ = A.CallTo(() => _mockPolicy.IsAuthorized("TestActivity", "resource123")).Returns(true);
		_ = A.CallTo(() => _mockPolicy.HasGrant("ActivityName")).Returns(true);
		_ = A.CallTo(() => _mockPolicy.HasGrant("ResourceType", "resource123")).Returns(true);

		// Act
		var isAuthorizedResult = policy.IsAuthorized("TestActivity", "resource123");
		var hasGrantResult = policy.HasGrant("ActivityName");
		var hasResourceGrantResult = policy.HasGrant("ResourceType", "resource123");

		// Assert
		isAuthorizedResult.ShouldBeTrue();
		hasGrantResult.ShouldBeTrue();
		hasResourceGrantResult.ShouldBeTrue();

		// Verify delegations
		_ = A.CallTo(() => _mockPolicy.IsAuthorized("TestActivity", "resource123")).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _mockPolicy.HasGrant("ActivityName")).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _mockPolicy.HasGrant("ResourceType", "resource123")).MustHaveHappenedOnceExactly();
	}

	private sealed class MockAuthorizationPolicyProvider(IAuthorizationPolicy policy) : IAuthorizationPolicyProvider
	{
		public Task<IAuthorizationPolicy> GetPolicyAsync()
		{
			return Task.FromResult(policy);
		}
	}
}
