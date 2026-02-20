// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents an automated security response configuration.
/// </summary>
public sealed class AutomatedSecurityResponse
{
	/// <summary>
	/// Gets or sets the type of threat this automated response is configured for.
	/// </summary>
	/// <value> The threat type that triggers this automated response. </value>
	public ThreatType ThreatType { get; set; }

	/// <summary>
	/// Gets or sets the action to be taken when this automated response is triggered.
	/// </summary>
	/// <value> A string describing the response action to execute. </value>
	public string ResponseAction { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether this automated security response is enabled.
	/// </summary>
	/// <value> True if the automated response is active, false otherwise. </value>
	public bool Enabled { get; set; }
}
