// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// The result of a principal status query, including when the status last changed.
/// </summary>
/// <param name="Status">The current status of the principal.</param>
/// <param name="StatusChangedAt">When the status last changed, or <see langword="null"/> if unknown.
/// Used by the orphaned access detector to evaluate grace periods for inactive users.</param>
public sealed record PrincipalStatusResult(PrincipalStatus Status, DateTimeOffset? StatusChangedAt);
