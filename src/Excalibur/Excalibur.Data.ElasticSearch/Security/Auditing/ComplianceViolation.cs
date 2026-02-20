// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a compliance violation.
/// </summary>
public sealed class ComplianceViolation
{
	/// <summary>
	/// Gets or sets the type of compliance violation.
	/// </summary>
	/// <value> The classification of the compliance violation. </value>
	public string ViolationType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the description of the compliance violation.
	/// </summary>
	/// <value> A detailed explanation of the compliance violation and its implications. </value>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the severity level of the compliance violation.
	/// </summary>
	/// <value> The security risk level indicating the severity of the compliance violation. </value>
	public SecurityRiskLevel Severity { get; set; }
}
