// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Provisioning;

namespace Excalibur.A3.Governance;

/// <summary>
/// Default workflow configuration that requires a single "Manager" approval for all requests.
/// </summary>
internal sealed class DefaultSingleApproverWorkflow : IProvisioningWorkflowConfiguration
{
	private static readonly IReadOnlyList<ApprovalStepTemplate> DefaultSteps =
	[
		new ApprovalStepTemplate(
			StepOrder: 1,
			ApproverRole: "Manager",
			IsRequired: true,
			Condition: ApprovalCondition.Always),
	];

	/// <inheritdoc />
	public Task<IReadOnlyList<ApprovalStepTemplate>> GetApprovalStepsAsync(
		string grantScope,
		string grantType,
		int riskScore,
		CancellationToken cancellationToken) =>
		Task.FromResult(DefaultSteps);
}
