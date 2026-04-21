// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Represents a detected Separation of Duties conflict for a specific user.
/// </summary>
/// <param name="PolicyId">The identifier of the violated <see cref="SoDPolicy"/>.</param>
/// <param name="UserId">The user who holds conflicting grants.</param>
/// <param name="ConflictingItem1">The first conflicting role or activity name.</param>
/// <param name="ConflictingItem2">The second conflicting role or activity name.</param>
/// <param name="DetectedAt">When the conflict was detected (UTC).</param>
/// <param name="Severity">The severity of the violated policy.</param>
public sealed record SoDConflict(
	string PolicyId,
	string UserId,
	string ConflictingItem1,
	string ConflictingItem2,
	DateTimeOffset DetectedAt,
	SoDSeverity Severity);
