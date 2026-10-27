using Excalibur.Data.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlDataAccessPolicyFactoryShould
{
    [Fact]
    public void ThrowOnNullLogger()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new SqlDataAccessPolicyFactory(null!));
    }

    [Fact]
    public void CreateSuccessfullyWithValidLogger()
    {
        // Arrange & Act
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Assert
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void ImplementIDataAccessPolicyFactory()
    {
        // Arrange & Act
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Assert
        factory.ShouldBeAssignableTo<IDataAccessPolicyFactory>();
    }

    [Fact]
    public void ReturnNonNullRetryPolicy()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy = factory.GetRetryPolicy();

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public void ReturnSameCachedRetryPolicyOnMultipleCalls()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy1 = factory.GetRetryPolicy();
        var policy2 = factory.GetRetryPolicy();

        // Assert
        policy1.ShouldBeSameAs(policy2);
    }

    [Fact]
    public void ReturnNonNullCircuitBreakerPolicy()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy = factory.CreateCircuitBreakerPolicy();

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public void ReturnNonNullComprehensivePolicy()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy = factory.GetComprehensivePolicy();

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public void CreateNewCircuitBreakerPolicyEachCall()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy1 = factory.CreateCircuitBreakerPolicy();
        var policy2 = factory.CreateCircuitBreakerPolicy();

        // Assert — circuit breaker policies should be new instances
        policy1.ShouldNotBeSameAs(policy2);
    }

    // ---- The retry schedule. These arms did not exist: this suite asserted only that policies are
    // ---- constructible and cached, so the backoff schedule, its jitter and its ceiling were entirely
    // ---- unbound. Nothing could have gone red when the jitter was a flat 0-100ms on a delay measured in
    // ---- seconds, which is why that shipped.

    [Fact]
    public void ScheduleTheUnchangedNominalLadder()
    {
        // 2s, 4s, 8s across the three retries, matching the Math.Pow(2, retryAttempt) seconds the provider
        // used to compute. Full jitter samples below the nominal, so the nominal is the upper bound.
        double[] nominalMs = [2_000d, 4_000d, 8_000d];

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var delay = SqlDataAccessPolicyFactory.RetrySchedule.CalculateDelay(attempt);

            delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
            delay.TotalMilliseconds.ShouldBeLessThanOrEqualTo(
                nominalMs[attempt - 1],
                $"attempt {attempt} must not exceed its nominal delay once jitter is applied below the cap");
        }
    }

    [Fact]
    public void ApplyJitterProportionalToTheDelay_NotAFlatHundredMilliseconds()
    {
        // THE RED-DETECTING ARM. The old provider returned 8s + [0, 100)ms at the third retry -- jitter of
        // 1.25%, so two hosts that failed together retried within a tenth of a second of each other every
        // time. Real jitter must reach the LOW end of the interval, which is the only property that
        // actually separates them.
        //
        // 400 draws against a pure calculator, so there is no clock and no I/O and the arm is not
        // probabilistic at this sample count; the old implementation fails it at every single draw.
        var half = TimeSpan.FromSeconds(4);
        var sawLowerHalf = false;

        for (var i = 0; i < 400 && !sawLowerHalf; i++)
        {
            sawLowerHalf = SqlDataAccessPolicyFactory.RetrySchedule.CalculateDelay(3) < half;
        }

        sawLowerHalf.ShouldBeTrue(
            "over 400 draws the third retry never fell below half its 8s nominal delay; a flat 100ms "
            + "jitter on a seconds-scale delay decorrelates nothing");
    }

    [Fact]
    public void CapTheScheduleWhereItPreviouslyHadNoCeilingAtAll()
    {
        // The old provider applied no maximum: Math.Pow(2, retryAttempt) grew without bound and was held
        // only by the retry count. If the budget were ever raised the schedule would have run away.
        for (var attempt = 1; attempt <= 40; attempt++)
        {
            SqlDataAccessPolicyFactory.RetrySchedule
                .CalculateDelay(attempt)
                .ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30), $"attempt {attempt} breached the ceiling");
        }
    }
}
