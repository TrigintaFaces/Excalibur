// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Provides durable, tamper-evident storage of audit events.
/// </summary>
/// <remarks>
/// Implementations may use SQL Server, Postgres, append-only blob storage, or specialized audit platforms (e.g., Splunk, Datadog).
/// </remarks>
public interface IAuditWriter
{
	/// <summary>
	/// Stores an audit event with hash chain linking.
	/// </summary>
	/// <param name="auditEvent"> The audit event to store. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The stored event with assigned ID, hash, and sequence number. </returns>
	Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
