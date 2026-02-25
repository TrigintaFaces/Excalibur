// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Spin wait strategy for low-latency scenarios.
/// </summary>
public sealed class SpinWaitStrategy : WaitStrategyBase
{
	private SpinWait _spinWait;

	/// <inheritdoc />
	public override async ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(condition);

		while (!condition() && !cancellationToken.IsCancellationRequested)
		{
			_spinWait.SpinOnce();

			// Yield periodically to prevent hogging the CPU
			if (_spinWait.Count % 100 == 0)
			{
				await Task.Yield();
			}
		}

		return !cancellationToken.IsCancellationRequested;
	}

	/// <inheritdoc />
	public override void Reset() => _spinWait.Reset();
}
