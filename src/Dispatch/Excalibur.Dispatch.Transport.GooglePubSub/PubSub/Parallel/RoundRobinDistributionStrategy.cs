// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Round-robin work distribution strategy.
/// </summary>
public sealed class RoundRobinDistributionStrategy : IWorkDistributionStrategy
{
	private int _nextWorker;

	/// <inheritdoc />
	public int SelectWorker(WorkDistributionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var worker = Interlocked.Increment(ref _nextWorker) % context.TotalWorkers;
		return Math.Abs(worker);
	}

	/// <inheritdoc />
	public void RecordCompletion(int workerId, TimeSpan duration)
	{
		// Round-robin doesn't need completion tracking
	}
}
