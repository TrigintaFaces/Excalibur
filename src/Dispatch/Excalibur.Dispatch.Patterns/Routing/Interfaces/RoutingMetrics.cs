// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Represents routing metrics for monitoring.
/// </summary>
public sealed class RoutingMetrics
{
	/// <summary>
	/// Gets or sets the total number of routing decisions.
	/// </summary>
	/// <value>
	/// The total number of routing decisions.
	/// </value>
	public long TotalRoutingDecisions { get; set; }

	/// <summary>
	/// Gets or sets the number of successful routings.
	/// </summary>
	/// <value>
	/// The number of successful routings.
	/// </value>
	public long SuccessfulRoutings { get; set; }

	/// <summary>
	/// Gets or sets the number of failed routings.
	/// </summary>
	/// <value>
	/// The number of failed routings.
	/// </value>
	public long FailedRoutings { get; set; }

	/// <summary>
	/// Gets route usage statistics.
	/// </summary>
	/// <value>
	/// Route usage statistics.
	/// </value>
	public Dictionary<string, long> RouteUsage { get; } = [];

	/// <summary>
	/// Gets rule match statistics.
	/// </summary>
	/// <value>
	/// Rule match statistics.
	/// </value>
	public Dictionary<string, long> RuleMatches { get; } = [];

	/// <summary>
	/// Gets or sets the average routing decision time.
	/// </summary>
	/// <value>
	/// The average routing decision time.
	/// </value>
	public TimeSpan AverageDecisionTime { get; set; }

	/// <summary>
	/// Gets or sets the last reset timestamp.
	/// </summary>
	/// <value>
	/// The last reset timestamp.
	/// </value>
	public DateTimeOffset LastReset { get; set; } = DateTimeOffset.UtcNow;
}
