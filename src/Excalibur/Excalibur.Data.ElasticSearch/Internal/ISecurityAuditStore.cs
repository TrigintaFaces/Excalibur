// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// used by <see cref="Security.SecurityAuditor"/>. Exposes <b>use-case</b>
/// operations (ensure schema, append events) — not SDK factory shape — so
/// tests can substitute the SDK without depending on which
/// <c>ElasticsearchClient</c> / <c>Indices</c> / <c>Cluster</c> overloads
/// remain virtual in a given SDK minor version. Not a consumer-facing
/// abstraction; do not make this public.
/// </summary>
/// <remarks>
/// <para>
/// First of the 6 γ seams per COMPASS S798 msg 1746 ruling + S799 msg 1799
/// naming ACK. Follows the ADR-142 §D7 canonical template set by
/// <c>Excalibur.Security.Azure.Internal.ISecretClient</c> (S797,
/// <c>bd-wy56o5</c>) and <c>Excalibur.Dispatch.Transport.AzureServiceBus.Internal.IServiceBusClient</c>
/// (S798, <c>f5c960341</c> + <c>f151edda7</c>).
/// </para>
/// <para>
/// Naming test: the suffix <c>Store</c> describes the consumer's domain role
/// (audit event persistence) rather than an SDK sub-client — matches the
/// "describes the work, not the wire" rubric COMPASS set in msg 1746.
/// </para>
/// </remarks>
internal interface ISecurityAuditStore
{
	/// <summary>
	/// Ensures the security audit index template exists in Elasticsearch,
	/// provisioning it if missing. Idempotent.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> if the template was created by this call;
	/// <see langword="false"/> if it already existed.
	/// </returns>
	Task<bool> EnsureAuditIndexTemplateAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Bulk-appends a batch of audit events to the current month's audit
	/// index (<c>security-audit-{yyyy-MM}</c>). The adapter constructs the
	/// bulk request and index-operations internally; callers pass only the
	/// domain events.
	/// </summary>
	/// <param name="events">The audit events to append.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// An <see cref="AuditBulkAppendResult"/> describing the outcome.
	/// </returns>
	Task<AuditBulkAppendResult> BulkAppendEventsAsync(
		IReadOnlyList<SecurityAuditEvent> events,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of a <see cref="ISecurityAuditStore.BulkAppendEventsAsync"/>
/// operation. Domain shape — no SDK types cross the seam.
/// </summary>
/// <param name="Success">Whether the batch was accepted by the store.</param>
/// <param name="ErrorDetails">Error diagnostics when <paramref name="Success"/> is false; otherwise null.</param>
/// <param name="AppendedCount">The number of events submitted in the batch.</param>
internal readonly record struct AuditBulkAppendResult(
	bool Success,
	string? ErrorDetails,
	int AppendedCount);
