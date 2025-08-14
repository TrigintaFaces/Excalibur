using Excalibur.DataAccess.SqlServer;

using FakeItEasy;

using Polly;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class IDataAccessPolicyFactoryShould
{
	[Fact]
	public void ProvideComprehensivePolicy()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var policy = Policy.NoOpAsync();

		A.CallTo(() => factory.GetComprehensivePolicy()).Returns(policy);

		// Act
		var result = factory.GetComprehensivePolicy();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(policy);
		A.CallTo(() => factory.GetComprehensivePolicy()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ProvideRetryPolicy()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var policy = Policy.NoOpAsync();

		A.CallTo(() => factory.GetRetryPolicy()).Returns(policy);

		// Act
		var result = factory.GetRetryPolicy();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(policy);
		A.CallTo(() => factory.GetRetryPolicy()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CreateCircuitBreakerPolicy()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var policy = Policy.NoOpAsync();

		A.CallTo(() => factory.CreateCircuitBreakerPolicy()).Returns(policy);

		// Act
		var result = factory.CreateCircuitBreakerPolicy();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(policy);
		A.CallTo(() => factory.CreateCircuitBreakerPolicy()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportCompositionOfPolicies()
	{
		// Arrange
		var factory = A.Fake<IDataAccessPolicyFactory>();
		var retryPolicy = Policy.NoOpAsync();
		var circuitBreakerPolicy = Policy.NoOpAsync();
		var comprehensivePolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

		A.CallTo(() => factory.GetRetryPolicy()).Returns(retryPolicy);
		A.CallTo(() => factory.CreateCircuitBreakerPolicy()).Returns(circuitBreakerPolicy);
		A.CallTo(() => factory.GetComprehensivePolicy()).Returns(comprehensivePolicy);

		// Act
		var result = factory.GetComprehensivePolicy();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(comprehensivePolicy);
	}
}
