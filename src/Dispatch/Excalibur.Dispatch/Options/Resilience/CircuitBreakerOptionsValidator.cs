// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Validates <see cref="CircuitBreakerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks for circuit breaker configuration.
/// </remarks>
public sealed class CircuitBreakerOptionsValidator : IValidateOptions<CircuitBreakerOptions>
{
	/// <summary>The shortest rolling window a ratio-based circuit breaker provider accepts.</summary>
	private static readonly TimeSpan MinimumSamplingDuration = TimeSpan.FromMilliseconds(500);

	/// <summary>The fewest observed calls over which a failure ratio can be evaluated.</summary>
	private const int MinimumObservableCalls = 2;

	/// <summary>The shortest window the resilience provider accepts for a sampling or break duration.</summary>
	private static readonly TimeSpan PollyMinimumWindow = TimeSpan.FromMilliseconds(500);

	/// <summary>The longest window the resilience provider accepts for a sampling or break duration.</summary>
	private static readonly TimeSpan PollyMaximumWindow = TimeSpan.FromDays(1);

	/// <summary>The shortest per-operation timeout the resilience provider accepts.</summary>
	private static readonly TimeSpan PollyMinimumTimeout = TimeSpan.FromMilliseconds(10);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CircuitBreakerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// A consecutive-failure run of at least one is the smallest meaningful trigger.
		if (options.ConsecutiveFailureThreshold < 1)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.ConsecutiveFailureThreshold)} must be >= 1 " +
				$"(was {options.ConsecutiveFailureThreshold}).");
		}

		// A ratio over fewer than two observed calls is not a ratio. This is checked HERE, at startup,
		// rather than by a provider at first use: a provider that narrows a range the options type
		// admits has strengthened a precondition its callers cannot see, and the failure then lands on
		// the first message through that transport instead of on configuration.
		if (options.MinimumThroughput < MinimumObservableCalls)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.MinimumThroughput)} must be >= {MinimumObservableCalls} " +
				$"(was {options.MinimumThroughput}). A failure ratio cannot be evaluated over fewer " +
				$"observed calls; to open on a single failure use " +
				$"{nameof(CircuitBreakerOptions.ConsecutiveFailureThreshold)} instead.");
		}

		// FailureRatio must be a usable proportion. Zero would open the circuit on an empty window.
		// NOTE this is STRICTER than the provider, which accepts 0.0 -- it is our constraint, not Polly's.
		if (options.FailureRatio is <= 0.0 or > 1.0)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.FailureRatio)} must be greater than 0 and at most 1 " +
				$"(was {options.FailureRatio.ToString(CultureInfo.InvariantCulture)}).");
		}

		// SamplingDuration is a rolling window; ratio-based providers require at least 500ms.
		if (options.SamplingDuration < MinimumSamplingDuration)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.SamplingDuration)} must be at least {MinimumSamplingDuration} " +
				$"(was {options.SamplingDuration}).");
		}

		// Positivity is not the real bound. These values are forwarded verbatim into Polly's
		// CircuitBreakerStrategyOptions / TimeoutStrategyOptions, which validate them again with narrower
		// ranges -- so anything this validator admits but Polly does not produces a startup that passes
		// our own checks and then throws from inside the provider at pipeline construction. The options
		// type must not admit a value the provider cannot honour.
		//
		// Bounds read from Polly 8.6.6's own [Range] attributes, not from memory:
		//   CircuitBreakerStrategyOptions.SamplingDuration   00:00:00.500 .. 1.00:00:00
		//   CircuitBreakerStrategyOptions.BreakDuration      00:00:00.500 .. 1.00:00:00
		//   TimeoutStrategyOptions.Timeout                   00:00:00.010 .. 1.00:00:00
		// MinimumThroughput already matches Polly exactly and is checked above.
		//
		// FailureRatio does NOT, and the distinction matters: Polly's range is [0, 1] and accepts 0.0,
		// while the check above rejects it. That rejection is defensible on its own merits -- a ratio of
		// zero opens the circuit on a window of pure success -- but it is OUR invariant, not the
		// provider's, and a reader who takes it for a provider constraint will not know it is ours to
		// relax. The bounds listed below ARE the provider's, verbatim.
		if (options.SamplingDuration < PollyMinimumWindow || options.SamplingDuration > PollyMaximumWindow)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.SamplingDuration)} must be between {PollyMinimumWindow} and " +
				$"{PollyMaximumWindow} (was {options.SamplingDuration}). The resilience provider enforces this " +
				"range and would reject the value when the pipeline is built.");
		}

		if (options.BreakDuration < PollyMinimumWindow || options.BreakDuration > PollyMaximumWindow)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.BreakDuration)} must be between {PollyMinimumWindow} and " +
				$"{PollyMaximumWindow} (was {options.BreakDuration}). The resilience provider enforces this " +
				"range and would reject the value when the pipeline is built.");
		}

		// Same reasoning as the windows above: this value becomes TimeoutStrategyOptions.Timeout, whose
		// own lower bound is 10ms. "Positive" admitted 1ms, which we accepted and the provider did not.
		if (options.OperationTimeout < PollyMinimumTimeout || options.OperationTimeout > PollyMaximumWindow)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.OperationTimeout)} must be between {PollyMinimumTimeout} and " +
				$"{PollyMaximumWindow} (was {options.OperationTimeout}). The resilience provider enforces this " +
				"range and would reject the value when the pipeline is built.");
		}

		// Cross-property: OperationTimeout should not exceed BreakDuration (operations would always timeout during probe)
		if (options.OperationTimeout >= options.BreakDuration)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.OperationTimeout)} ({options.OperationTimeout}) should be less than " +
				$"{nameof(CircuitBreakerOptions.BreakDuration)} ({options.BreakDuration}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
