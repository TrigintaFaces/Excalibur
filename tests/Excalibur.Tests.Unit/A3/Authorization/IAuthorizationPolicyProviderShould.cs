using Excalibur.A3.Authorization;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Authorization;

public class IAuthorizationPolicyProviderShould : SharedApplicationContextTestBase
{
	[Fact]
	public async Task ReturnIAuthorizationPolicyWhenGetPolicyAsyncCalled()
	{
		// Arrange
		var policyProvider = A.Fake<IAuthorizationPolicyProvider>();
		var policy = A.Fake<IAuthorizationPolicy>();

		_ = A.CallTo(() => policyProvider.GetPolicyAsync()).Returns(policy);

		// Act
		var result = await policyProvider.GetPolicyAsync().ConfigureAwait(true);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeSameAs(policy);
	}
}
