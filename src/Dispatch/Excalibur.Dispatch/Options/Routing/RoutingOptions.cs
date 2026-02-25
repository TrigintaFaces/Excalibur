// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Routing;

/// <summary>
/// Configuration options for message routing behavior.
/// </summary>
/// <remarks>
/// These options control how messages are routed to different message buses, including default bus selection and routing policy configuration.
/// </remarks>
public sealed class RoutingOptions
{
	/// <summary>
	/// Gets or sets optional path or policy for dynamic route Excalibur.Tests.Integration.
	/// </summary>
	/// <value>The current <see cref="RoutingPolicyPath"/> value.</value>
	public string? RoutingPolicyPath { get; set; }

	/// <summary>
	/// Gets or sets name of the default remote message bus.
	/// </summary>
	/// <value>The current <see cref="DefaultRemoteBusName"/> value.</value>
	public string? DefaultRemoteBusName { get; set; }
}
