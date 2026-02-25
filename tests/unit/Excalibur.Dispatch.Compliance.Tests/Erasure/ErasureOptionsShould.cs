namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class ErasureOptionsShould
{
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

        Should.NotThrow(() => options.Validate());
    }

    [Fact]
    public void Throw_when_minimum_grace_period_is_negative()
    {
        var options = new ErasureOptions
        {
            MinimumGracePeriod = TimeSpan.FromMinutes(-1)
        };

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Throw_when_default_grace_period_less_than_minimum()
    {
        var options = new ErasureOptions
        {
            DefaultGracePeriod = TimeSpan.FromMinutes(30),
            MinimumGracePeriod = TimeSpan.FromHours(1)
        };

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Throw_when_default_grace_period_exceeds_maximum()
    {
        var options = new ErasureOptions
        {
            DefaultGracePeriod = TimeSpan.FromDays(31),
            MaximumGracePeriod = TimeSpan.FromDays(30)
        };

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Throw_when_maximum_exceeds_30_days()
    {
        var options = new ErasureOptions
        {
            MaximumGracePeriod = TimeSpan.FromDays(31),
            DefaultGracePeriod = TimeSpan.FromDays(1)
        };

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Throw_when_batch_size_is_zero()
    {
        var options = new ErasureOptions();
        options.Execution.BatchSize = 0;

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Throw_when_max_retry_is_negative()
    {
        var options = new ErasureOptions();
        options.Execution.MaxRetryAttempts = -1;

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Delegate_certificate_retention_to_sub_options()
    {
        var options = new ErasureOptions
        {
            CertificateRetentionPeriod = TimeSpan.FromDays(365)
        };

        options.Retention.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
    }

    [Fact]
    public void Delegate_batch_size_to_sub_options()
    {
        var options = new ErasureOptions
        {
            BatchSize = 50
        };

        options.Execution.BatchSize.ShouldBe(50);
    }

    [Fact]
    public void Delegate_retry_to_sub_options()
    {
        var options = new ErasureOptions
        {
            MaxRetryAttempts = 5,
            RetryDelay = TimeSpan.FromSeconds(60)
        };

        options.Execution.MaxRetryAttempts.ShouldBe(5);
        options.Execution.RetryDelay.ShouldBe(TimeSpan.FromSeconds(60));
    }
}
