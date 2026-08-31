// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Manages dead letter queue operations for failed messages.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope: estate-wide, by construction.</b> Every operation here addresses a broker entity -- a topic, a
/// queue, a subscription -- named by the transport's own options, not a set of rows selected by a predicate.
/// There is no tenant discriminator to apply and none is applied. When several tenants share one transport
/// entity, every operation on this interface observes and modifies all of their messages together.
/// </para>
/// <para>
/// This is stated rather than left to be inferred, because the persistence-side dead-letter contract nearby
/// says the opposite: a dead-letter <em>store</em> confines every read and every delete to the ambient
/// tenant. A reader who carries that expectation across to this interface will be wrong, and wrong in the
/// destructive direction.
/// </para>
/// <para>
/// The consequence for a multi-tenant host is a deployment decision, not a call-site one: give each tenant
/// its own dead-letter entity, or accept that dead-letter administration is an operator-level activity that
/// spans the estate. No argument on these methods can narrow it.
/// </para>
/// </remarks>
public interface IDeadLetterQueueManager
{
	/// <summary>
	/// Moves a message to the dead letter queue.
	/// </summary>
	/// <param name="message"> The message to move to DLQ. </param>
	/// <param name="reason"> The reason for dead lettering. </param>
	/// <param name="exception"> Optional exception that caused the failure. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The ID of the message in the DLQ. </returns>
	Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves messages from the dead letter entity, across every tenant sharing it.
	/// </summary>
	/// <param name="maxMessages"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A list of dead letter messages, from every tenant sharing the entity. </returns>
	/// <remarks>
	/// The returned messages carry their bodies, so on a shared entity this reads one tenant's failed
	/// message content into a caller acting for another. It is the widest disclosure on this interface.
	/// </remarks>
	Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reprocesses messages from the dead letter queue.
	/// </summary>
	/// <param name="messages"> Messages to reprocess. </param>
	/// <param name="options"> Reprocessing options. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The result of the reprocessing operation. </returns>
	Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the dead letter queue.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Dead letter queue statistics. </returns>
	Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Purges every message from the dead letter entity, across every tenant sharing it.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The number of messages purged, across every tenant. </returns>
	/// <remarks>
	/// Destructive and unbounded: it empties the configured entity. Nothing is selected, so nothing is
	/// spared -- a message another tenant has not yet reprocessed is discarded along with the rest, and a
	/// dead-lettered message is the only remaining copy. Reprocess what is worth keeping, with
	/// <see cref="ReprocessDeadLetterMessagesAsync" />, before calling this.
	/// </remarks>
	Task<int> PurgeAllTenantsDeadLetterQueueAsync(
		CancellationToken cancellationToken);
}
