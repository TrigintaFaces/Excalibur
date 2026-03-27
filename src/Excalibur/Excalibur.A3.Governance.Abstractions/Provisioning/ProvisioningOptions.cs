// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Configuration options for provisioning workflows.
/// </summary>
public sealed class ProvisioningOptions
{
	/// <summary>
	/// Gets or sets the default approval timeout for provisioning steps.
	/// </summary>
	/// <value>Defaults to 72 hours.</value>
	public TimeSpan DefaultApprovalTimeout { get; set; } = TimeSpan.FromHours(72);

	/// <summary>
	/// Gets or sets a value indicating whether risk assessment is required
	/// before provisioning requests enter the approval workflow.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool RequireRiskAssessment { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether just-in-time (JIT) access
	/// is enabled for provisioning workflows.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool EnableJitAccess { get; set; }
}
