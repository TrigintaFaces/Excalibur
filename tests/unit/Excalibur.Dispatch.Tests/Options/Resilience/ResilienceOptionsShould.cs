using Excalibur.Dispatch.Options.Resilience;

using ResilienceCircuitBreakerOptions = Excalibur.Dispatch.Options.Resilience.CircuitBreakerOptions;
using ResilienceRetryOptions = Excalibur.Dispatch.Options.Resilience.RetryOptions;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ResilienceOptionsShould
{
	[Fact]
	public void CircuitBreakerOptions_HaveDefaults()
	{
		var opts = new ResilienceCircuitBreakerOptions();

		opts.ConsecutiveFailureThreshold.ShouldBe(5);
		opts.MinimumThroughput.ShouldBe(5);
	}

	[Fact]
	public void CircuitBreakerOptions_AllowSettingProperties()
	{
		var opts = new ResilienceCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 10,
			MinimumThroughput = 10,
		};

		opts.ConsecutiveFailureThreshold.ShouldBe(10);
		opts.MinimumThroughput.ShouldBe(10);
	}

	[Fact]
	public void CircuitBreakerOptionsValidator_PassesForValidDefaults()
	{
		var validator = new CircuitBreakerOptionsValidator();
		var opts = new ResilienceCircuitBreakerOptions();

		var result = validator.Validate(null, opts);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void CircuitBreakerOptionsValidator_FailsForZeroFailureThreshold()
	{
		var validator = new CircuitBreakerOptionsValidator();
		var opts = new ResilienceCircuitBreakerOptions { ConsecutiveFailureThreshold = 0 };

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void RetryAttribute_HaveDefaults()
	{
		var attr = new RetryAttribute();

		attr.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void RetryAttribute_AllowSettingProperties()
	{
		var attr = new RetryAttribute { MaxRetryAttempts = 5 };

		attr.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void RetryOptions_HaveDefaults()
	{
		var opts = new ResilienceRetryOptions();

		opts.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void RetryOptions_AllowSettingProperties()
	{
		var opts = new ResilienceRetryOptions { MaxRetryAttempts = 10 };

		opts.MaxRetryAttempts.ShouldBe(10);
	}

}
