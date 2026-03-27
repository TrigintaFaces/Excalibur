// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Evaluates a user's grants against Separation of Duties policies to detect conflicts.
/// </summary>
/// <remarks>
/// <para>
/// Supports two modes: current evaluation (check existing grants) and hypothetical
/// evaluation (check whether adding a proposed grant would create a conflict).
/// </para>
/// </remarks>
public interface ISoDEvaluator
{
	/// <summary>
	/// Evaluates a user's current grants against all SoD policies.
	/// </summary>
	/// <param name="userId">The user to evaluate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of detected SoD conflicts. Empty if no conflicts found.</returns>
	Task<IReadOnlyList<SoDConflict>> EvaluateCurrentAsync(string userId, CancellationToken cancellationToken);

	/// <summary>
	/// Evaluates whether adding a proposed grant would create any SoD conflicts.
	/// </summary>
	/// <param name="userId">The user to evaluate.</param>
	/// <param name="proposedScope">The proposed grant scope (role name or activity name).</param>
	/// <param name="proposedGrantType">The proposed grant type (e.g., Activity, ActivityGroup, Role) used to determine
	/// which policy scope the proposed scope should be checked against.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of SoD conflicts that would result from the proposed grant. Empty if no conflicts.</returns>
	Task<IReadOnlyList<SoDConflict>> EvaluateHypotheticalAsync(string userId, string proposedScope, string proposedGrantType, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this evaluator.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
