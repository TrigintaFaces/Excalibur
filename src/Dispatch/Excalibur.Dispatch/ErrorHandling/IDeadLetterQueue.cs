// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Defines the contract for a dead letter queue that stores failed messages for later inspection and replay.
/// </summary>
/// <remarks>
/// The dead letter queue is used to capture messages that cannot be processed after exhausting
/// all retry attempts. It provides capabilities for:
/// <list type="bullet">
///   <item>Storing failed messages with full exception context</item>
///   <item>Retrieving entries for inspection and debugging</item>
///   <item>Replaying messages for reprocessing</item>
///   <item>Purging old or resolved entries</item>
/// </list>
/// <para>
/// <strong>Tenancy — this interface is tenant-scoped.</strong> Its operations address only entries
/// belonging to the ambient tenant; an entry stored under a different tenant is not visible through it.
/// That guarantee matters here more than on most contracts because the entries returned include the failed
/// message body, so an estate-wide result would disclose one tenant's message content to another.
/// </para>
/// <para>
/// A host that establishes no tenant operates on the untenanted partition, which is a real partition
/// holding the entries that carry no tenant — not a wildcard. Forgetting to establish a tenant therefore
/// narrows what a caller can see rather than widening it.
/// </para>
/// <para>
/// Estate-wide inspection, replay, and purge are the operator capability and live on
/// <see cref="IDeadLetterQueueAdmin"/>, where each one says so in its name. Note that a separate
/// <em>interface</em> is not by itself a separate <em>capability</em>: the shipped provider registers both
/// interfaces against one instance, so a host that resolves the admin interface has granted estate-wide
/// reach to whatever holds it.
/// </para>
/// <para>
/// The originating tenant is retained in storage and used to scope these operations and to restore the
/// correct tenant when an entry is replayed. It is <em>not</em> projected onto <see cref="DeadLetterEntry"/>,
/// so callers cannot read an entry's tenant from the returned value and must not rely on doing so.
/// </para>
/// <para>
/// Two properties, not one: tenant scope governs which entries a caller may <em>address</em>; the tenant a
/// replayed message re-enters is always the tenant it was <em>stored</em> under. An entry stored without a
/// tenant re-enters untenanted — never as the caller's ambient tenant.
/// </para>
/// <para>
/// <strong>Effects — a value the caller supplies is the value a reader gets back.</strong> Whatever a
/// caller hands to <see cref="EnqueueAsync{T}"/> is what a subsequent read of that entry reports. This is
/// stated as an effect and not as a schema: how an implementation stores a value is its own business, and
/// what it owes is that a read agrees with the write.
/// </para>
/// <para>
/// Two consequences follow, and both are correct outcomes rather than loopholes. The clause binds only
/// what the caller <em>supplies</em>, so an implementation is never obliged to invent provenance it was
/// not given. And it says nothing about retention, so a discarding implementation satisfies it vacuously
/// because nothing is ever read back from it.
/// </para>
/// <para>
/// <strong>Every filter axis this contract exposes is a promise that the underlying value varies.</strong>
/// An implementation that pins a filtered value to a constant turns a caller's query into a confident
/// wrong answer with nothing to show for it, because an empty result is indistinguishable from "there are
/// none". Two entries that must fall on opposite sides of any exposed filter are required to be separated
/// by it.
/// </para>
/// <para>
/// <strong>Failure — the operation may fail with an implementation-specific exception, and a caller must
/// treat any failure as "not enqueued".</strong> This contract deliberately does not enumerate an
/// exception set. Enumerating one would oblige an implementation whose backing technology raises something
/// unlisted either to lie by wrapping it or to break the contract by throwing, so the honest clause is the
/// weaker one. A caller that wraps the call in a catch-all over every failure is therefore <em>correct</em>
/// rather than merely defensive.
/// </para>
/// <para>
/// <strong>Not supported.</strong> Two tenants holding the same entry identifier is outside this contract.
/// Identifiers are allocated per entry and are not reused across tenants; a store that admits the same
/// identifier under two tenants fails the addressing operations rather than choosing between the rows.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "DeadLetterQueue is a standard industry term in messaging systems")]
[TenantOwned]
public interface IDeadLetterQueue
{
	/// <summary>
	/// Enqueues a message to the dead letter queue.
	/// </summary>
	/// <typeparam name="T">The type of message being dead lettered.</typeparam>
	/// <param name="message">The message that failed processing.</param>
	/// <param name="reason">The reason for dead lettering.</param>
	/// <param name="exception">Optional exception that caused the failure.</param>
	/// <param name="metadata">Optional additional metadata. A nonblank <c>MessageType</c> supplies the original
	/// identity; otherwise the runtime type's declared name is used, or an empty identity when unknown.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The ID of the created dead letter entry.</returns>
	/// <remarks>
	/// Tenant-scoped: the entry is stored under the ambient tenant. When there is no ambient tenant the entry
	/// is stored untenanted and remains addressable only through <see cref="IDeadLetterQueueAdmin"/>.
	/// Missing message-name declarations do not prevent failure capture. An unknown identity is not
	/// reconstructed from a CLR type name; replay remains the configured handler's responsibility.
	/// </remarks>
	Task<Guid> EnqueueAsync<T>(
		T message,
		DeadLetterReason reason,
		CancellationToken cancellationToken,
		Exception? exception = null,
		IDictionary<string, string>? metadata = null);

