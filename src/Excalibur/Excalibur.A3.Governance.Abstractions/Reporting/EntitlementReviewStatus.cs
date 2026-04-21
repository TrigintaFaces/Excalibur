// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Reporting;

/// <summary>
/// Review and compliance status for an entitlement entry.
/// </summary>
/// <param name="HasBeenReviewed">Whether the entitlement has been reviewed in any access review campaign.</param>
/// <param name="LastReviewedOn">The date of the most recent completed review, or <see langword="null"/> if never reviewed.</param>
/// <param name="SoDConflictPolicyIds">Policy identifiers for any separation-of-duties conflicts, or <see langword="null"/> if none.</param>
public sealed record EntitlementReviewStatus(
	bool HasBeenReviewed,
	DateTimeOffset? LastReviewedOn,
	IReadOnlyList<string>? SoDConflictPolicyIds);
