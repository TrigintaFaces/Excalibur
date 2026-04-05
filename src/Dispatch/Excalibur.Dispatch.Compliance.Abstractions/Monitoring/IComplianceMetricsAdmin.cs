// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides administrative compliance metrics operations.
/// Implementations should implement this alongside <see cref="IComplianceMetrics"/>.
/// </summary>
public interface IComplianceMetricsAdmin
{
	/// <summary>Updates the count of keys nearing expiration.</summary>
	void UpdateKeysNearingExpiration(int count, string provider);

	/// <summary>Records an audit event being logged.</summary>
	void RecordAuditEventLogged(string eventType, string outcome, string? tenantId);

	/// <summary>Updates the current audit backlog size.</summary>
	void UpdateAuditBacklogSize(int count);

	/// <summary>Records audit chain integrity verification result.</summary>
	void RecordAuditIntegrityCheck(long eventsVerified, int violationsFound, double durationMs);

	/// <summary>Records an encryption key usage.</summary>
	void RecordKeyUsage(string keyId, string provider, string operation);
}
