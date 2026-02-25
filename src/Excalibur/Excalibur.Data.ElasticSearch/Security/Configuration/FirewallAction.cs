// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines firewall actions for connection handling.
/// </summary>
public enum FirewallAction
{
	/// <summary>
	/// Allow the connection.
	/// </summary>
	Allow = 0,

	/// <summary>
	/// Deny the connection silently.
	/// </summary>
	Deny = 1,

	/// <summary>
	/// Reject the connection with notification.
	/// </summary>
	Reject = 2,
}
