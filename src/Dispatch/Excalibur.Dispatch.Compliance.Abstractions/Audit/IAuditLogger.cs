// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

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
/// - LogAsync must be fire-and-forget safe (should not throw under normal conditions)
/// - Events must be persisted durably before returning
/// - High-frequency logging should use batching
/// </para>
/// </remarks>
public interface IAuditLogger
{
	/// <summary>
	/// Logs an audit event to the audit store.
	/// </summary>
	/// <param name="auditEvent"> The audit event to log. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The event ID and hash assigned to the logged event. </returns>
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
