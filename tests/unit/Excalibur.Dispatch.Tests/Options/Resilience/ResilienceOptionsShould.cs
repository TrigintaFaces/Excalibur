using Excalibur.Dispatch.Options.Resilience;

using ResilienceCircuitBreakerOptions = Excalibur.Dispatch.Options.Resilience.CircuitBreakerOptions;
using ResilienceRetryOptions = Excalibur.Dispatch.Options.Resilience.RetryOptions;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ResilienceOptionsShould
{
	[Fact]
	public void CircuitBreakerOptions_HaveDefaults()
	{
		var opts = new ResilienceCircuitBreakerOptions();

		opts.FailureThreshold.ShouldBe(5);
	}

	[Fact]
	public void CircuitBreakerOptions_AllowSettingProperties()
	{
		var opts = new ResilienceCircuitBreakerOptions
		{
			FailureThreshold = 10,
		};

		opts.FailureThreshold.ShouldBe(10);
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
		var opts = new ResilienceCircuitBreakerOptions { FailureThreshold = 0 };

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void RetryAttribute_HaveDefaults()
	{
		var attr = new RetryAttribute();

		attr.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void RetryAttribute_AllowSettingProperties()
	{
		var attr = new RetryAttribute { MaxAttempts = 5 };

		attr.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void RetryOptions_HaveDefaults()
	{
		var opts = new ResilienceRetryOptions();

		opts.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void RetryOptions_AllowSettingProperties()
	{
		var opts = new ResilienceRetryOptions { MaxAttempts = 10 };

		opts.MaxAttempts.ShouldBe(10);
	}

	[Fact]
	public void RetryPolicyOptions_HaveDefaults()
	{
		var opts = new RetryPolicyOptions();

		opts.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void RetryPolicyOptions_AllowSettingMaxRetryAttempts()
	{
		var opts = new RetryPolicyOptions { MaxRetryAttempts = 7 };

		opts.MaxRetryAttempts.ShouldBe(7);
	}

	[Fact]
	public void RetryPolicyOptions_BackoffDefaultValues()
	{
		var opts = new RetryPolicyOptions();

		opts.Backoff.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		opts.Backoff.MaxDelay.ShouldBe(TimeSpan.FromMinutes(30));
		opts.Backoff.BackoffMultiplier.ShouldBe(2.0);
		opts.Backoff.EnableJitter.ShouldBeFalse();
		opts.Backoff.JitterFactor.ShouldBe(0.1);
	}

	[Fact]
	public void RetryPolicyOptions_CircuitBreakerDefaultValues()
	{
		var opts = new RetryPolicyOptions();

		opts.CircuitBreaker.EnableCircuitBreaker.ShouldBeFalse();
		opts.CircuitBreaker.CircuitBreakerThreshold.ShouldBe(5);
		opts.CircuitBreaker.CircuitBreakerDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}
}
