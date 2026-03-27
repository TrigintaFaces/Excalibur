// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Provider-neutral read model for a role.
/// </summary>
/// <param name="RoleId">The unique role identifier.</param>
/// <param name="Name">The role display name.</param>
/// <param name="Description">Optional role description.</param>
/// <param name="TenantId">Optional tenant scope.</param>
/// <param name="ActivityGroupNames">Activity groups this role maps to.</param>
/// <param name="ActivityNames">Individual activities this role grants directly.</param>
/// <param name="State">Current role lifecycle state.</param>
/// <param name="CreatedAt">When the role was created.</param>
/// <param name="ParentRoleName">Optional parent role name for hierarchy inheritance. <see langword="null"/> indicates a root role.</param>
public sealed record RoleSummary(
	string RoleId,
	string Name,
	string? Description,
	string? TenantId,
	IReadOnlyList<string> ActivityGroupNames,
	IReadOnlyList<string> ActivityNames,
	RoleState State,
	DateTimeOffset CreatedAt,
	string? ParentRoleName = null);
