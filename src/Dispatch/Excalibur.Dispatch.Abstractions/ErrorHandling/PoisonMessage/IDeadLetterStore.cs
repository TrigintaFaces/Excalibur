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
