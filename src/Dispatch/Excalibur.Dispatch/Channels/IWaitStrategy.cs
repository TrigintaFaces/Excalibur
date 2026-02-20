// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Interface for channel wait strategies.
/// </summary>
public interface IWaitStrategy : IDisposable
{
	/// <summary>
	/// Waits for a condition to be met.
	/// </summary>
	/// <param name="condition"> The condition to wait for. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that completes when the condition is met. </returns>
	ValueTask<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken);
}
