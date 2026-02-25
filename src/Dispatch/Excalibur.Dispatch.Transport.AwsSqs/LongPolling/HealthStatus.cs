// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents the health status of the long polling optimizer.
/// </summary>
public sealed class HealthStatus
{
	/// <summary>
	/// Gets or sets a value indicating whether the optimizer is healthy.
	/// </summary>
	/// <value>
	/// A value indicating whether the optimizer is healthy.
	/// </value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets the current status.
	/// </summary>
	/// <value>
	/// The current status.
	/// </value>
	public string Status { get; set; } = "Initialized";

	/// <summary>
	/// Gets or sets the number of active queues.
	/// </summary>
	/// <value>
	/// The number of active queues.
	/// </value>
	public int ActiveQueues { get; set; }

	/// <summary>
	/// Gets or sets the total messages processed.
	/// </summary>
	/// <value>
	/// The total messages processed.
	/// </value>
	public long TotalMessagesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the efficiency score.
	/// </summary>
	/// <value>
	/// The efficiency score.
	/// </value>
	public double EfficiencyScore { get; set; }

	/// <summary>
	/// Gets or sets the last activity time.
	/// </summary>
	/// <value>
	/// The last activity time.
	/// </value>
	public DateTimeOffset LastActivityTime { get; set; }

	/// <summary>
	/// Gets additional health details.
	/// </summary>
	/// <value>
	/// Additional health details.
	/// </value>
	public Dictionary<string, object> Details { get; } = [];
}
