// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Represents the outcome of an approval step decision.
/// </summary>
public enum ApprovalOutcome
{
	/// <summary>
	/// The step was approved.
	/// </summary>
	Approved = 0,

	/// <summary>
	/// The step was denied.
	/// </summary>
	Denied = 1,
}
