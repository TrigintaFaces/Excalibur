// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Assesses the risk score of a proposed grant for provisioning workflow decisions.
/// </summary>
/// <remarks>
/// Risk scores are advisory (0-100) and do not block provisioning.
/// They influence which approval steps are triggered via <see cref="ApprovalCondition.RiskScoreAbove"/>.
/// </remarks>
public interface IGrantRiskAssessor
{
	/// <summary>
	/// Assesses the risk of a proposed grant.
	/// </summary>
	/// <param name="userId">The user who would receive the grant.</param>
	/// <param name="grantScope">The scope/qualifier of the proposed grant.</param>
	/// <param name="grantType">The type of the proposed grant.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A risk score from 0 (no risk) to 100 (maximum risk).</returns>
	Task<int> AssessRiskAsync(string userId, string grantScope, string grantType, CancellationToken cancellationToken);
}
