// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Determines the approval steps required for a provisioning request
/// based on the requested scope and risk score.
/// </summary>
public interface IProvisioningWorkflowConfiguration
{
	/// <summary>
	/// Gets the approval step templates that apply to the given request context.
	/// </summary>
	/// <param name="grantScope">The scope/qualifier of the proposed grant.</param>
	/// <param name="grantType">The type of the proposed grant.</param>
	/// <param name="riskScore">The risk score assessed for this grant (0-100).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The ordered list of approval steps for this request.</returns>
	Task<IReadOnlyList<ApprovalStepTemplate>> GetApprovalStepsAsync(
		string grantScope, string grantType, int riskScore, CancellationToken cancellationToken);
}
