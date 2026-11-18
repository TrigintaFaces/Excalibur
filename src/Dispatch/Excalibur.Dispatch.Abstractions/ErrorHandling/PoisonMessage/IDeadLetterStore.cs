// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
/// <para>
/// <strong>Optional capabilities are discovered through <see cref="IServiceProvider.GetService(Type)"/>,
/// never by casting the store.</strong> A store answers for the capability interfaces it implements and
/// returns <see langword="null"/> for the rest. A decorator answers for what it adds and defers everything
/// else to the store it wraps, so decoration cannot silently drop a capability the underlying store has.
/// Casting (<c>store is IDeadLetterStoreAdmin</c>) sees only the outermost type and is therefore lossy
/// through any decorator; it must not be used.
/// </para>
/// <para>
/// <strong>The obligation above is enforced at registration, not merely written here.</strong> The
/// <see cref="TenantOwnedAttribute"/> below is the declaration point: a host composing
/// row-discriminator multi-tenancy refuses to start unless the registered provider presents a tenancy
/// capability marker, and that marker is emitted only by the registration seam that supplies the store
/// its <see cref="ITenantContext"/> — never registered beside it. A store built without the ambient
/// tenant therefore cannot carry a marker attesting that it applies one. A consumer-supplied store that
/// ignores the ambient tenant is refused at startup instead of returning another tenant's message bodies
/// at runtime.
/// </para>
/// </remarks>
[TenantOwned]
public interface IDeadLetterStore : IServiceProvider
{
	/// <summary>
	/// Resolves an optional dead-letter capability, or <see langword="null"/> when it is unavailable.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve, for example <see cref="IDeadLetterStoreAdmin"/>. </param>
	/// <returns>
	/// An instance assignable to <paramref name="serviceType"/> when this store provides the capability;
	/// otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// The default implementation answers for any capability this instance itself implements, so a leaf store
	/// need not override it. A decorator must override it to defer unknown capabilities to the store it wraps;
	/// without that, wrapping a store silently removes every capability the wrapper does not itself implement.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

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
	/// Claims a dead letter message for replay by marking it replayed, and reports whether this caller won
	/// the claim.
	/// </summary>
	/// <param name="messageId"> The ID of the message to claim. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>
	/// <see cref="DeadLetterReplayOutcome.Claimed"/> when this caller moved the message from not-replayed to
	/// replayed and may now replay it; <see cref="DeadLetterReplayOutcome.AlreadyReplayed"/> when it was
	/// already marked; <see cref="DeadLetterReplayOutcome.MessageNotFound"/> when no such message exists in
	/// the caller's tenant.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Implementations MUST make this a conditional write whose row count is the decision</b> — the
	/// not-replayed condition belongs in the statement, not in a prior read. A read-then-write, however
	/// carefully guarded, lets two callers both observe an unmarked message and both proceed.
	/// </para>
	/// <para>
	/// <b>Callers claim BEFORE the replay, never after it</b>, and release the claim with
	/// <see cref="ReleaseReplayClaimAsync"/> when the replay does not succeed. Marking after the dispatch
	/// makes the flag a record of a replay rather than a claim on one, which is what leaves the duplicate
	/// window open.
	/// </para>
	/// <para>
	/// <b>This is not idempotent and must not be, which is a deliberate reversal.</b> Calling it twice
	/// returns <see cref="DeadLetterReplayOutcome.Claimed"/> then
	/// <see cref="DeadLetterReplayOutcome.AlreadyReplayed"/>; a second call that reported success would make
	/// the claim worthless. Absence of a throw is not the contract — the returned outcome is.
	/// </para>
	/// </remarks>
	Task<DeadLetterReplayOutcome> MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Returns a claimed message to the not-replayed state, so a replay that did not succeed can be
	/// attempted again.
	/// </summary>
	/// <param name="messageId"> The ID of the message whose claim is being released. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <remarks>
	/// <para>
	/// <b>Without this, claiming before the replay would silently remove a behaviour consumers have.</b> A
	/// replay whose dispatch fails must stay replayable; a claim that is never released would mark it
	/// replayed although nothing was delivered, and the row would then be indistinguishable from a replay
	/// that succeeded.
	/// </para>
	/// <para>
	/// <b>The residual exposure is a process ending between a failed replay and this release</b>, which
	/// leaves one message marked replayed that was never delivered. That is bounded at one and the state
	/// converges; the ordering it replaces was unbounded, re-delivering on every subsequent attempt. No
	/// claim identity is needed to make the release safe, because the caller releasing it is the only one
	/// that holds it.
	/// </para>
	/// <para>
	/// Releasing a message that is not marked, or that does not exist, is a no-op rather than an error.
	/// </para>
	/// </remarks>
	Task ReleaseReplayClaimAsync(string messageId, CancellationToken cancellationToken);

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
	/// <summary>Gets the count of messages in the dead letter queue, for the ambient tenant.</summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The number of entries the ambient tenant owns. </returns>
	/// <remarks>
	/// Confined to the ambient tenant, on the same terms as every read on <see cref="IDeadLetterStore" />.
	/// The confinement is restated here rather than inherited, because this is a separate interface and an
	/// implementer writing it does not necessarily read the one above. An estate-wide total is a disclosure
	/// even though it returns no message content: it tells one tenant how many failures every other tenant
	/// has.
	/// </remarks>
	Task<long> GetCountAsync(CancellationToken cancellationToken);

	/// <summary>Cleans up old dead letter messages for the ambient tenant, by retention policy.</summary>
	/// <param name="retentionDays"> Entries dead-lettered longer ago than this are removed. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The number of entries removed, all of them owned by the ambient tenant. </returns>
	/// <remarks>
	/// Confined to the ambient tenant, and this is the operation where that matters most on this interface.
	/// A retention sweep matches rows by age, so an implementation that omits the tenant term deletes every
	/// tenant's aged entries whenever any one tenant's retention runs -- silent data loss rather than a
	/// disclosure, and unrecoverable, since a dead-lettered message is the only remaining copy. Scoping only
	/// the read paths does not remove that problem, it relocates it.
	/// </remarks>
	Task<int> CleanupOldMessagesAsync(int retentionDays, CancellationToken cancellationToken);
}
