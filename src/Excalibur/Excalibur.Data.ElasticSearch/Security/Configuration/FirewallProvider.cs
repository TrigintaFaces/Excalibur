// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the supported firewall providers.
/// </summary>
public enum FirewallProvider
{
	/// <summary>
	/// Windows Firewall integration.
	/// </summary>
	WindowsFirewall = 0,

	/// <summary>
	/// Linux iptables integration.
	/// </summary>
	IpTables = 1,

	/// <summary>
	/// pfSense firewall integration.
	/// </summary>
	PfSense = 2,

	/// <summary>
	/// Cloud provider firewall (AWS Security Groups, Azure NSG, etc.).
	/// </summary>
	CloudProvider = 3,
}
