// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Binds the settings that actually govern when the ratio-based circuit breaker opens. Before these
/// locks the failure proportion and the sampling window were fixed in the adapter, so a caller could
/// configure a threshold, receive no error, and get a circuit governed by values it never chose.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class PollyCircuitBreakerHonoursSuppliedOptionsShould : UnitTestBase
{
	private static CircuitBreakerOptions Options(
		int failureThreshold = 5,
		double? failureRatio = null,
		TimeSpan? samplingDuration = null) =>
		new()
		{
			FailureThreshold = failureThreshold,
			FailureRatio = failureRatio ?? 0.5,
			SamplingDuration = samplingDuration ?? TimeSpan.FromSeconds(30),
			OpenDuration = TimeSpan.FromSeconds(30),
			OperationTimeout = TimeSpan.FromSeconds(5),
		};

	private static async Task<bool> IsOpenAsync(PollyCircuitBreakerAdapter adapter)
	{
		try
		{
			_ = await adapter.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);
			return false;
		}
		catch (CircuitBreakerOpenException)
		{
			return true;
		}
	}

	private static async Task RunAsync(PollyCircuitBreakerAdapter adapter, int successes, int failures)
	{
		for (var i = 0; i < successes; i++)
		{
			try { _ = await adapter.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None); }
			catch (CircuitBreakerOpenException) { }
		}

		for (var i = 0; i < failures; i++)
		{
			try { _ = await adapter.ExecuteAsync<int>(() => throw new InvalidOperationException("induced"), CancellationToken.None); }
			catch (InvalidOperationException) { }
			catch (CircuitBreakerOpenException) { }
		}
	}

	/// <summary>
	/// SAFETY. Identical traffic, two different requested proportions, opposite outcomes. The proportion
	/// was fixed at 0.5 inside the adapter, so both of these cases used to open and the caller's choice
	/// had no effect on anything.
	/// </summary>
	[Fact]
	public async Task Open_a_sixty_percent_failure_window_at_the_default_proportion()
	{
		await using var adapter = new PollyCircuitBreakerAdapter("ratio-half", Options(failureRatio: 0.5));

		await RunAsync(adapter, successes: 4, failures: 6);

		(await IsOpenAsync(adapter)).ShouldBeTrue("60% of the window failed, at or above the requested 0.5");
	}

	/// <summary>
	/// SAFETY, the discriminating half. The same traffic must leave the circuit closed when the caller
	/// asked for a proportion the window never reaches. This is the arm that fails if the adapter goes
	/// back to applying a constant.
	/// </summary>
	[Fact]
	public async Task Leave_the_same_sixty_percent_window_closed_at_a_higher_requested_proportion()
	{
		await using var adapter = new PollyCircuitBreakerAdapter("ratio-high", Options(failureRatio: 0.75));

		await RunAsync(adapter, successes: 4, failures: 6);

		(await IsOpenAsync(adapter)).ShouldBeFalse(
			"60% of the window failed, below the requested 0.75, so the circuit must stay closed");
	}

	/// <summary>LIVENESS. A healthy caller is not tripped by the new wiring.</summary>
	[Fact]
	public async Task Stay_closed_while_every_call_succeeds()
	{
		await using var adapter = new PollyCircuitBreakerAdapter("healthy", Options(failureRatio: 0.5));

		await RunAsync(adapter, successes: 20, failures: 0);

		(await IsOpenAsync(adapter)).ShouldBeFalse("no call failed, so the circuit must remain closed");
	}

	/// <summary>
	/// SAFETY. The reported configuration names the values actually applied to the pipeline. An instrument
	/// that omits the two settings governing the breaker cannot be used to confirm what is in force.
	/// </summary>
	[Fact]
	public async Task Report_the_governing_settings_it_actually_applied()
	{
		var options = Options(failureRatio: 0.75, samplingDuration: TimeSpan.FromSeconds(12));
		await using var adapter = new PollyCircuitBreakerAdapter("reported", options);

		adapter.Configuration[nameof(CircuitBreakerOptions.FailureRatio)].ShouldBe(0.75);
		adapter.Configuration[nameof(CircuitBreakerOptions.SamplingDuration)].ShouldBe(TimeSpan.FromSeconds(12));
	}

	/// <summary>
	/// SAFETY. A threshold this provider cannot express is refused by name. Previously the underlying
	/// library rejected it with a message naming only its own strategy type, so the caller was told a
	/// configuration was invalid without being told which of their settings caused it.
	/// </summary>
	[Fact]
	public void Refuse_a_failure_threshold_below_the_two_calls_a_proportion_needs()
	{
		var act = () => new PollyCircuitBreakerAdapter("too-low", Options(failureThreshold: 1));

		var ex = Should.Throw<ArgumentOutOfRangeException>(act);
		ex.Message.ShouldContain(nameof(CircuitBreakerOptions.FailureThreshold));
		ex.Message.ShouldContain("too-low");
	}

	/// <summary>LIVENESS. The lowest threshold the provider can express is accepted, so the guard is not a blanket refusal.</summary>
	[Fact]
	public async Task Accept_the_lowest_failure_threshold_the_provider_can_express()
	{
		await using var adapter = new PollyCircuitBreakerAdapter("lowest", Options(failureThreshold: 2));

		adapter.Configuration[nameof(CircuitBreakerOptions.FailureThreshold)].ShouldBe(2);
	}

	/// <summary>
	/// SAFETY. Startup validation rejects a proportion that could never govern anything, so the failure
	/// arrives at configuration time rather than as a circuit that behaves unexpectedly in production.
	/// </summary>
	[Theory]
	[InlineData(0.0)]
	[InlineData(-0.1)]
	[InlineData(1.5)]
	public void Reject_a_failure_proportion_outside_the_usable_range(double failureRatio)
	{
		var result = new CircuitBreakerOptionsValidator().Validate(null, Options(failureRatio: failureRatio));

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.FailureRatio));
	}

	/// <summary>SAFETY. A sampling window shorter than a ratio-based provider accepts is caught at startup, not at first use.</summary>
	[Fact]
	public void Reject_a_sampling_window_shorter_than_the_provider_accepts()
	{
		var result = new CircuitBreakerOptionsValidator()
			.Validate(null, Options(samplingDuration: TimeSpan.FromMilliseconds(100)));

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.SamplingDuration));
	}

	/// <summary>LIVENESS. The shipped defaults validate, so the new arms are not a blanket rejection.</summary>
	[Fact]
	public void Accept_the_shipped_defaults()
	{
		new CircuitBreakerOptionsValidator().Validate(null, new CircuitBreakerOptions())
			.Succeeded.ShouldBeTrue("a caller who configures nothing must still start");
	}

	/// <summary>SAFETY. The policy adapter shares the guard, so neither ratio-based entry point silently reinterprets the threshold.</summary>
	[Fact]
	public void Refuse_the_same_threshold_on_the_policy_adapter()
	{
		var act = () => new PollyCircuitBreakerPolicyAdapter(Options(failureThreshold: 1), "policy-too-low");

		Should.Throw<ArgumentOutOfRangeException>(act)
			.Message.ShouldContain(nameof(CircuitBreakerOptions.FailureThreshold));
	}
}
