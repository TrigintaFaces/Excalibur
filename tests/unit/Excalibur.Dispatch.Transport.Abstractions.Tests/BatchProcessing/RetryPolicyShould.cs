using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class RetryPolicyShould
{
    [Fact]
    public void Should_Default_MaxRetries_To_3()
    {
        var policy = new RetryPolicy();

        policy.MaxRetries.ShouldBe(3);
    }

    [Fact]
    public void Should_Default_InitialDelay_To_1_Second()
    {
        var policy = new RetryPolicy();

        policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_Default_MaxDelay_To_1_Minute()
    {
        var policy = new RetryPolicy();

        policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Should_Default_BackoffMultiplier_To_2()
    {
        var policy = new RetryPolicy();

        policy.BackoffMultiplier.ShouldBe(2.0);
    }

    [Fact]
    public void Should_Default_UseExponentialBackoff_To_True()
    {
        var policy = new RetryPolicy();

        policy.UseExponentialBackoff.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_UseJitter_To_True()
    {
        var policy = new RetryPolicy();

        policy.UseJitter.ShouldBeTrue();
    }
}
