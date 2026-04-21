// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Defines lifecycle operations for a long polling receiver.
/// </summary>
public interface ILongPollingReceiverLifecycle
{
	/// <summary>
	/// Gets the current polling status.
	/// </summary>
	SqsPollingStatus Status { get; }

	/// <summary>
	/// Starts the receiver.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops the receiver.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StopAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the receiver's operations.
	/// </summary>
	/// <returns>A <see cref="ValueTask{T}"/> representing the asynchronous operation, containing the receiver statistics.</returns>
	ValueTask<ReceiverStatistics> GetStatisticsAsync();
}
