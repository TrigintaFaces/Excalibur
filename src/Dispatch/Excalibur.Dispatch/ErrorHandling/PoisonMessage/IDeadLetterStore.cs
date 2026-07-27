// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Defines the contract for storing and retrieving messages from the dead letter queue.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Tenancy.</strong> This interface is <em>tenant-scoped</em>: every operation addresses only
/// entries belonging to the ambient tenant, and an entry stored under a different tenant is not visible
/// through it. That guarantee matters here specifically because the entries carry
/// <see cref="DeadLetterMessage.MessageBody"/> — the failed message content — so an estate-wide result
/// discloses one tenant's message content to another.
/// </para>
/// <para>
/// <strong>Implementers must enforce this.</strong> The ambient tenant is supplied by the registered
/// tenant context; a host that registers none operates entirely under the reserved untenanted partition,
/// which is a concrete partition like any other rather than an absence of scoping. An implementation that
/// ignores the ambient tenant satisfies the method signatures while breaking the contract, and no
/// signature here can prevent that — which is why implementations are expected to demonstrate isolation
/// against the provided conformance suite rather than assert it.
/// </para>
/// <para>
/// The scoping applies to writes and deletes as well as reads: a caller must not be able to mark
/// replayed, delete, or purge an entry belonging to another tenant. Scoping only the read paths turns a
/// disclosure into silent data loss rather than removing the problem.
/// </para>
/// </remarks>
public interface IDeadLetterStore
{
	/// <summary>
	/// Stores a message in the dead letter queue.
	/// </summary>
	/// <param name="message"> The dead letter message to store. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves a dead letter message by its ID.
	/// </summary>
	/// <param name="messageId"> The ID of the message to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the dead letter message, or null if not found. </returns>
	Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves dead letter messages based on filter criteria.
	/// </summary>
	/// <param name="filter"> The filter criteria for retrieving messages. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the collection of matching dead letter messages. </returns>
	Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
		DeadLetterFilter filter,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a dead letter message as replayed.
	/// </summary>
	/// <param name="messageId"> The ID of the message that was replayed. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a dead letter message.
	/// </summary>
	/// <param name="messageId"> The ID of the message to delete. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation with a boolean indicating success. </returns>
	Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken);

}

/// <summary>
/// Provides administrative operations for the dead letter store.
/// </summary>
public interface IDeadLetterStoreAdmin
{
	/// <summary>Gets the count of messages in the dead letter queue.</summary>
	Task<long> GetCountAsync(CancellationToken cancellationToken);

	/// <summary>Cleans up old dead letter messages based on retention policy.</summary>
	Task<int> CleanupOldMessagesAsync(int retentionDays, CancellationToken cancellationToken);
}
