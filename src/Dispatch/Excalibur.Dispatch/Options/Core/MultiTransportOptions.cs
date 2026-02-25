// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for multiple transports.
/// </summary>
public sealed class MultiTransportOptions
{
	/// <summary>
	/// Gets the configured transports.
	/// </summary>
	/// <value> The transport configurations keyed by transport name. </value>
	public IDictionary<string, TransportConfiguration> Transports { get; } =
		new Dictionary<string, TransportConfiguration>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the default transport name.
	/// </summary>
	/// <value> The transport used when no specific transport is specified. </value>
	public string? DefaultTransport { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable transport failover.
	/// </summary>
	/// <value> <see langword="true" /> to enable transport failover; otherwise, <see langword="false" />. </value>
	public bool EnableFailover { get; set; } = true;
}
