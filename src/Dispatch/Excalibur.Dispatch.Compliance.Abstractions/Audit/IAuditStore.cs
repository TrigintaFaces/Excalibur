// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides storage and retrieval services for audit events.
/// </summary>
/// <remarks>
/// <para>
/// The audit store is responsible for:
/// - Durable, tamper-evident storage of audit events
/// - Efficient querying for compliance reports
/// - Long-term retention according to policy
/// </para>
/// <para> Implementations may use SQL Server, Postgres, append-only blob storage, or specialized audit platforms (e.g., Splunk, Datadog). </para>
/// </remarks>
public interface IAuditStore
{
	/// <summary>
	/// Stores an audit event with hash chain linking.
	/// </summary>
	/// <param name="auditEvent"> The audit event to store. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The stored event with assigned ID, hash, and sequence number. </returns>
	Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves an audit event by its ID.
	/// </summary>
	/// <param name="eventId"> The unique identifier of the event. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The audit event, or null if not found. </returns>
	Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken);

	/// <summary>
	/// Queries audit events based on the specified criteria.
	/// </summary>
	/// <param name="query"> The query parameters. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A list of matching audit events. </returns>
	Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the count of audit events matching the query criteria.
	/// </summary>
	/// <param name="query"> The query parameters (MaxResults and Skip are ignored). </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The number of matching events. </returns>
	Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken);

	/// <summary>
	/// Verifies the hash chain integrity for events in the specified range.
	/// </summary>
	/// <param name="startDate"> The start of the verification period. </param>
	/// <param name="endDate"> The end of the verification period. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The integrity verification result. </returns>
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
	Task<AuditEvent?> GetLastEventAsync(
		string? tenantId,
		CancellationToken cancellationToken);
}
