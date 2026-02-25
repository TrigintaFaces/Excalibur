// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures threat intelligence integration for enhanced security monitoring.
/// </summary>
public sealed class ThreatIntelligenceOptions
{
	/// <summary>
	/// Gets a value indicating whether threat intelligence integration is enabled.
	/// </summary>
	/// <value> True to integrate with threat intelligence feeds, false otherwise. </value>
	public bool Enabled { get; init; }

	/// <summary>
	/// Gets the threat intelligence feed URLs.
	/// </summary>
	/// <value> List of threat intelligence feed endpoints to consume. </value>
	public List<string> FeedUrls { get; init; } = [];

	/// <summary>
	/// Gets the feed update interval.
	/// </summary>
	/// <value> The time interval between threat intelligence feed updates. Defaults to 1 hour. </value>
	public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets a value indicating whether to automatically block known malicious IPs.
	/// </summary>
	/// <value> True to automatically update IP blacklists from threat feeds, false for manual review. </value>
	public bool AutoBlockMaliciousIps { get; init; } = true;
}