	/// <summary>
	/// Retrieves dead letter entries based on filter criteria.
	/// </summary>
	/// <param name="filter">Optional filter for querying entries. If null, returns all entries up to the limit.</param>
	/// <param name="limit">Maximum number of entries to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A read-only list of dead letter entries matching the criteria.</returns>
	/// <remarks>
	/// Tenant-scoped: returns none of another tenant's entries, and every one of the caller's own that
	/// matches <paramref name="filter"/>. The tenant term is taken from the registered tenant context,
	/// never from <paramref name="filter"/>, so a caller cannot widen this read by omitting a tenant nor
	/// redirect it by naming another one.
	/// <para>
	/// Fails closed: when multi-tenancy is registered but resolves no tenant, this raises
	/// <see cref="TenantRequiredException"/> rather than returning entries across tenants. With no tenant
	/// context registered at all, the read binds the reserved untenanted partition.
	/// </para>
	/// </remarks>
	Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
		CancellationToken cancellationToken,
		DeadLetterQueryFilter? filter = null,
		int limit = 100);

	/// <summary>
	/// Retrieves a specific dead letter entry by its ID.
	/// </summary>
	/// <param name="entryId">The unique identifier of the entry.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The dead letter entry if found, null otherwise.</returns>
	/// <remarks>
	/// Tenant-scoped: resolves the caller's own entry for <paramref name="entryId"/> when one exists, and
	/// an entry stored under another tenant is reported as not found. An identifier alone does
	/// not address an entry — it is addressed by identifier <em>within</em> the ambient tenant, so holding an
	/// entry id obtained from a log line, an export, or a correlation trail does not grant access to it.
	/// <para>
	/// Fails closed: when multi-tenancy is registered but resolves no tenant, this raises
	/// <see cref="TenantRequiredException"/> rather than resolving the entry unscoped. With no tenant context
	/// registered at all, the lookup binds the reserved untenanted partition.
	/// </para>
	/// </remarks>
	Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken);

	/// <summary>
	/// Replays a dead letter entry, re-submitting it for processing.
	/// </summary>
	/// <param name="entryId">The unique identifier of the entry to replay.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> when this call replayed the entry; <see langword="false"/> when it did not.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <strong>A replay happens at most once per entry, and the return says whether it was THIS call that
	/// performed it.</strong> An implementation claims the entry before invoking the handler, so of several
	/// concurrent callers exactly one replays and the others return <see langword="false"/>. A
	/// <see langword="false"/> therefore carries three distinct situations and does not separate them: no
	/// such entry; the entry was already replayed; or another caller claimed it first. Treat it as "not
	/// replayed by me" rather than as "not found".
	/// </para>
	/// <para>
	/// A handler that throws propagates, and the claim is released so the entry can be replayed again —
	/// a transient downstream failure does not consume the entry.
	/// </para>
	/// <para>
	/// <strong>Known gap.</strong> If the process stops between claiming an entry and delivering it, or if
	/// releasing the claim after a failed handler does not succeed, the entry is left marked replayed
	/// without having been delivered — and that state cannot be told apart from a replay that succeeded,
	/// because the entry carries the same flag either way. The lost replay is bounded: it affects the one
	/// entry, and no later batch re-delivers it. The remaining signal is a logged warning, so treat a
	/// replay that reported an error as needing confirmation from your own handler's effects rather than
	/// from the entry's state.
	/// </para>
	/// Tenant-scoped: only an entry stored under the ambient tenant is replayable, and another tenant's entry
	/// is reported as not found. Fails closed: when multi-tenancy is registered but resolves no tenant, this
	/// raises <see cref="TenantRequiredException"/> rather than replaying unscoped.
	/// <para>
	/// The replay <em>target</em> is enforced and is a separate property: a replayed message always
	/// re-enters the tenant its entry was stored under, never the caller's, so a replay cannot inject
	/// another tenant's message into the caller's own tenant.
	/// </para>
	/// </remarks>
	Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current count of entries in the dead letter queue.
	/// </summary>
	/// <param name="filter">Optional filter to count specific entries.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of entries matching the filter criteria.</returns>
	/// <remarks>
	/// Tenant-scoped: counts none of another tenant's entries, and every one of the caller's own that
	/// matches <paramref name="filter"/>, so the number does not disclose another tenant's failure volume
	/// nor undercount the caller's own. The tenant term is taken from the registered tenant context, never
	/// from <paramref name="filter"/>.
	/// <para>
	/// Fails closed: when multi-tenancy is registered but resolves no tenant, this raises
	/// <see cref="TenantRequiredException"/> rather than counting across tenants. With no tenant context
	/// registered at all, the count binds the reserved untenanted partition.
	/// </para>
	/// </remarks>
	Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null);
}

