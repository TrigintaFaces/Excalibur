using Excalibur.DataAccess.SqlServer;

using FakeItEasy;

using Polly;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer;

public class IDataAccessPolicyFactoryShould
{
	[Fact]
	public void ProvideComprehensivePolicy()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var expectedPolicy = A.Fake<IAsyncPolicy>();
		_ = A.CallTo(() => factory.GetComprehensivePolicy()).Returns(expectedPolicy);

		// Act
		var policy = factory.GetComprehensivePolicy();

		// Assert
		policy.ShouldBe(expectedPolicy);
		_ = A.CallTo(() => factory.GetComprehensivePolicy()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ProvideRetryPolicy()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var expectedPolicy = A.Fake<IAsyncPolicy>();
		_ = A.CallTo(() => factory.GetRetryPolicy()).Returns(expectedPolicy);

		// Act
		var policy = factory.GetRetryPolicy();

		// Assert
		policy.ShouldBe(expectedPolicy);
		_ = A.CallTo(() => factory.GetRetryPolicy()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ProvideCircuitBreakerPolicy()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var expectedPolicy = A.Fake<IAsyncPolicy>();
		_ = A.CallTo(() => factory.CreateCircuitBreakerPolicy()).Returns(expectedPolicy);

		// Act
		var policy = factory.CreateCircuitBreakerPolicy();

		// Assert
		policy.ShouldBe(expectedPolicy);
		_ = A.CallTo(() => factory.CreateCircuitBreakerPolicy()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReturnDistinctPolicies()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var comprehensivePolicy = A.Fake<IAsyncPolicy>();
		var retryPolicy = A.Fake<IAsyncPolicy>();
		var circuitBreakerPolicy = A.Fake<IAsyncPolicy>();

		_ = A.CallTo(() => factory.GetComprehensivePolicy()).Returns(comprehensivePolicy);
		_ = A.CallTo(() => factory.GetRetryPolicy()).Returns(retryPolicy);
		_ = A.CallTo(() => factory.CreateCircuitBreakerPolicy()).Returns(circuitBreakerPolicy);

		// Act
		var policy1 = factory.GetComprehensivePolicy();
		var policy2 = factory.GetRetryPolicy();
		var policy3 = factory.CreateCircuitBreakerPolicy();

		// Assert
		policy1.ShouldBe(comprehensivePolicy);
		policy2.ShouldBe(retryPolicy);
		policy3.ShouldBe(circuitBreakerPolicy);

		policy1.ShouldNotBe(policy2);
		policy1.ShouldNotBe(policy3);
		policy2.ShouldNotBe(policy3);
	}
}
