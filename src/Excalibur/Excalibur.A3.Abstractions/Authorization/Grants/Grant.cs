// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Provider-neutral authorization grant assigned to a subject for a specific qualifier.
/// </summary>
/// <param name="UserId">The user/subject identifier.</param>
/// <param name="FullName">Optional display name.</param>
/// <param name="TenantId">Optional tenant identifier.</param>
/// <param name="GrantType">The type of grant (e.g., role, activity-group).</param>
/// <param name="Qualifier">The qualifier or scope for the grant.</param>
/// <param name="ExpiresOn">Optional expiration timestamp (UTC).</param>
/// <param name="GrantedBy">Identifier that issued the grant.</param>
/// <param name="GrantedOn">Timestamp when the grant was issued (UTC).</param>
public sealed record Grant(
	string UserId,
	string? FullName,
	string? TenantId,
	string GrantType,
	string Qualifier,
	DateTimeOffset? ExpiresOn,
	string GrantedBy,
	DateTimeOffset GrantedOn);
