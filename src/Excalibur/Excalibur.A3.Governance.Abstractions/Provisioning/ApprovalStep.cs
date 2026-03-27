// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Represents a concrete approval step within a <c>ProvisioningRequest</c>.
/// </summary>
/// <remarks>
/// Created from an <see cref="ApprovalStepTemplate"/> when a provisioning request
/// enters review. Tracks the actual decision, justification, and decider.
/// </remarks>
/// <param name="StepId">Unique identifier for this step instance.</param>
/// <param name="ApproverRole">The role required to approve this step.</param>
/// <param name="Outcome">The approval outcome, or <see langword="null"/> if not yet decided.</param>
/// <param name="Justification">Optional justification provided by the decider.</param>
/// <param name="DecidedAt">When the decision was made, or <see langword="null"/> if pending.</param>
/// <param name="DecidedBy">The identity of the decider, or <see langword="null"/> if pending.</param>
public sealed record ApprovalStep(
	string StepId,
	string ApproverRole,
	ApprovalOutcome? Outcome,
	string? Justification,
	DateTimeOffset? DecidedAt,
	string? DecidedBy);
