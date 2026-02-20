// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Configuration for a specific transport.
/// </summary>
public sealed class TransportConfiguration
{
	/// <summary>
	/// Gets or sets the transport name.
	/// </summary>
	/// <value> The unique name identifying the transport. </value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the transport is enabled.
	/// </summary>
	/// <value> <see langword="true" /> when the transport is active; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the transport priority for failover scenarios.
	/// </summary>
	/// <value> The failover priority where lower values indicate higher precedence. </value>
	public int Priority { get; set; } = 100;
}
