// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// Detects orphaned access by scanning grants against the identity system.
/// </summary>
/// <remarks>
/// <para>
/// Identifies grants held by non-active users (inactive, departed, or unknown status)
/// and recommends appropriate actions. The detector reports recommendations but does
/// not perform revocations itself.
/// </para>
/// </remarks>
public interface IOrphanedAccessDetector
{
	/// <summary>
	/// Scans all grants, checks each user's status via <see cref="IUserStatusProvider"/>,
	/// and returns a report of orphaned grants with recommended actions.
	/// </summary>
	/// <param name="tenantId">Optional tenant scope. <see langword="null"/> scans all tenants.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A report containing all detected orphaned grants.</returns>
	Task<OrphanedAccessReport> DetectAsync(string? tenantId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this detector.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
