// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Task-based wait strategy that uses Task.Yield.
/// </summary>
public sealed class YieldWaitStrategy : WaitStrategyBase
{
	/// <inheritdoc />
	public override async ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(condition);

		while (!condition() && !cancellationToken.IsCancellationRequested)
		{
			await Task.Yield();
		}

		return !cancellationToken.IsCancellationRequested;
	}
}
