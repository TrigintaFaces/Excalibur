// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Defines a template for an approval step in a provisioning workflow.
/// </summary>
/// <remarks>
/// Templates describe the required approval steps before they are instantiated
/// as concrete <see cref="ApprovalStep"/> records on a <c>ProvisioningRequest</c>.
/// </remarks>
/// <param name="StepOrder">The ordinal position of this step in the workflow (1-based).</param>
/// <param name="ApproverRole">The role required to approve this step (e.g., "Manager", "SecurityOfficer").</param>
/// <param name="IsRequired">Whether this step must be completed for the request to proceed.</param>
/// <param name="Condition">The condition under which this step applies.</param>
/// <param name="ConditionThreshold">Optional threshold for condition evaluation (e.g., risk score threshold for <see cref="ApprovalCondition.RiskScoreAbove"/>).</param>
public sealed record ApprovalStepTemplate(
	int StepOrder,
	string ApproverRole,
	bool IsRequired,
	ApprovalCondition Condition = ApprovalCondition.Always,
	int? ConditionThreshold = null);
