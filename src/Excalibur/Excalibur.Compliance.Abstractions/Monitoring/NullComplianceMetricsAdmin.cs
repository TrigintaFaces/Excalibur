// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Null-object <see cref="IComplianceMetricsAdmin"/>: an explicit, harmless no-op used when the resolved
/// <see cref="IComplianceMetrics"/> instance does not implement the admin recording surface.
/// </summary>
/// <remarks>
/// A metrics implementation that is not an admin recorder (for example a read-only or test double) has no
/// place to record to, so every admin call is intentionally a no-op. Routing those calls through this
/// single object — rather than guarding each call site with an <c>is IComplianceMetricsAdmin</c> check —
/// makes the "nothing to record" case an explicit, named behaviour instead of a silently skipped branch
/// scattered across every extension method.
/// </remarks>
internal sealed class NullComplianceMetricsAdmin : IComplianceMetricsAdmin
{
	/// <summary>Gets the shared stateless instance.</summary>
	public static NullComplianceMetricsAdmin Instance { get; } = new();

	private NullComplianceMetricsAdmin()
	{
	}

	/// <inheritdoc />
	public void UpdateKeysNearingExpiration(int count, string provider)
	{
	}

	/// <inheritdoc />
	public void RecordAuditEventLogged(string eventType, string outcome, string? tenantId)
	{
	}

	/// <inheritdoc />
	public void UpdateAuditBacklogSize(int count)
	{
	}

	/// <inheritdoc />
	public void RecordAuditIntegrityCheck(long eventsVerified, int violationsFound, double durationMs)
	{
	}

	/// <inheritdoc />
	public void RecordKeyUsage(string keyId, string provider, string operation)
	{
	}
}
