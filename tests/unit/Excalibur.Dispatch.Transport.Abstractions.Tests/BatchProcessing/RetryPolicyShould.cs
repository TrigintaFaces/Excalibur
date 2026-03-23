using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RetryPolicyOptionsShould
{
	[Fact]
	public void Default_MaxRetryAttempts_To_3()
	{
		var options = new TransportRetryPolicyOptions();

		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Default_BaseDelayMs_To_1000()
	{
		var options = new TransportRetryPolicyOptions();

		options.BaseDelayMs.ShouldBe(1000);
	}

	[Fact]
	public void Default_MaxDelayMs_To_30000()
	{
		var options = new TransportRetryPolicyOptions();

		options.MaxDelayMs.ShouldBe(30000);
	}

	[Fact]
	public void Default_UseExponentialBackoff_To_True()
	{
		var options = new TransportRetryPolicyOptions();

		options.UseExponentialBackoff.ShouldBeTrue();
	}
}
