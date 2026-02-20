// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Represents the result of a routing decision.
/// </summary>
public sealed class RoutingResult
{
	/// <summary>
	/// Gets or sets the selected route.
	/// </summary>
	/// <value>
	/// The selected route.
	/// </value>
	public RouteDefinition? SelectedRoute { get; set; }

	/// <summary>
	/// Gets alternative routes if primary fails.
	/// </summary>
	/// <value>
	/// Alternative routes if primary fails.
	/// </value>
	public Collection<RouteDefinition> AlternativeRoutes { get; } = [];

	/// <summary>
	/// Gets or sets the rule that was applied.
	/// </summary>
	/// <value>
	/// The rule that was applied.
	/// </value>
	public TimeBasedRoutingRule? AppliedRule { get; set; }

	/// <summary>
	/// Gets or sets the routing decision timestamp.
	/// </summary>
	/// <value>
	/// The routing decision timestamp.
	/// </value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the routing decision metadata.
	/// </summary>
	/// <value>
	/// The routing decision metadata.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether routing was successful.
	/// </summary>
	/// <value>
	/// A value indicating whether routing was successful.
	/// </value>
	public bool IsSuccess { get; set; }

	/// <summary>
	/// Gets or sets the reason if routing failed.
	/// </summary>
	/// <value>
	/// The reason if routing failed.
	/// </value>
	public string? FailureReason { get; set; }
}
