// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Provides querying, retrieval, and integrity verification of stored audit events.
/// </summary>
/// <remarks>
/// <para>
/// Supports efficient querying for compliance reports and hash chain integrity verification
/// for long-term, tamper-evident audit trails.
/// </para>
/// <para>
/// <b>A filter an implementation cannot serve MUST be refused, never answered emptily.</b> An
/// implementation that stores a field in a form no comparison can match -- a column encrypted at rest with
/// a randomized cipher is the case that arises in practice -- throws <see cref="NotSupportedException"/>
/// naming the field, and does not delegate a query it already knows will match nothing. An empty list or a
/// zero count is indistinguishable, at the caller, from "there are no such events", so an operator asking
/// what an actor did would be told in effect that the actor did nothing, while the records sit present and
/// unmatchable. Silence is the one answer an audit trail must never give. Refusal is therefore part of this
/// contract rather than a deviation from it: a caller may be told no, and may not be told a falsehood.
/// </para>
/// </remarks>
public interface IAuditQuery
{
	/// <summary>
	/// Retrieves an audit event by its ID.
	/// </summary>
	/// <param name="eventId"> The unique identifier of the event. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The audit event, or null if not found. </returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance when the implementing
	/// <see cref="IAuditStore"/> presents <see cref="ITenantScopingCapability{TContract}"/>: an event
	/// stored under another tenant is reported as not found, never resolved. See
	/// <see cref="IAuditStore"/> for the full confinement statement.
	/// </remarks>
	Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken);

	/// <summary>
	/// Queries audit events based on the specified criteria.
	/// </summary>
	/// <param name="query"> The query parameters. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A list of matching audit events. </returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance when the implementing
	/// <see cref="IAuditStore"/> presents <see cref="ITenantScopingCapability{TContract}"/>: returns none
	/// of another tenant's audit events and every one of the caller's own that matches
	/// <paramref name="query"/>. See <see cref="IAuditStore"/> for the full confinement statement.
	/// <para>
	/// An empty list means no stored event matched. It never means the implementation was unable to apply a
	/// filter -- that case throws; see the remarks on <see cref="IAuditQuery"/>.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">
	/// Thrown when <paramref name="query"/> filters on a field this implementation cannot compare, naming
	/// the field.
	/// </exception>
	Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the count of audit events matching the query criteria.
	/// </summary>
	/// <param name="query"> The query parameters (MaxResults and Skip are ignored). </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The number of matching events. </returns>
	/// <remarks>
	/// Confined the same way as <see cref="QueryAsync"/>: the count reflects only the caller's own
	/// tenant's matching events, so it does not disclose another tenant's audit volume.
	/// <para>
	/// A zero means no stored event matched, and carries the same obligation as an empty list from
	/// <see cref="QueryAsync"/>: an unservable filter throws rather than counting to zero.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">
	/// Thrown on the same terms as <see cref="QueryAsync"/>.
	/// </exception>
	Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken);

	/// <summary>
	/// Verifies the hash chain integrity for events in the specified range.
	/// </summary>
	/// <param name="startDate"> The start of the verification period. </param>
	/// <param name="endDate"> The end of the verification period. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The integrity verification result. </returns>
	/// <remarks>
	/// Confined the same way as <see cref="QueryAsync"/>: verifies only the caller's own tenant's hash
	/// chain over the given range, never another tenant's.
	/// </remarks>
	Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the last recorded event (for chain linking).
	/// </summary>
	/// <param name="tenantId"> Optional tenant ID for multi-tenant isolation. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The last audit event, or null if no events exist. </returns>
	/// <remarks>
	/// Unlike the other members here, tenant confinement is taken from <paramref name="tenantId"/>
	/// explicitly rather than from ambient state: the result is the last event within that partition (or
	/// the untenanted partition when <paramref name="tenantId"/> is <see langword="null"/>), never another
	/// tenant's most recent event.
	/// </remarks>
	Task<AuditEvent?> GetLastEventAsync(
		string? tenantId,
		CancellationToken cancellationToken);
}
