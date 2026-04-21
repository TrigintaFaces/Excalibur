// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.NonHumanIdentity;

namespace Excalibur.A3.Governance.Reporting;

/// <summary>
/// A single entitlement (grant) in a report snapshot.
/// </summary>
/// <param name="UserId">The principal who holds the grant.</param>
/// <param name="PrincipalType">The type of principal (human, service account, bot, API key).</param>
/// <param name="GrantScope">The scope or resource the grant applies to.</param>
/// <param name="GrantedOn">When the grant was created.</param>
/// <param name="GrantedBy">Who or what issued the grant.</param>
/// <param name="ExpiresOn">When the grant expires, or <see langword="null"/> if permanent.</param>
/// <param name="IsActive">Whether the grant is currently active.</param>
/// <param name="ReviewStatus">Review and compliance status, or <see langword="null"/> if unavailable.</param>
public sealed record EntitlementEntry(
	string UserId,
	PrincipalType PrincipalType,
	string GrantScope,
	DateTimeOffset GrantedOn,
	string GrantedBy,
	DateTimeOffset? ExpiresOn,
	bool IsActive,
	EntitlementReviewStatus? ReviewStatus);
