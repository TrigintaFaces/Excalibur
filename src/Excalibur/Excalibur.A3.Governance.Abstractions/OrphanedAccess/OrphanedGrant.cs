// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// Represents a single grant flagged as orphaned during a scan.
/// </summary>
/// <param name="UserId">The user/subject identifier who holds the grant.</param>
/// <param name="GrantScope">The grant's qualifier/scope.</param>
/// <param name="UserStatus">The principal's status at the time of detection.</param>
/// <param name="GrantedOn">When the grant was originally assigned.</param>
/// <param name="RecommendedAction">The recommended action for this orphaned grant.</param>
public sealed record OrphanedGrant(
	string UserId,
	string GrantScope,
	PrincipalStatus UserStatus,
	DateTimeOffset GrantedOn,
	OrphanedAccessAction RecommendedAction);
