// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Calculates fixed delay for retry operations.
/// </summary>
/// <remarks> Returns the same delay for every retry attempt. Useful when the delay should be consistent regardless of failure count. </remarks>
public sealed class FixedBackoffCalculator : IBackoffCalculator
{
	private readonly TimeSpan _delay;

	/// <summary>
	/// Initializes a new instance of the <see cref="FixedBackoffCalculator" /> class.
	/// </summary>
	/// <param name="delay"> The fixed delay between retry attempts. </param>
	public FixedBackoffCalculator(TimeSpan delay)
	{
		if (delay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(delay),
				Resources.FixedBackoffCalculator_DelayCannotBeNegative);
		}

		_delay = delay;
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(attempt),
				Resources.FixedBackoffCalculator_AttemptMustBeAtLeastOne);
		}

		return _delay;
	}
}
