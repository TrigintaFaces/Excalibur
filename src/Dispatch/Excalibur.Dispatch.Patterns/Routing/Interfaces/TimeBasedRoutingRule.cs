// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Represents a time-based routing rule.
/// </summary>
public sealed class TimeBasedRoutingRule
{
	/// <summary>
	/// Gets or sets the unique identifier for the rule.
	/// </summary>
	/// <value>
	/// The unique identifier for the rule.
	/// </value>
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the name of the rule.
	/// </summary>
	/// <value>
	/// The name of the rule.
	/// </value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the description of the rule.
	/// </summary>
	/// <value>
	/// The description of the rule.
	/// </value>
	public string? Description { get; set; }

	/// <summary>
	/// Gets or sets the priority of the rule (higher values have higher priority).
	/// </summary>
	/// <value>
	/// The priority of the rule (higher values have higher priority).
	/// </value>
	public int Priority { get; set; }

	/// <summary>
	/// Gets or sets the time conditions for this rule.
	/// </summary>
	/// <value>
	/// The time conditions for this rule.
	/// </value>
	public TimeConditions Conditions { get; set; } = new();

	/// <summary>
	/// Gets the routes to use when this rule matches.
	/// </summary>
	/// <value>
	/// The routes to use when this rule matches.
	/// </value>
	public Collection<RouteDefinition> Routes { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether this rule is enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether this rule is enabled.
	/// </value>
	public bool IsEnabled { get; set; } = true;

	/// <summary>
	/// Gets the metadata associated with this rule.
	/// </summary>
	/// <value>
	/// The metadata associated with this rule.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];
}
