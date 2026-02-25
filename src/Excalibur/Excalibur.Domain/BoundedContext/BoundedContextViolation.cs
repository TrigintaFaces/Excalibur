// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.BoundedContext;

/// <summary>
/// Represents a detected cross-boundary violation between bounded contexts.
/// </summary>
/// <param name="SourceType">The type that references across the boundary.</param>
/// <param name="TargetType">The type being referenced across the boundary.</param>
/// <param name="SourceContext">The bounded context of the source type.</param>
/// <param name="TargetContext">The bounded context of the target type.</param>
/// <param name="Description">A human-readable description of the violation.</param>
public sealed record BoundedContextViolation(
	Type SourceType,
	Type TargetType,
	string SourceContext,
	string TargetContext,
	string Description);
