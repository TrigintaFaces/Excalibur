// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Computes geometrically growing retry delays: <c>min(baseDelay * multiplier^(attempt-1), maxDelay)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This calculator no longer applies jitter, and that is the point.</b> It used to carry an
/// <c>enableJitter</c> flag and a <c>jitterFactor</c>, which made it one of four places jitter was
/// implemented and the reason the strategy enum needed a separate <c>ExponentialWithJitter</c> member.
/// Randomisation now belongs to <see cref="JitteredBackoffCalculator" />, which composes over this
/// calculator -- so this type computes one thing, and the growth law and the randomness are selected
/// independently.
/// </para>
/// <para>
/// The arithmetic itself is delegated to <see cref="ExponentialBackoff" />, the shared pure function the
/// transports call directly. Keeping a second copy here is what previously justified this class owning a
/// jitter source of its own; with jitter gone there is nothing left to duplicate.
/// </para>
/// </remarks>
internal sealed class ExponentialBackoffCalculator : IBackoffCalculator
{
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly double _multiplier;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class with default options.
	/// </summary>
	public ExponentialBackoffCalculator()
		: this(new RetryOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class.
	/// </summary>
	/// <param name="options"> The retry options containing backoff configuration. </param>
	public ExponentialBackoffCalculator(RetryOptions options)
		: this(
			options?.BaseDelay ?? TimeSpan.FromSeconds(1),
			options?.MaxDelay ?? TimeSpan.FromMinutes(30),
			options?.BackoffMultiplier ?? 2.0)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class with explicit parameters.
	/// </summary>
	/// <param name="baseDelay"> The base delay for the first retry. </param>
	/// <param name="maxDelay"> The maximum delay cap. </param>
	/// <param name="multiplier"> The multiplier for exponential growth (typically 2.0). </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="baseDelay" /> or <paramref name="maxDelay" /> is negative, or
	/// <paramref name="multiplier" /> is less than 1.
	/// </exception>
	public ExponentialBackoffCalculator(TimeSpan baseDelay, TimeSpan maxDelay, double multiplier = 2.0)
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
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		// Validated HERE rather than left to the shared helper, which clamps a non-positive attempt
		// instead of refusing it. The interface contract requires a throw, and a clamp would turn a
		// caller's off-by-one into a silent zero delay.
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(attempt), Resources.ExponentialBackoffCalculator_AttemptMustBeAtLeastOne);
		}

		var parameters = new BackoffParameters
		{
			BaseDelay = _baseDelay,
			MaxDelay = _maxDelay,
			Multiplier = _multiplier,
			UseJitter = false,
		};

		return ExponentialBackoff.Calculate(attempt, in parameters);
	}

	/// <summary>
	/// Creates a calculator with settings suited to message-queue retries.
	/// </summary>
	/// <returns>
	/// A jittered exponential calculator: one second base, five minute ceiling, doubling, with full
	/// jitter applied.
	/// </returns>
	/// <remarks>
	/// Returns <see cref="IBackoffCalculator" /> rather than this type because the jitter it has always
	/// applied is now a decorator rather than a flag. The delay sequence it produces is the one a durable
	/// queue wants: growing, capped, and decorrelated across competing consumers.
	/// </remarks>
	public static IBackoffCalculator CreateForMessageQueue() =>
		new JitteredBackoffCalculator(
			new ExponentialBackoffCalculator(
				baseDelay: TimeSpan.FromSeconds(1),
				maxDelay: TimeSpan.FromMinutes(5),
				multiplier: 2.0));
}
