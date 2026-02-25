using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RetryOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new RetryOptions();

        options.MaxAttempts.ShouldBe(3);
        options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
        options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
        options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
        options.BackoffMultiplier.ShouldBe(2.0);
        options.JitterFactor.ShouldBe(0.1);
        options.UseJitter.ShouldBeTrue();
    }

    [Fact]
    public void HaveDefaultNonRetryableExceptions()
    {
        var options = new RetryOptions();

        options.NonRetryableExceptions.ShouldContain(typeof(ArgumentException));
        options.NonRetryableExceptions.ShouldContain(typeof(ArgumentNullException));
        options.NonRetryableExceptions.ShouldContain(typeof(InvalidOperationException));
        options.NonRetryableExceptions.Count.ShouldBe(3);
    }

    [Fact]
    public void HaveEmptyRetryableExceptionsByDefault()
    {
        var options = new RetryOptions();

        options.RetryableExceptions.ShouldBeEmpty();
    }

    [Fact]
    public void AllowSettingProperties()
    {
        var options = new RetryOptions
        {
            MaxAttempts = 5,
            BaseDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromMinutes(1),
            BackoffStrategy = BackoffStrategy.Linear,
            BackoffMultiplier = 1.5,
            JitterFactor = 0.2,
            UseJitter = false,
        };

        options.MaxAttempts.ShouldBe(5);
        options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
        options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
        options.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
        options.BackoffMultiplier.ShouldBe(1.5);
        options.JitterFactor.ShouldBe(0.2);
        options.UseJitter.ShouldBeFalse();
    }

    [Fact]
    public void AllowAddingRetryableExceptions()
    {
        var options = new RetryOptions();
        options.RetryableExceptions.Add(typeof(TimeoutException));
        options.RetryableExceptions.Add(typeof(IOException));

        options.RetryableExceptions.Count.ShouldBe(2);
        options.RetryableExceptions.ShouldContain(typeof(TimeoutException));
        options.RetryableExceptions.ShouldContain(typeof(IOException));
    }

    [Fact]
    public void AllowModifyingNonRetryableExceptions()
    {
        var options = new RetryOptions();
        options.NonRetryableExceptions.Add(typeof(NotSupportedException));

        options.NonRetryableExceptions.Count.ShouldBe(4);
        options.NonRetryableExceptions.ShouldContain(typeof(NotSupportedException));
    }
}
