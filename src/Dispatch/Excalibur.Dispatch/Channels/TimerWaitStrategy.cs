// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Timer-based wait strategy that uses a timer for delays.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TimerWaitStrategy" /> class. </remarks>
/// <param name="delayMilliseconds"> The delay in milliseconds between checks. </param>
public sealed class TimerWaitStrategy(int delayMilliseconds = 10) : WaitStrategyBase
{
	private readonly int _delayMilliseconds = delayMilliseconds > 0
		? delayMilliseconds
		: throw new ArgumentException(ErrorConstants.DelayMustBePositive, nameof(delayMilliseconds));

	/// <inheritdoc />
	public override async ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(condition);

		while (!condition() && !cancellationToken.IsCancellationRequested)
		{
			await Task.Delay(_delayMilliseconds, cancellationToken).ConfigureAwait(false);
		}

		return !cancellationToken.IsCancellationRequested;
	}
}
