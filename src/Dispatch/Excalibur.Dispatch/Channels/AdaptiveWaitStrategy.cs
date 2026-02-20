// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Adaptive wait strategy that adjusts its behavior based on contention.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AdaptiveWaitStrategy" /> class. </remarks>
/// <param name="maxSpinCount"> The maximum number of spin iterations before yielding. </param>
/// <param name="contentionThreshold"> The threshold for switching to less aggressive waiting. </param>
public sealed class AdaptiveWaitStrategy(int maxSpinCount = 100, int contentionThreshold = 10) : WaitStrategyBase
{
	private readonly int _maxSpinCount = maxSpinCount > 0
		? maxSpinCount
		: throw new ArgumentException(ErrorConstants.MaxSpinCountMustBePositive, nameof(maxSpinCount));

	private readonly int _contentionThreshold = contentionThreshold > 0
		? contentionThreshold
		: throw new ArgumentException(ErrorConstants.ContentionThresholdMustBePositive, nameof(contentionThreshold));

	private int _contentionCount;
	private SpinWait _spinWait;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override async ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(condition);

		_spinWait.Reset();
		var spinCount = 0;
		var useYield = _contentionCount > _contentionThreshold;

		try
		{
			// First, try spinning if contention is low
			if (!useYield)
			{
				while (spinCount < _maxSpinCount && !condition() && !cancellationToken.IsCancellationRequested)
				{
					_spinWait.SpinOnce(sleep1Threshold: -1);
					spinCount++;
				}

				if (condition())
				{
					// Success with spinning - reduce contention count
					if (_contentionCount > 0)
					{
						_ = Interlocked.Decrement(ref _contentionCount);
					}

					return true;
				}
			}

			// If still not ready, indicate contention and use yielding
			_ = Interlocked.Increment(ref _contentionCount);

			while (!condition() && !cancellationToken.IsCancellationRequested)
			{
				await Task.Yield();
			}

			return !cancellationToken.IsCancellationRequested;
		}
		finally
		{
			// Adjust contention based on outcome
			if (spinCount >= _maxSpinCount && _contentionCount < _contentionThreshold * 2)
			{
				_ = Interlocked.Increment(ref _contentionCount);
			}
		}
	}

	/// <inheritdoc />
	public override void Reset()
	{
		_contentionCount = 0;
		_spinWait.Reset();
	}
}
