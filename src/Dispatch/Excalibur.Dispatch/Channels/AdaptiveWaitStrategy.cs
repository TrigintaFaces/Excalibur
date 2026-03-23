// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Adaptive wait strategy that adjusts its behavior based on contention.
/// Uses exponential backoff when spinning is insufficient, avoiding CPU saturation
/// from tight Task.Yield loops under sustained contention.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AdaptiveWaitStrategy" /> class. </remarks>
/// <param name="maxSpinCount"> The maximum number of spin iterations before yielding. </param>
/// <param name="contentionThreshold"> The threshold for switching to less aggressive waiting. </param>
internal sealed class AdaptiveWaitStrategy(int maxSpinCount = 100, int contentionThreshold = 10) : WaitStrategyBase
{
	/// <summary>
	/// Initial delay for exponential backoff after spinning is exhausted.
	/// </summary>
	private const int InitialBackoffMs = 1;

	/// <summary>
	/// Maximum delay for exponential backoff to prevent excessive latency.
	/// </summary>
	private const int MaxBackoffMs = 64;

	private readonly int _maxSpinCount = maxSpinCount > 0
		? maxSpinCount
		: throw new ArgumentException(ErrorConstants.MaxSpinCountMustBePositive, nameof(maxSpinCount));

	private readonly int _contentionThreshold = contentionThreshold > 0
		? contentionThreshold
		: throw new ArgumentException(ErrorConstants.ContentionThresholdMustBePositive, nameof(contentionThreshold));

	private int _contentionCount;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override async ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(condition);

		var spinWait = new SpinWait();
		var spinCount = 0;
		var useYield = _contentionCount > _contentionThreshold;

		try
		{
			// First, try spinning if contention is low
			if (!useYield)
			{
				while (spinCount < _maxSpinCount && !condition() && !cancellationToken.IsCancellationRequested)
				{
					spinWait.SpinOnce(sleep1Threshold: -1);
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

			// If still not ready, indicate contention and use exponential backoff
			// instead of a tight Task.Yield loop that saturates the CPU.
			_ = Interlocked.Increment(ref _contentionCount);

			var backoffMs = InitialBackoffMs;
			while (!condition() && !cancellationToken.IsCancellationRequested)
			{
				try
				{
					await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					break;
				}

				backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
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
		Interlocked.Exchange(ref _contentionCount, 0);
	}
}
