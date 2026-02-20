// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Hybrid wait strategy that combines spinning and timer-based waiting.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="HybridWaitStrategy" /> class. </remarks>
/// <param name="spinCount"> The number of times to spin before using a timer. </param>
/// <param name="delayMilliseconds"> The delay in milliseconds when using a timer. </param>
public sealed class HybridWaitStrategy(int spinCount = 10, int delayMilliseconds = 1) : WaitStrategyBase
{
	private readonly int _spinCount = spinCount > 0
		? spinCount
		: throw new ArgumentException(ErrorConstants.SpinCountMustBePositive, nameof(spinCount));

	private readonly int _delayMilliseconds = delayMilliseconds > 0
		? delayMilliseconds
		: throw new ArgumentException(ErrorConstants.DelayMustBePositive, nameof(delayMilliseconds));

	/// <summary>
	/// Changed from readonly to reset-able.
	/// </summary>
	private SpinWait _spinWait;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override async ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(condition);

		_spinWait.Reset(); // Reset SpinWait for new operation

		// First, try spinning for a short time
		for (var i = 0; i < _spinCount; i++)
		{
			if (condition())
			{
				return true;
			}

			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			_spinWait.SpinOnce(sleep1Threshold: -1); // Never yield to OS sleep
		}

		// If still not ready, use timer-based waiting
		while (!condition() && !cancellationToken.IsCancellationRequested)
		{
			await Task.Delay(_delayMilliseconds, cancellationToken).ConfigureAwait(false);
		}

		return !cancellationToken.IsCancellationRequested;
	}

	/// <inheritdoc />
	public override void Reset() => _spinWait.Reset();
}
