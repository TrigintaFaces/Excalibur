// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Represents the status of a projection rebuild operation.
/// </summary>
/// <param name="ProjectionName">The name of the projection being rebuilt.</param>
/// <param name="State">The current state of the rebuild.</param>
/// <param name="Progress">The progress percentage (0-100).</param>
/// <param name="LastRebuiltAt">The timestamp of the last successful rebuild, or <see langword="null"/> if never rebuilt.</param>
public sealed record ProjectionRebuildStatus(
	string ProjectionName,
	ProjectionRebuildState State,
	int Progress,
	DateTimeOffset? LastRebuiltAt);
