// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Calculates retry delays using AWS "Decorrelated Jitter":
/// <c>delay = min(maxDelay, random(baseDelay, previousDelay * 3))</c>, where the next delay is a function of
/// the <b>previous actual delay</b> rather than of the attempt number alone.
/// </summary>
/// <remarks>
/// <para>
/// Decorrelated jitter threads the previously-computed delay forward, producing a smoother, less-correlated
/// growth than full jitter while still spreading concurrent clients to avoid the thundering-herd problem.
/// Because the delay depends on prior state, this calculator is <b>stateful</b> and is intended for the
/// <b>in-process</b> retry path (e.g. the retry middleware / Polly pipeline) where that state legitimately
/// lives within a single process. Durable retry paths (outbox/inbox schedulable stores) use an
/// attempt-derived calculator (e.g. <see cref="FullJitterBackoffCalculator"/>) so restart-survival is
/// structural — decorrelated jitter is deliberately not persisted across restarts.
/// </para>
/// <para>
/// State is reset at the start of each retry sequence (<c>attempt == 1</c>), so a single instance can be
/// reused across sequences. Access is guarded by a lock, so the calculator is safe under concurrent use.
/// </para>
/// </remarks>
internal sealed class DecorrelatedJitterBackoffCalculator : IBackoffCalculator
{
	private const double GrowthFactor = 3.0;

	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly Func<double> _jitterSource;
	private readonly System.Threading.Lock _lock = new();
	private TimeSpan _previousDelay;

	/// <summary>
	/// Initializes a new instance of the <see cref="DecorrelatedJitterBackoffCalculator"/> class with default options.
	/// </summary>
	public DecorrelatedJitterBackoffCalculator()
		: this(new RetryPolicyOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DecorrelatedJitterBackoffCalculator"/> class.
	/// </summary>
	/// <param name="options"> The retry policy options containing backoff configuration. </param>
	public DecorrelatedJitterBackoffCalculator(RetryPolicyOptions options)
		: this(
			options?.Backoff.BaseDelay ?? TimeSpan.FromSeconds(1),
			options?.Backoff.MaxDelay ?? TimeSpan.FromMinutes(30))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DecorrelatedJitterBackoffCalculator"/> class with explicit parameters.
	/// </summary>
	/// <param name="baseDelay"> The base delay and the lower bound of each sampled delay. </param>
	/// <param name="maxDelay"> The maximum delay cap. </param>
	/// <param name="jitterSource">
	/// Optional source of jitter randomness producing values in <c>[0.0, 1.0)</c>. When <see langword="null"/>,
	/// <see cref="Random.Shared"/> is used. Inject a seeded/controllable source to make the sequence
	/// deterministic for testing.
	/// </param>
	public DecorrelatedJitterBackoffCalculator(
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		Func<double>? jitterSource = null)
	{
		if (baseDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(baseDelay), Resources.ExponentialBackoffCalculator_BaseDelayMustBePositive);
		}

		if (maxDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDelay), Resources.ExponentialBackoffCalculator_MaxDelayMustBePositive);
		}

		_baseDelay = baseDelay;
		_maxDelay = maxDelay;
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

		lock (_lock)
		{
			// A new retry sequence resets the threaded state so a reused instance starts from base.
			var previousMs = attempt == 1 ? _baseDelay.TotalMilliseconds : _previousDelay.TotalMilliseconds;
			if (previousMs <= 0)
			{
				previousMs = _baseDelay.TotalMilliseconds;
			}

			// Decorrelated jitter: sample uniformly from [base, previous * 3], capped at maxDelay.
			var upper = previousMs * GrowthFactor;
			var lower = _baseDelay.TotalMilliseconds;
			var sampledMs = lower + (_jitterSource() * (upper - lower));

			var cappedMs = Math.Min(sampledMs, _maxDelay.TotalMilliseconds);
			cappedMs = Math.Max(cappedMs, lower);

			var result = TimeSpan.FromMilliseconds(cappedMs);
			_previousDelay = result;
			return result;
		}
	}
}
