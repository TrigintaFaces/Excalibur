using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Options;

public class RetryPolicyOptionsShould
{
    [Fact]
    public void Should_Default_MaxRetryAttempts_To_3()
    {
        var options = new RetryPolicyOptions();

        options.MaxRetryAttempts.ShouldBe(3);
    }

    [Fact]
    public void Should_Default_BaseDelayMs_To_1000()
    {
        var options = new RetryPolicyOptions();

        options.BaseDelayMs.ShouldBe(1000);
    }

    [Fact]
    public void Should_Default_MaxDelayMs_To_30000()
    {
        var options = new RetryPolicyOptions();

        options.MaxDelayMs.ShouldBe(30000);
    }

    [Fact]
    public void Should_Default_UseExponentialBackoff_To_True()
    {
        var options = new RetryPolicyOptions();

        options.UseExponentialBackoff.ShouldBeTrue();
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var options = new RetryPolicyOptions
        {
            MaxRetryAttempts = 5,
            BaseDelayMs = 500,
            MaxDelayMs = 60000,
            UseExponentialBackoff = false,
        };

        options.MaxRetryAttempts.ShouldBe(5);
        options.BaseDelayMs.ShouldBe(500);
        options.MaxDelayMs.ShouldBe(60000);
        options.UseExponentialBackoff.ShouldBeFalse();
    }
}
