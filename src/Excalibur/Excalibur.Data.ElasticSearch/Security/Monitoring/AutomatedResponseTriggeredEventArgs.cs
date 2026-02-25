// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event args for automated response events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AutomatedResponseTriggeredEventArgs" /> class.
/// </remarks>
/// <param name="threatType"> The type of threat that triggered the automated response. </param>
/// <param name="responseAction"> The description of the automated response action that was triggered. </param>
public sealed class AutomatedResponseTriggeredEventArgs(ThreatType threatType, string responseAction) : EventArgs
{
	/// <summary>
	/// Gets the type of threat that triggered the automated response.
	/// </summary>
	/// <value>
	/// A ThreatType enumeration value indicating the category of security threat that caused the automated response to be triggered.
	/// </value>
	public ThreatType ThreatType { get; } = threatType;

	/// <summary>
	/// Gets the description of the automated response action that was triggered.
	/// </summary>
	/// <value>
	/// A string describing the specific automated response action taken by the security monitoring system in response to the detected threat.
	/// </value>
	public string ResponseAction { get; } = responseAction;

	/// <summary>
	/// Gets the timestamp when the automated response was triggered.
	/// </summary>
	/// <value>
	/// A DateTimeOffset representing the exact moment when the automated response action was initiated by the security monitoring system.
	/// </value>
	public DateTimeOffset TriggeredAt { get; } = DateTimeOffset.UtcNow;
}
