// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Determines when an approval step is required in a provisioning workflow.
/// </summary>
public enum ApprovalCondition
{
	/// <summary>
	/// The approval step is always required regardless of context.
	/// </summary>
	Always = 0,

	/// <summary>
	/// The approval step is required when the grant's risk score exceeds a configured threshold.
	/// </summary>
	RiskScoreAbove = 1,

	/// <summary>
	/// The approval step is required when the grant scope is marked as sensitive.
	/// </summary>
	SensitiveScope = 2,
}
