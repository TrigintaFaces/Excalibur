// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Calculates linear backoff delays for retry operations.
/// </summary>
/// <remarks>
/// Increases the delay linearly with each attempt: delay = baseDelay * attempt. Provides a gentler increase than exponential backoff.
/// </remarks>
public sealed class LinearBackoffCalculator : IBackoffCalculator
{
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;

	/// <summary>
	/// Initializes a new instance of the <see cref="LinearBackoffCalculator" /> class.
	/// </summary>
	/// <param name="baseDelay"> The base delay, multiplied by the attempt number. </param>
	/// <param name="maxDelay"> The maximum delay cap. </param>
	public LinearBackoffCalculator(TimeSpan baseDelay, TimeSpan? maxDelay = null)
	{
		if (baseDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(baseDelay),
				Resources.LinearBackoffCalculator_BaseDelayMustBePositive);
		}

		_baseDelay = baseDelay;
		_maxDelay = maxDelay ?? TimeSpan.FromMinutes(30);

		if (_maxDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maxDelay),
				Resources.LinearBackoffCalculator_MaxDelayMustBePositive);
		}
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(attempt),
				Resources.LinearBackoffCalculator_AttemptMustBeAtLeastOne);
		}

		// Linear delay: baseDelay * attempt
		var delayMs = _baseDelay.TotalMilliseconds * attempt;

		// Clamp to max delay
		delayMs = Math.Min(delayMs, _maxDelay.TotalMilliseconds);

		return TimeSpan.FromMilliseconds(delayMs);
	}
}
