// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures firewall integration and rule management.
/// </summary>
public sealed class FirewallOptions
{
	/// <summary>
	/// Gets a value indicating whether firewall integration is enabled.
	/// </summary>
	/// <value> True to enable automatic firewall rule management, false otherwise. </value>
	public bool Enabled { get; init; }

	/// <summary>
	/// Gets the firewall provider type.
	/// </summary>
	/// <value> The type of firewall system to integrate with. </value>
	public FirewallProvider Provider { get; init; } = FirewallProvider.WindowsFirewall;

	/// <summary>
	/// Gets a value indicating whether to automatically create firewall rules.
	/// </summary>
	/// <value> True to automatically manage firewall rules, false for manual management. </value>
	public bool AutoCreateRules { get; init; } = true;

	/// <summary>
	/// Gets the default action for unknown connections.
	/// </summary>
	/// <value> The action to take for connections not explicitly allowed or denied. </value>
	public FirewallAction DefaultAction { get; init; } = FirewallAction.Deny;
}
