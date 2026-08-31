// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch;

/// <summary>
/// Provides administrative and query operations for outbox store management.
/// </summary>
/// <remarks>
/// <para>
/// These operations are used by background services, health checks, and administrative tooling.
/// They are NOT needed for normal outbox message flow (stage/send/fail).
/// Implementations should access this sub-interface via <c>GetService(typeof(IOutboxStoreAdmin))</c>
/// or direct DI registration.
/// </para>
/// </remarks>
public interface IOutboxStoreAdmin
{
	/// <summary>
	/// Retrieves failed messages that are eligible for retry, across <b>every</b> tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Observation only — the result MUST NOT be handed to a transport.</b> This is a plain read: it
	/// matches rows without conditionally taking them, so two callers observe the same message and neither
	/// holds it. Publishing from this set is check-then-act and delivers the message twice. A drain claims
	/// through <c>IOutboxStore.GetUnsentMessagesAsync</c>, whose claim already admits a failed message once
	/// its next-attempt floor has elapsed; this read consults no floor, so a message deferred moments ago
	/// still appears here. Use it to report and diagnose, never to dispatch.
	/// </para>
	/// <para>
	/// <b>Scope: estate-wide.</b> This read matches rows by status and age, not by tenant, so it observes the
	/// messages of every tenant in the store. It modifies nothing. Each returned
	/// <see cref="OutboundMessage"/> carries its own <c>TenantId</c>, so a caller can re-establish the owning
	/// tenant for each message it handles.
	/// </para>
	/// <para>
	/// The name is the safety control. An unscoped read is reachable only by writing "AllTenants" at the call
	/// site — never by omitting a scope, and never by passing a value that happens to mean "everything". This
	/// discloses full message bodies, so a reviewer reading the call site sees the disclosure without tracing
	/// where a scope came from.
	/// </para>
	/// <para>
	/// The outbox drain is cross-tenant by design: one dispatcher serves every tenant, so scoping this read to
	/// an ambient tenant would stall delivery for all the others. An outbox store reads no ambient tenant
	/// context; should a tenant-scoped read ever be required, it arrives as an explicit parameter on a distinct
	/// operation — never by inferring a scope from ambient state.
	/// </para>
	/// </remarks>
	/// <param name="maxRetries"> Maximum number of retry attempts to consider. </param>
	/// <param name="olderThan"> Only return messages that failed before this timestamp. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of failed messages eligible for retry, across all tenants. </returns>
	ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves messages scheduled for future delivery, across <b>every</b> tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Observation only — the result MUST NOT be handed to a transport.</b> This is a plain read: it
	/// matches rows without conditionally taking them, so two callers observe the same message and neither
	/// holds it. Publishing from this set is check-then-act and delivers the message twice — a filter on an
	/// unset dispatcher column does not make it a claim, because the value it tests is never conditionally
	/// written. A drain claims through <c>IOutboxStore.GetUnsentMessagesAsync</c>, whose claim already admits
	/// a scheduled message once its time has arrived. Use it to report and diagnose, never to dispatch.
	/// </para>
	/// <para>
	/// <b>Scope: estate-wide.</b> This read matches rows by status and age, not by tenant, so it observes the
	/// messages of every tenant in the store. It modifies nothing. Each returned
	/// <see cref="OutboundMessage"/> carries its own <c>TenantId</c>, so a caller can re-establish the owning
	/// tenant for each message it handles.
	/// </para>
	/// <para>
	/// The name is the safety control. An unscoped read is reachable only by writing "AllTenants" at the call
	/// site — never by omitting a scope, and never by passing a value that happens to mean "everything". This
	/// discloses full message bodies, so a reviewer reading the call site sees the disclosure without tracing
	/// where a scope came from.
	/// </para>
	/// <para>
	/// The outbox drain is cross-tenant by design: one dispatcher serves every tenant, so scoping this read to
	/// an ambient tenant would stall delivery for all the others. An outbox store reads no ambient tenant
	/// context; should a tenant-scoped read ever be required, it arrives as an explicit parameter on a distinct
	/// operation — never by inferring a scope from ambient state.
	/// </para>
	/// </remarks>
	/// <param name="scheduledBefore"> Only return messages scheduled before this timestamp. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of scheduled messages ready for delivery, across all tenants. </returns>
	ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Removes sent messages older than the specified age across <b>every</b> tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is an estate-wide retention sweep and is deliberately unscoped: it matches rows by age, not by
	/// tenant, so it removes the qualifying messages of every tenant in the store. The name declares that
	/// scope so it cannot be reached by a caller who meant a single tenant.
	/// </para>
	/// <para>
	/// An outbox store reads no ambient tenant context, so it cannot honor a tenant it never sees. Should
	/// tenant-scoped retention ever be required, it arrives as an explicit parameter — never by inferring a
	/// scope from ambient state.
	/// </para>
	/// </remarks>
	/// <param name="olderThan"> Remove messages sent before this timestamp. </param>
	/// <param name="batchSize"> Maximum number of messages to remove in one operation. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The number of messages removed, across all tenants. </returns>
	ValueTask<int> CleanupAllTenantsSentMessagesAsync(
		DateTimeOffset olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the outbox store, across <b>every</b> tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Scope: estate-wide.</b> The name is the safety control: estate-wide counters are reachable only by
	/// writing "AllTenants" at the call site, never by omitting a scope. Aggregate counts disclose no message
	/// identifiers, bodies, or tenant names — only totals — so this is a weaker disclosure than
	/// <see cref="GetAllTenantsFailedMessagesAsync"/>, which returns whole messages.
	/// </para>
	/// <para>
	/// These are estate-wide counters reported to an operator, not to a tenant. The figures describe the
	/// whole table: the call takes no tenant argument, and <see cref="OutboxStatistics"/> carries no tenant
	/// field, so a confined result could not say which partition it described even if one were produced.
	/// </para>
	/// <para>
	/// An outbox store reads no ambient tenant context, so it cannot honor a tenant it never sees. Should
	/// per-tenant counters ever be required, they arrive as an explicit parameter on a distinct operation —
	/// never by inferring a scope from ambient state.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Statistics including message counts by status and oldest unsent message age, across all tenants. </returns>
	ValueTask<OutboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken);
}
