using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Options;

public class ProviderOptionsValidatorShould
{
    private readonly ProviderOptionsValidator _validator = new();

    [Fact]
    public void Should_Succeed_With_Default_Options()
    {
        var options = new ProviderOptions();

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Should_Throw_On_Null_Options()
    {
        Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
    }

    [Fact]
    public void Should_Fail_When_DefaultTimeoutMs_Is_Zero()
    {
        var options = new ProviderOptions { DefaultTimeoutMs = 0 };

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeFalse();
        result.FailureMessage.ShouldContain("DefaultTimeoutMs");
    }

    [Fact]
    public void Should_Fail_When_DefaultTimeoutMs_Is_Negative()
    {
        var options = new ProviderOptions { DefaultTimeoutMs = -1 };

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void Should_Fail_When_BaseDelayMs_Greater_Than_MaxDelayMs()
    {
        var options = new ProviderOptions
        {
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = 5000,
                MaxDelayMs = 1000,
            },
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeFalse();
        result.FailureMessage.ShouldContain("BaseDelayMs");
        result.FailureMessage.ShouldContain("MaxDelayMs");
    }

    [Fact]
    public void Should_Fail_When_BaseDelayMs_Is_Zero()
    {
        var options = new ProviderOptions
        {
            RetryPolicy = new RetryPolicyOptions { BaseDelayMs = 0 },
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeFalse();
        result.FailureMessage.ShouldContain("BaseDelayMs");
    }

    [Fact]
    public void Should_Fail_When_MaxDelayMs_Is_Zero()
    {
        var options = new ProviderOptions
        {
            RetryPolicy = new RetryPolicyOptions { MaxDelayMs = 0, BaseDelayMs = 0 },
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void Should_Succeed_When_BaseDelayMs_Equals_MaxDelayMs()
    {
        var options = new ProviderOptions
        {
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = 1000,
                MaxDelayMs = 1000,
            },
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Should_Collect_Multiple_Failures()
    {
        var options = new ProviderOptions
        {
            DefaultTimeoutMs = 0,
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = 0,
                MaxDelayMs = 0,
            },
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeFalse();
        // Multiple failures should be reported
        result.FailureMessage.ShouldContain("DefaultTimeoutMs");
        result.FailureMessage.ShouldContain("BaseDelayMs");
    }
}
