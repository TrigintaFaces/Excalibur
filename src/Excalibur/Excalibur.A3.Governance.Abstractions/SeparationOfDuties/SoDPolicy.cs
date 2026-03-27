// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Defines a Separation of Duties policy that prevents a single user from holding
/// a conflicting combination of roles or activities.
/// </summary>
/// <param name="PolicyId">Unique identifier for this policy.</param>
/// <param name="Name">Human-readable policy name.</param>
/// <param name="Description">Optional policy description.</param>
/// <param name="Severity">The severity level applied when this policy is violated.</param>
/// <param name="PolicyScope">Whether the policy references roles or activities.</param>
/// <param name="ConflictingItems">
/// The set of role names or activity names that conflict. A violation is detected when a user
/// holds grants for two or more items in this list. Supports N-way conflicts (not just pairs).
/// </param>
/// <param name="TenantId">Optional tenant scope; <see langword="null"/> means the policy is global.</param>
/// <param name="CreatedBy">Identifier of the actor who created this policy.</param>
public sealed record SoDPolicy(
	string PolicyId,
	string Name,
	string? Description,
	SoDSeverity Severity,
	SoDPolicyScope PolicyScope,
	IReadOnlyList<string> ConflictingItems,
	string? TenantId,
	string CreatedBy);
