// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// The result of an orphaned access detection scan.
/// </summary>
/// <param name="GeneratedAt">When the report was generated.</param>
/// <param name="TenantId">The tenant scope of the scan, or <see langword="null"/> for all tenants.</param>
/// <param name="OrphanedGrants">The grants flagged as orphaned.</param>
/// <param name="TotalUsersScanned">The total number of distinct users evaluated.</param>
public sealed record OrphanedAccessReport(
	DateTimeOffset GeneratedAt,
	string? TenantId,
	IReadOnlyList<OrphanedGrant> OrphanedGrants,
	int TotalUsersScanned);