/// <summary>
/// Provides batch and purge operations for the dead letter queue.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Tenancy.</strong> This interface is <em>estate-wide</em>: a platform operator inspects, replays,
/// and purges failed messages across every tenant, and these operations are deliberately not filtered by the
/// ambient tenant. It is the privileged counterpart to <see cref="IDeadLetterQueue"/> and is registered
/// separately, so a host that does not need operator capabilities need not expose it at all.
/// </para>
/// <para>
/// Entries returned here carry no tenant discriminator — <see cref="DeadLetterEntry"/> does not expose the
/// originating tenant — so results from different tenants cannot be told apart by the caller, and they
/// include the failed message body. Treat anything obtained through this interface as privileged: do not
/// surface it to a tenant-facing view.
/// </para>
/// <para>
/// Replay always restores the tenant an entry was <em>stored</em> under, never the caller's. Scope governs
/// which entries an operation may address; it does not change the tenant a replayed message re-enters, so
/// replaying another tenant's entry cannot inject it into the operator's own tenant.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "DeadLetterQueue is a standard industry term")]
public interface IDeadLetterQueueAdmin
{
	/// <summary>Replays multiple dead letter entries matching the filter.</summary>
	/// <param name="filter">Selects the entries to replay. The selection is not narrowed by the ambient tenant.</param>
	/// <param name="limit">
	/// The maximum number of entries this call may take. Required: the batch size governs how much work a
	/// single operator action performs, so it is the caller's to state rather than an implementation's to
	/// assume.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A <see cref="ReplayBatchResult"/> reporting how many entries were enumerated, how many were replayed,
	/// and whether <paramref name="limit"/> cut the selection short.
	/// </returns>
	/// <remarks>
	/// Estate-wide: selects across every tenant. Each replayed message re-enters the tenant its entry was
	/// stored under, so a batch spanning several tenants restores each message to its own.
	/// <para>
	/// A partial batch is reported, never silent: when the limit is reached,
	/// <see cref="ReplayBatchResult.Truncated"/> is <see langword="true"/> and the caller must re-run to
	/// drain the remainder. The result is not a bare count, so "this many were replayed" can no longer be
	/// mistaken for "every entry matching the filter was replayed".
	/// </para>
	/// </remarks>
	Task<ReplayBatchResult> ReplayBatchAsync(DeadLetterQueryFilter filter, int limit, CancellationToken cancellationToken);

	/// <summary>Purges a dead letter entry.</summary>
	/// <param name="entryId">The unique identifier of the entry to purge.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if an entry was purged; false if no entry matched.</returns>
	/// <remarks>Estate-wide: addresses the entry in whichever tenant holds it, not only the ambient tenant.</remarks>
	Task<bool> PurgeAsync(Guid entryId, CancellationToken cancellationToken);

