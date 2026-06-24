// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Provides audit logging services for compliance and security monitoring.
/// </summary>
/// <remarks>
/// <para>
/// The audit logger creates a hash-chained, tamper-evident audit trail:
/// - Each event is hashed with the previous event's hash
/// - Integrity can be verified for any time range
/// - Events are immutable once logged
/// </para>
/// <para>
/// Implementation notes:
/// - LogAsync is <b>fail-closed</b>: a store failure is surfaced as <see cref="AuditPersistenceException"/>,
///   never masked behind a success-shaped result. Callers must not treat a returned <see cref="AuditEventId"/>
///   as anything other than a durably persisted event.
/// - Events must be persisted durably before returning a successful result.
/// - High-frequency logging should use batching.
/// </para>
/// </remarks>
public interface IAuditLogger
{
	/// <summary>
	/// Logs an audit event to the audit store.
	/// </summary>
	/// <remarks>
	/// This operation is <b>fail-closed</b>. If the underlying audit store cannot durably persist the event,
	/// the failure is surfaced as <see cref="AuditPersistenceException"/> rather than returning a
	/// success-shaped <see cref="AuditEventId"/>. A returned <see cref="AuditEventId"/> therefore always
	/// denotes a durably recorded event. Callers requiring fail-open behavior must catch
	/// <see cref="AuditPersistenceException"/> and apply their own retry or queueing policy.
	/// </remarks>
	/// <param name="auditEvent"> The audit event to log. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The event ID and hash assigned to the durably persisted event. </returns>
	/// <exception cref="AuditPersistenceException">Thrown when the audit store fails to durably persist the event.</exception>
	Task<AuditEventId> LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Verifies the integrity of the audit log for the specified time range.
	/// </summary>
	/// <param name="startDate"> The start of the verification period. </param>
	/// <param name="endDate"> The end of the verification period. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The integrity verification result. </returns>
	/// <remarks>
	/// This operation may be expensive for large time ranges. Consider running during maintenance windows for large-scale verification.
	/// </remarks>
	Task<AuditIntegrityResult> VerifyIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken);
}
