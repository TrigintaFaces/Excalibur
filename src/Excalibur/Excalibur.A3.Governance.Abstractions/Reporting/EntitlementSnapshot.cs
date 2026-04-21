// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Reporting;

/// <summary>
/// A point-in-time snapshot of entitlements for reporting purposes.
/// </summary>
/// <remarks>
/// Summary statistics (total users, total grants, total roles) are intentionally omitted
/// because they are derivable from the <see cref="Entries"/> collection.
/// </remarks>
/// <param name="GeneratedAt">When the snapshot was generated.</param>
/// <param name="ReportType">The type of report this snapshot represents.</param>
/// <param name="Scope">The user ID or tenant ID scope, or <see langword="null"/> for unscoped reports.</param>
/// <param name="Entries">The entitlement entries in this snapshot.</param>
public sealed record EntitlementSnapshot(
	DateTimeOffset GeneratedAt,
	EntitlementReportType ReportType,
	string? Scope,
	IReadOnlyList<EntitlementEntry> Entries);
