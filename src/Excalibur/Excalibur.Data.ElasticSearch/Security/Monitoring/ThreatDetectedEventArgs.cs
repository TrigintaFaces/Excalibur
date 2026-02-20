// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event args for threat detection events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ThreatDetectedEventArgs" /> class with the specified threat type and description.
/// </remarks>
/// <param name="threatType"> The type of threat that was detected. </param>
/// <param name="description"> A detailed description of the detected threat. </param>
public sealed class ThreatDetectedEventArgs(ThreatType threatType, string description) : EventArgs
{
	/// <summary>
	/// Gets the type of threat that was detected.
	/// </summary>
	/// <value> A ThreatType enumeration value indicating the category or classification of the detected security threat. </value>
	public ThreatType ThreatType { get; } = threatType;

	/// <summary>
	/// Gets the description of the detected threat.
	/// </summary>
	/// <value>
	/// A string containing detailed information about the detected security threat, including context and relevant details for security analysis.
	/// </value>
	public string Description { get; } = description;

	/// <summary>
	/// Gets the timestamp when the threat was detected.
	/// </summary>
	/// <value> A DateTimeOffset representing the exact time when the security threat was detected by the monitoring system. </value>
	public DateTimeOffset DetectedAt { get; } = DateTimeOffset.UtcNow;
}
