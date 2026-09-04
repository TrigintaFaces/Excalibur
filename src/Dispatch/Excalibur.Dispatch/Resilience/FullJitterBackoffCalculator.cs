// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Calculates retry delays using the AWS "Full Jitter" strategy:
/// <c>delay = random(0, min(maxDelay, baseDelay * multiplier^(attempt-1)))</c>.
/// </summary>
/// <remarks>
/// <para>
/// Full Jitter spreads retries uniformly across the entire <c>[0, exponential]</c> window rather than the
/// symmetric <c>±(delay × jitterFactor)</c> band used by <see cref="ExponentialBackoffCalculator"/> with jitter
/// enabled. This maximally decorrelates concurrent clients, giving the best protection against the
/// thundering-herd problem at the cost of occasionally very short delays.
/// See: https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/
/// </para>
/// <para>
/// The result is a pure function of <c>attempt</c> and the injected jitter source, so a seeded source makes
/// the delay deterministic for tests.
/// </para>
/// </remarks>
internal sealed class FullJitterBackoffCalculator : IBackoffCalculator
{
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly double _multiplier;
	private readonly Func<double> _jitterSource;

	/// <summary>
	/// Initializes a new instance of the <see cref="FullJitterBackoffCalculator"/> class with default options.
	/// </summary>
	public FullJitterBackoffCalculator()
		: this(new RetryPolicyOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FullJitterBackoffCalculator"/> class.
	/// </summary>
	/// <param name="options"> The retry policy options containing backoff configuration. </param>
	public FullJitterBackoffCalculator(RetryPolicyOptions options)
		: this(
			options?.Backoff.BaseDelay ?? TimeSpan.FromSeconds(1),
			options?.Backoff.MaxDelay ?? TimeSpan.FromMinutes(30),
			options?.Backoff.BackoffMultiplier ?? 2.0)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FullJitterBackoffCalculator"/> class with explicit parameters.
	/// </summary>
	/// <param name="baseDelay"> The base delay for the first retry. </param>
	/// <param name="maxDelay"> The maximum delay cap (also caps the random upper bound). </param>
	/// <param name="multiplier"> The multiplier for exponential growth (typically 2.0). </param>
	/// <param name="jitterSource">
	/// Optional source of jitter randomness producing values in <c>[0.0, 1.0)</c>. When <see langword="null"/>,
	/// <see cref="Random.Shared"/> is used. Inject a seeded/controllable source to make the delay deterministic
	/// for testing (a pure function of <c>attempt</c> and the seeded sequence).
	/// </param>
	public FullJitterBackoffCalculator(
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		double multiplier = 2.0,
		Func<double>? jitterSource = null)
	{
		if (baseDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(baseDelay), Resources.ExponentialBackoffCalculator_BaseDelayCannotBeNegative);
		}

		if (maxDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDelay), Resources.ExponentialBackoffCalculator_MaxDelayCannotBeNegative);
		}

		if (multiplier < 1.0)
		{
			throw new ArgumentOutOfRangeException(nameof(multiplier), Resources.ExponentialBackoffCalculator_MultiplierMustBeAtLeastOne);
		}

		_baseDelay = baseDelay;
		_maxDelay = maxDelay;
		_multiplier = multiplier;
#pragma warning disable CA5394 // Do not use insecure randomness - jitter does not require cryptographic security
		_jitterSource = jitterSource ?? Random.Shared.NextDouble;
#pragma warning restore CA5394
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(attempt), Resources.ExponentialBackoffCalculator_AttemptMustBeAtLeastOne);
		}

		// Exponential ceiling: baseDelay * multiplier^(attempt-1), capped at maxDelay.
		var exponentialFactor = Math.Pow(_multiplier, attempt - 1);
		var ceilingMs = _baseDelay.TotalMilliseconds * exponentialFactor;

		// Guard NaN/Infinity (0 * Infinity = NaN; +Infinity from overflow) before the cap so the never-throws
		// contract holds and TimeSpan.FromMilliseconds never sees a non-finite value.
		if (double.IsNaN(ceilingMs))
		{
			ceilingMs = 0;
		}

		ceilingMs = Math.Min(ceilingMs, _maxDelay.TotalMilliseconds);
		ceilingMs = Math.Max(0, ceilingMs);

		// Full Jitter: uniformly sample [0, ceiling]. The jitter source yields [0.0, 1.0).
		var delayMs = _jitterSource() * ceilingMs;

		return TimeSpan.FromMilliseconds(delayMs);
	}
}