	/// <summary>Purges all dead letter entries older than the specified age.</summary>
	/// <param name="olderThan">The minimum age an entry must have reached to be purged.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of entries purged.</returns>
	/// <remarks>
	/// Estate-wide and irreversible: deletes matching entries in <em>every</em> tenant on an age predicate
	/// alone. There is no tenant term in the selection, so a host that exposes this operation to a
	/// tenant-facing caller lets one tenant delete another's failed messages.
	/// </remarks>
	Task<int> PurgeAllTenantsEntriesOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken);

	/// <summary>Retrieves dead letter entries belonging to <b>every</b> tenant.</summary>
	/// <param name="filter">
	/// Selects the entries to return, and carries the page size through <see cref="DeadLetterQueryFilter.Take"/>.
	/// The selection is not narrowed by the ambient tenant. Pass <see langword="null"/> for the defaults.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching entries, drawn from every tenant partition.</returns>
	/// <remarks>
	/// <para>
	/// The operator counterpart to <see cref="IDeadLetterQueue.GetEntriesAsync"/>, which is confined to the
	/// caller's own partition. Entries carry the failed message body and
	/// <see cref="DeadLetterEntry"/> exposes no tenant discriminator, so results from different tenants
	/// cannot be told apart by the caller: treat anything obtained here as privileged and never surface it
	/// to a tenant-facing view.
	/// </para>
	/// <para>
	/// <b>The name is the safety control.</b> Estate-wide reach is spelled at the call site, never inferred
	/// from a scope nobody established — the same discipline as
	/// <see cref="PurgeAllTenantsEntriesOlderThanAsync"/>. A caller cannot arrive here by forgetting to set
	/// a tenant, which is the shape that fails open.
	/// </para>
	/// <para>
	/// An <b>optional capability</b> whose default implementation throws. A store that supports it
	/// overrides this method; a decorator must override it to forward to its inner store, or a decorated
	/// store reports the capability as missing even though the store underneath supports it.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">Thrown by stores that do not support estate-wide inspection.</exception>
	Task<IReadOnlyList<DeadLetterEntry>> GetAllTenantsEntriesAsync(
		DeadLetterQueryFilter? filter,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"This dead letter queue does not support estate-wide inspection. Store type: '{GetType().FullName}'. " +
			"Use GetEntriesAsync for the calling tenant's entries.");

	/// <summary>Retrieves a single dead letter entry from whichever tenant holds it.</summary>
	/// <param name="entryId">The unique identifier of the entry.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The entry, or <see langword="null"/> when no tenant holds one with that identifier.</returns>
	/// <remarks>
	/// The operator counterpart to <see cref="IDeadLetterQueue.GetEntryAsync"/>, which resolves the entry
	/// only within the caller's own partition and reports not-found for another tenant's. Identifiers are
	/// allocated per entry and are not reused across tenants, so at most one row can match.
	/// <para>
	/// The same privilege and naming notes as <see cref="GetAllTenantsEntriesAsync"/> apply, and this is
	/// likewise an optional capability whose default implementation throws.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">Thrown by stores that do not support estate-wide inspection.</exception>
	Task<DeadLetterEntry?> GetAllTenantsEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"This dead letter queue does not support estate-wide inspection. Store type: '{GetType().FullName}'. " +
			"Use GetEntryAsync for an entry in the calling tenant's partition.");

	/// <summary>Replays a single dead letter entry held by any tenant.</summary>
	/// <param name="entryId">The unique identifier of the entry to replay.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> when an entry was replayed; <see langword="false"/> when none matched.</returns>
	/// <remarks>
	/// The single-entry counterpart to <see cref="ReplayBatchAsync"/>, which is already estate-wide, and the
	/// operator counterpart to <see cref="IDeadLetterQueue.ReplayAsync"/>.
	/// <para>
	/// <b>Scope governs which entry may be addressed; it never governs where the message lands.</b> A
	/// replayed message re-enters the tenant its entry was <em>stored</em> under, never the operator's own,
	/// so replaying another tenant's entry cannot inject it into the caller's tenant. An entry stored
	/// without a tenant re-enters untenanted.
	/// </para>
	/// <para>
	/// Likewise an optional capability whose default implementation throws.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">Thrown by stores that do not support estate-wide replay.</exception>
	Task<bool> ReplayAllTenantsEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"This dead letter queue does not support estate-wide replay. Store type: '{GetType().FullName}'. " +
			"Use ReplayAsync for an entry in the calling tenant's partition, or ReplayBatchAsync for a filtered sweep.");
}
