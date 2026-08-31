// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures network security controls and access restrictions.
/// </summary>
public sealed class NetworkSecurityOptions
{
	/// <summary>
	/// Gets a value indicating whether network security controls are enabled.
	/// </summary>
	/// <value> True to enable network-level security controls, false otherwise. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the IP address whitelist for allowed connections.
	/// </summary>
	/// <value> List of IP addresses or CIDR ranges allowed to connect. </value>
	public List<string> IpWhitelist { get; init; } = [];

	/// <summary>
	/// Gets the IP address blacklist for blocked connections.
	/// </summary>
	/// <value> List of IP addresses or CIDR ranges to block. </value>
	public List<string> IpBlacklist { get; init; } = [];
}
