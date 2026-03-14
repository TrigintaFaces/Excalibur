using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class ErasureOptionsShould
{
    private static readonly ErasureOptionsValidator Validator = new();

    [Fact]
    public void Have_sensible_defaults()
    {
        var options = new ErasureOptions();

        options.DefaultGracePeriod.ShouldBe(TimeSpan.FromHours(72));
        options.MinimumGracePeriod.ShouldBe(TimeSpan.FromHours(1));
        options.MaximumGracePeriod.ShouldBe(TimeSpan.FromDays(30));
        options.EnableAutoDiscovery.ShouldBeTrue();
        options.RequireVerification.ShouldBeTrue();
        options.NotifyOnCompletion.ShouldBeTrue();
        options.AllowImmediateErasure.ShouldBeFalse();
    }

    [Fact]
    public void Validate_successfully_with_defaults()
    {
        var options = new ErasureOptions();

        Validator.Validate(null, options).ShouldBe(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Fail_when_minimum_grace_period_is_negative()
    {
        var options = new ErasureOptions
        {
            MinimumGracePeriod = TimeSpan.FromMinutes(-1)
        };

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Fail_when_default_grace_period_less_than_minimum()
    {
        var options = new ErasureOptions
        {
            DefaultGracePeriod = TimeSpan.FromMinutes(30),
            MinimumGracePeriod = TimeSpan.FromHours(1)
        };

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Fail_when_default_grace_period_exceeds_maximum()
    {
        var options = new ErasureOptions
        {
            DefaultGracePeriod = TimeSpan.FromDays(31),
            MaximumGracePeriod = TimeSpan.FromDays(30)
        };

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Fail_when_maximum_exceeds_30_days()
    {
        var options = new ErasureOptions
        {
            MaximumGracePeriod = TimeSpan.FromDays(31),
            DefaultGracePeriod = TimeSpan.FromDays(1)
        };

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Fail_when_batch_size_is_zero()
    {
        var options = new ErasureOptions();
        options.Execution.BatchSize = 0;

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Fail_when_max_retry_is_negative()
    {
        var options = new ErasureOptions();
        options.Execution.MaxRetryAttempts = -1;

        Validator.Validate(null, options).Failed.ShouldBeTrue();
    }
}
