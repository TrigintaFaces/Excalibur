// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Represents the result of a routing evaluation.
/// </summary>
public sealed class RoutingResult
{
	/// <summary>
	/// Gets or sets the name of the target bus.
	/// </summary>
	/// <value>The current <see cref="BusName"/> value.</value>
	public string? BusName { get; set; }

	/// <summary>
	/// Gets the routing metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, object>? Metadata { get; init; }
}
