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
public sealed class AutomatedResponseTriggeredEventArgs(ThreatType threatType) : EventArgs
{
	/// <summary>
	/// Gets the type of threat that triggered the automated response.
	/// </summary>
	/// <value>
	/// A ThreatType enumeration value indicating the category of security threat that caused the automated response to be triggered.
	/// </value>
	public ThreatType ThreatType { get; } = threatType;

	/// <summary>
	/// Gets the timestamp when the automated response was triggered.
	/// </summary>
	/// <value>
	/// A DateTimeOffset representing the exact moment when the automated response action was initiated by the security monitoring system.
	/// </value>
	public DateTimeOffset TriggeredAt { get; } = DateTimeOffset.UtcNow;
}
