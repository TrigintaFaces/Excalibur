// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.BoundedContext;

/// <summary>
/// Validates bounded context boundaries and reports cross-boundary violations.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the single-purpose pattern with ≤5 methods (quality gate).
/// Implementations scan registered types decorated with <see cref="BoundedContextAttribute"/>
/// and detect when aggregates or entities from different bounded contexts have direct
/// references to each other, which may indicate an architectural boundary violation.
/// </para>
/// <para>
/// Reference: Similar in concept to <c>Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck</c>
/// (single async method returning a result), but for architectural validation rather than runtime health.
/// </para>
/// </remarks>
public interface IBoundedContextValidator
{
	/// <summary>
	/// Validates all registered bounded context boundaries and returns any violations found.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A collection of bounded context violations. Empty if no violations are found.</returns>
	Task<IReadOnlyList<BoundedContextViolation>> ValidateAsync(CancellationToken cancellationToken);
}
