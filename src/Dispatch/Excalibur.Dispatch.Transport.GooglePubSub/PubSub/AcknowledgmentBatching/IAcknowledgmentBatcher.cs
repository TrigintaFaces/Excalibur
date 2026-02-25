// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines the contract for batching acknowledgments to optimize API calls.
/// </summary>
public interface IAcknowledgmentBatcher : IAsyncDisposable
{
	/// <summary>
	/// Adds an acknowledgment ID to the batch.
	/// </summary>
	/// <param name="ackId"> The acknowledgment ID. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the acknowledgment operation. </returns>
	ValueTask AddAcknowledgmentAsync(string ackId, CancellationToken cancellationToken);

	/// <summary>
	/// Adds multiple acknowledgment IDs to the batch.
	/// </summary>
	/// <param name="ackIds"> The acknowledgment IDs. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the acknowledgment operation. </returns>
	ValueTask AddAcknowledgmentsAsync(IEnumerable<string> ackIds, CancellationToken cancellationToken);

	/// <summary>
	/// Flushes any pending acknowledgments immediately.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the flush operation. </returns>
	ValueTask FlushAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current metrics for the acknowledgment batcher.
	/// </summary>
	/// <returns> The current metrics. </returns>
	AcknowledgmentMetrics GetMetrics();
}
