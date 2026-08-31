// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Provides administrative and query operations for inbox store management.
/// </summary>
/// <remarks>
/// <para>
/// These operations are used by background services, health checks, retry processors,
/// and administrative tooling. They are NOT needed for normal inbox message flow
/// (create, check, mark processed/failed).
/// </para>
/// <para>
/// This follows the same ISP pattern as <see cref="IOutboxStoreAdmin"/> for the base
/// <see cref="IOutboxStore"/>. Implementations should register this sub-interface
/// separately in DI so consumers can resolve it independently.
/// </para>
/// <para>
/// <strong>Interface split:</strong>
/// <list type="bullet">
/// <item><see cref="IInboxStore"/> -- Core: create, check, mark processed/failed, get entry (6 methods, hot path)</item>
/// <item><see cref="IInboxStoreAdmin"/> -- Admin: bulk queries, statistics, cleanup, retry-processor mark-failed (5 methods, operational)</item>
/// </list>
/// </para>
/// </remarks>
public interface IInboxStoreAdmin
{
	/// <summary>
	/// Retrieves message entries eligible for retry, across <b>every</b> tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Scope: estate-wide.</b> The status, retry-count and age filters below are the only predicates
	/// applied; no tenant term is applied and none can be. This is the retry sweeper's read, and the sweeper
	/// runs on a background loop with no ambient tenant to scope it by — a tenant-confined variant could only
	/// drain the one partition that happened to be current, leaving every other tenant's failed entries to
	/// accumulate unattended.
	/// </para>
	/// <para>
	/// <b>This read takes no ownership term.</b> It is a plain query: it writes nothing, and two callers
	/// issuing it concurrently receive the same entries. It therefore does not, and cannot, prevent two
	/// processors from dispatching the same entry. A caller that needs that exclusion must take it itself,
	/// through <see cref="ILeasedInboxStore.TryAcquireLeaseAsync"/> on a store that offers it.
	/// </para>
	/// <para>
	/// <b>Which statuses are eligible depends on whether the store declares the lease protocol, and this
	/// is a required behaviour, not an implementation liberty.</b> A store that implements
	/// <see cref="ILeasedInboxStore"/> MUST return, in addition to <see cref="InboxStatus.Failed"/>
	/// entries, any <see cref="InboxStatus.Processing"/> entry whose lease has <b>expired</b> — compared
	/// against the store's own clock, as everywhere else in the lease protocol. A store that does not
	/// implement the lease protocol returns <see cref="InboxStatus.Failed"/> entries only.
	/// </para>
	/// <para>
	/// The expired-lease arm is what keeps the two properties from trading against each other. A leasing
	/// caller moves an entry to <see cref="InboxStatus.Processing"/> before invoking its handler; if that
	/// caller then dies, a read admitting only <see cref="InboxStatus.Failed"/> would never select the
	/// entry again, and it would be reachable solely by a redelivery that a message already consumed off
	/// the transport never receives. Omitting this arm therefore converts a bounded duplicate dispatch into
	/// permanent silent loss, which is the worse failure of the two.
	/// </para>
	/// <para>
	/// An entry that is <see cref="InboxStatus.Processing"/> with <b>no</b> lease recorded was claimed
	/// through the term-less claim protocol, which is held until its own caller finalizes or releases it.
	/// Absent is "no expiry", never "expired": such an entry MUST NOT be returned.
	/// </para>
	/// <para>
	/// The name is the safety control. The returned entries span partitions, so a caller must re-establish
	/// each entry's own tenant from <see cref="InboxEntry.TenantId"/> before acting on it, rather than
	/// processing the batch under whatever scope happened to be ambient at the call site.
	/// </para>
	/// </remarks>
	/// <param name="maxRetries">Maximum number of retry attempts to consider.</param>
	/// <param name="olderThan">Only return entries older than this timestamp.</param>
	/// <param name="batchSize">Maximum number of entries to return.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Collection of failed inbox entries eligible for retry, from every tenant.</returns>
	ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks an existing inbox entry as failed/retryable, setting its retry count <strong>exactly</strong> to
	/// <paramref name="retryCount"/> without auto-incrementing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a retry-processor operation for the transient-failure case: a short-circuit (e.g. an open
	/// circuit breaker) is not a delivery attempt, so it must leave the message re-admittable for retry
	/// <em>without</em> consuming an attempt. Unlike the core
	/// <see cref="IInboxStore.MarkFailedAsync(string, string, string, CancellationToken)"/> (which increments
	/// the retry count), this overload sets the count exactly — symmetric with
	/// <see cref="IOutboxStore.MarkFailedAsync(string, string, int, CancellationToken)"/>.
	/// </para>
	/// <para>
	/// The entry must already exist; existence semantics match the core method (implementations that update
	/// in place leave a missing entry unchanged or throw, consistent with their existing behavior). This is a
	/// retry-only path that processes already-persisted failed entries, so no insert/upsert is performed.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="handlerType">The handler type the entry is keyed to.</param>
	/// <param name="errorMessage">The error description to record.</param>
	/// <param name="retryCount">The retry count to set on the entry, exactly (not incremented).</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous mark-failed operation.</returns>
	ValueTask MarkFailedAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		int retryCount,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves every inbox entry, across <b>every</b> tenant, for testing and diagnostics.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Scope: estate-wide, and the widest disclosure on this interface.</b> No predicate of any kind is
	/// applied — the read returns the whole store, and each <see cref="InboxEntry"/> carries its payload and
	/// metadata. It observes all partitions and modifies none.
	/// </para>
	/// <para>
	/// The name is the safety control. "GetAll" alone was ambiguous — it read as either "all of this tenant's
	/// entries" or "all entries everywhere" — so the scope is now stated in the name and cannot be reached by
	/// a caller who meant one tenant.
	/// </para>
	/// <para>
	/// An inbox store reads no ambient tenant context, so it cannot honor a tenant it never sees. Should a
	/// tenant-scoped enumeration ever be required, it arrives as an explicit parameter on a distinct
	/// operation — never by inferring a scope from ambient state.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Collection of all inbox entries, across all tenants.</returns>
	ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about inbox entries, across <b>every</b> tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Scope: estate-wide.</b> The counters describe the whole store: no tenant discriminator is applied,
	/// and <see cref="InboxStatistics"/> carries no tenant field, so a confined result could not say which
	/// partition it described even if one were produced. It observes all partitions and modifies none.
	/// </para>
	/// <para>
	/// The name is the safety control: estate-wide counters are reachable only by writing "AllTenants" at the
	/// call site. Aggregate counts disclose no message identifiers, payloads, or tenant names — only totals —
	/// so this is a weaker disclosure than <see cref="GetAllTenantsEntriesAsync"/>, which returns whole entries.
	/// </para>
	/// <para>
	/// An inbox store reads no ambient tenant context, so it cannot honor a tenant it never sees. Should
	/// per-tenant counters ever be required, they arrive as an explicit parameter on a distinct operation —
	/// never by inferring a scope from ambient state.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Statistics including counts of entries by status, across all tenants.</returns>
	ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Removes processed entries older than the specified timestamp across <b>every</b> tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Scope: estate-wide, and destructive.</b> This is a retention sweep that matches rows by status and
	/// age, not by tenant, so it deletes the qualifying entries of every tenant in the store. It observes and
	/// modifies all partitions.
	/// </para>
	/// <para>
	/// The name is the safety control. An unbounded delete is reachable only by writing "AllTenants" at the
	/// call site — never by omitting a scope, and never by passing a value that happens to mean "everything".
	/// A reviewer reading the call site sees the blast radius without tracing where a scope came from.
	/// </para>
	/// <para>
	/// An inbox store reads no ambient tenant context, so it cannot honor a tenant it never sees. Should
	/// tenant-scoped retention ever be required, it arrives as an explicit parameter on a distinct operation —
	/// never by inferring a scope from ambient state.
	/// </para>
	/// </remarks>
	/// <param name="olderThan">Remove entries processed before this timestamp.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The number of entries removed, across all tenants.</returns>
	ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);
}
