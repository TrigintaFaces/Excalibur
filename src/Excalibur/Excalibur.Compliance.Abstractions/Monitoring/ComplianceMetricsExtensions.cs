// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Extension methods for <see cref="IComplianceMetrics"/>.
/// </summary>
public static class ComplianceMetricsExtensions
{
	/// <summary>Updates the count of keys nearing expiration.</summary>
	public static void UpdateKeysNearingExpiration(this IComplianceMetrics metrics, int count, string provider)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		Admin(metrics).UpdateKeysNearingExpiration(count, provider);
	}

	/// <summary>Records an audit event being logged.</summary>
	public static void RecordAuditEventLogged(this IComplianceMetrics metrics, string eventType, string outcome, string? tenantId = null)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		Admin(metrics).RecordAuditEventLogged(eventType, outcome, tenantId);
	}

	/// <summary>Updates the current audit backlog size.</summary>
	public static void UpdateAuditBacklogSize(this IComplianceMetrics metrics, int count)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		Admin(metrics).UpdateAuditBacklogSize(count);
	}

	/// <summary>Records audit chain integrity verification result.</summary>
	public static void RecordAuditIntegrityCheck(this IComplianceMetrics metrics, long eventsVerified, int violationsFound, double durationMs)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		Admin(metrics).RecordAuditIntegrityCheck(eventsVerified, violationsFound, durationMs);
	}

	/// <summary>Records an encryption key usage.</summary>
	public static void RecordKeyUsage(this IComplianceMetrics metrics, string keyId, string provider, string operation)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		Admin(metrics).RecordKeyUsage(keyId, provider, operation);
	}

	/// <summary>
	/// Resolves the admin recording surface of a metrics instance, falling back to an explicit no-op
	/// (<see cref="NullComplianceMetricsAdmin"/>) when the instance does not implement it — so callers
	/// record unconditionally instead of repeating an <c>is IComplianceMetricsAdmin</c> guard, and the
	/// "not an admin recorder" case is a named behaviour rather than a silently skipped branch.
	/// </summary>
	private static IComplianceMetricsAdmin Admin(IComplianceMetrics metrics)
		=> metrics as IComplianceMetricsAdmin ?? NullComplianceMetricsAdmin.Instance;
}
