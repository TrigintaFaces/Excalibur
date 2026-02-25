// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Represents route evaluation results.
/// </summary>
public sealed class RouteEvaluation
{
	/// <summary>
	/// Gets or sets the route definition.
	/// </summary>
	/// <value>
	/// The route definition.
	/// </value>
	public RouteDefinition Route { get; set; } = new();

	/// <summary>
	/// Gets the matching rules.
	/// </summary>
	/// <value>
	/// The matching rules.
	/// </value>
	public Collection<TimeBasedRoutingRule> MatchingRules { get; } = [];

	/// <summary>
	/// Gets or sets the evaluation score.
	/// </summary>
	/// <value>
	/// The evaluation score.
	/// </value>
	public double Score { get; set; }

	/// <summary>
	/// Gets or sets the reason for selection or rejection.
	/// </summary>
	/// <value>
	/// The reason for selection or rejection.
	/// </value>
	public string Reason { get; set; } = string.Empty;
}
