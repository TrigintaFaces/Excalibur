// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

/// <summary>
/// Locks the poll interval an operator configures to the value the drain loop and the failure-floor
/// validators actually read.
/// </summary>
/// <remarks>
/// <para>
/// The knob and the reader were two option types with no translation between them. The builder's
/// polling-interval verb lands on <c>OutboxOptions</c>; the drain loop and every provider's
/// failure-floor validator read <c>OutboxProcessingOptions</c>, which nothing bound, so it always
/// yielded its own five-second default.
/// </para>
/// <para>
/// The second arm is the one that matters. A silently-ignored poll interval is a performance surprise; a
/// validator holding <c>FailureBackoffFloorSeconds &gt; PollingInterval</c> against an interval the system
/// does not use is a gate that passes the exact configuration it exists to reject -- a floor beneath the
/// operator's real poll interval, which re-admits a failed message on the next cycle with no backoff.
/// </para>
/// <para>
/// Resolved from a real container built by the production registration, not from hand-constructed options:
/// the defect was entirely in the wiring, so options assembled by the test would have agreed with each
/// other and proved nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxPollingIntervalReachesTheDrainLoopShould
{
	private static ServiceProvider BuildProvider(TimeSpan operatorPoll, int? floorSeconds = null)
	{
		var services = new ServiceCollection();

		_ = services.AddExcaliburOutbox(outbox => outbox
			.UseInMemory()
			.WithProcessing(processing => processing.PollingInterval(operatorPoll)));

		if (floorSeconds is { } floor)
		{
			_ = services.Configure<InMemoryOutboxOptions>(o => o.FailureBackoffFloorSeconds = floor);
		}

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// LIVENESS: the interval the operator set is the interval the drain loop delays on.
	/// </summary>
	[Fact]
	public void OperatorPollingInterval_IsTheValueTheDrainLoopReads()
	{
		using var provider = BuildProvider(TimeSpan.FromMilliseconds(250));

		var loopValue = provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value.PollingInterval;

		loopValue.ShouldBe(
			TimeSpan.FromMilliseconds(250),
			"OutboxProcessingOptions.PollingInterval is what OutboxBackgroundService delays on. A five-second "
			+ "value here means the operator's setting was discarded between the builder and the loop.");
	}

	/// <summary>
	/// SAFETY: a backoff floor beneath the operator's real poll interval is refused at startup.
	/// </summary>
	/// <remarks>
	/// The floor (10s) sits above the type's own five-second default and below the operator's configured
	/// interval (60s), so this configuration is rejected only if the validator reads the operator's value.
	/// Against the unbound type it passed -- the arm is red on the defect and on nothing else.
	/// </remarks>
	[Fact]
	public async Task FloorBeneathTheOperatorPollingInterval_IsRefusedAtStartup()
	{
		using var provider = BuildProvider(TimeSpan.FromSeconds(60), floorSeconds: 10);

		var ex = await Should.ThrowAsync<OptionsValidationException>(
			() => Task.Run(() => provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>().Value));

		ex.Message.ShouldContain("FailureBackoffFloorSeconds");
	}

	/// <summary>
	/// LIVENESS control: a floor above the operator's interval still starts, so the arm above is not a
	/// validator that rejects everything.
	/// </summary>
	[Fact]
	public void FloorAboveTheOperatorPollingInterval_Starts()
	{
		using var provider = BuildProvider(TimeSpan.FromSeconds(5), floorSeconds: 30);

		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>().Value;

		options.FailureBackoffFloorSeconds.ShouldBe(30);
	}
    [Theory]
    [InlineData(-2d)]
    [InlineData(4294967295d)]
    public void RefuseUnsupportedFinalPollingTimeoutAtStartup(double milliseconds)
    {
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        Should.Throw<ArgumentOutOfRangeException>(() => Task.Delay(delay, new CancellationToken(true)));
        using var provider = BuildFinalOptionsProvider(options => options.PollingInterval = delay);
        AssertStartupRefuses(provider, nameof(OutboxProcessingOptions.PollingInterval));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void RefuseUnsupportedFinalDrainTimeoutAtStartup(int seconds)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => { using var unsupported = new CancellationTokenSource(TimeSpan.FromSeconds(seconds)); });
        using var provider = BuildFinalOptionsProvider(options => options.DrainTimeoutSeconds = seconds);
        AssertStartupRefuses(provider, nameof(OutboxProcessingOptions.DrainTimeoutSeconds));
    }

    [Fact]
    public void RefuseNegativeFinalRetryCountAtStartup()
    {
        using var provider = BuildFinalOptionsProvider(options => options.MaxRetries = -1);
        AssertStartupRefuses(provider, nameof(OutboxProcessingOptions.MaxRetries));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(-1.5d)]
    [InlineData(0.5d)]
    [InlineData(4294967294d)]
    public void PreserveNativeSupportedPollingAndImmediateDrainPolicies(double milliseconds)
    {
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        Task.Delay(delay, new CancellationToken(true)).IsCanceled.ShouldBeTrue();
        using var immediateDrain = new CancellationTokenSource(TimeSpan.Zero);
        immediateDrain.IsCancellationRequested.ShouldBeTrue();
        using var provider = BuildFinalOptionsProvider(options =>
        {
            options.PollingInterval = delay;
            options.DrainTimeoutSeconds = 0;
            options.MaxRetries = 0;
            options.Enabled = false;
        });
        provider.GetRequiredService<IStartupValidator>().Validate();
        var finalOptions = provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value;
        finalOptions.PollingInterval.ShouldBe(delay);
        finalOptions.DrainTimeoutSeconds.ShouldBe(0);
        finalOptions.MaxRetries.ShouldBe(0);
        finalOptions.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void KeepValidationRegistrationStableAcrossRepeatedOutboxRegistration()
    {
        var services = new ServiceCollection();
        services.AddExcaliburOutbox(outbox => outbox.UseInMemory());
        var validators = services.Count(descriptor => descriptor.ServiceType == typeof(IValidateOptions<OutboxProcessingOptions>));
        var startupValidators = services.Count(descriptor => descriptor.ServiceType == typeof(IStartupValidator));
        validators.ShouldBeGreaterThan(0);
        services.AddExcaliburOutbox(outbox => outbox.UseInMemory());
        services.Count(descriptor => descriptor.ServiceType == typeof(IValidateOptions<OutboxProcessingOptions>)).ShouldBe(validators);
        services.Count(descriptor => descriptor.ServiceType == typeof(IStartupValidator)).ShouldBe(startupValidators);
    }

    [Fact]
    public void ValidateAfterConsumerPostConfigurationAndAlongsideConsumerValidators()
    {
        var services = new ServiceCollection();
        services.AddOptions<OutboxProcessingOptions>().Validate(_ => true);
        services.AddExcaliburOutbox(outbox => outbox.UseInMemory());
        services.PostConfigure<OutboxProcessingOptions>(options => options.MaxRetries = -1);
        using var provider = services.BuildServiceProvider();
        AssertStartupRefuses(provider, nameof(OutboxProcessingOptions.MaxRetries));
    }
    private static void AssertStartupRefuses(ServiceProvider provider, string property)
    {
        Exception? failure = null;
        try
        {
            provider.GetRequiredService<IStartupValidator>().Validate();
        }
        catch (OptionsValidationException exception)
        {
            failure = exception;
        }
        catch (AggregateException exception)
        {
            failure = exception;
        }
        failure.ShouldNotBeNull();
        var errors = failure is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions.ToArray()
            : new[] { failure };
        errors.ShouldNotBeEmpty();
        foreach (var error in errors)
        {
            var validation = error.ShouldBeOfType<OptionsValidationException>();
            validation.OptionsType.ShouldBe(typeof(OutboxProcessingOptions));
            validation.Failures.ShouldContain(message => message.Contains(property, StringComparison.Ordinal));
        }
    }
    private static ServiceProvider BuildFinalOptionsProvider(Action<OutboxProcessingOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddExcaliburOutbox(outbox => outbox.UseInMemory()
            .WithProcessing(processing => processing.PollingInterval(TimeSpan.FromMilliseconds(250))));
        services.Configure<InMemoryOutboxOptions>(options => options.FailureBackoffFloorSeconds = int.MaxValue);
        services.Configure(configure);
        return services.BuildServiceProvider();
    }
}
