// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Represents health information for an individual Elasticsearch node.
/// </summary>
public sealed class NodeHealthInfo
{
	/// <summary>
	/// Gets or sets the node ID.
	/// </summary>
	/// <value>
	/// The node ID.
	/// </value>
	public string NodeId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the node name.
	/// </summary>
	/// <value>
	/// The node name.
	/// </value>
	public string? NodeName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the node is considered healthy.
	/// </summary>
	/// <value>
	/// A value indicating whether the node is considered healthy.
	/// </value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets the CPU usage percentage.
	/// </summary>
	/// <value>
	/// The CPU usage percentage.
	/// </value>
	public double? CpuUsagePercent { get; set; }

	/// <summary>
	/// Gets or sets the memory usage percentage.
	/// </summary>
	/// <value>
	/// The memory usage percentage.
	/// </value>
	public double? MemoryUsagePercent { get; set; }

	/// <summary>
	/// Gets or sets the maximum disk usage percentage across all data paths.
	/// </summary>
	/// <value>
	/// The maximum disk usage percentage across all data paths.
	/// </value>
	public double? DiskUsagePercent { get; set; }

	/// <summary>
	/// Gets or sets the JVM heap usage percentage.
	/// </summary>
	/// <value>
	/// The JVM heap usage percentage.
	/// </value>
	public double? HeapUsagePercent { get; set; }

	/// <summary>
	/// Gets or sets the 1-minute load average.
	/// </summary>
	/// <value>
	/// The 1-minute load average.
	/// </value>
	public double? LoadAverage { get; set; }

	/// <summary>
	/// Gets or sets the error message if health checking failed for this node.
	/// </summary>
	/// <value>
	/// The error message if health checking failed for this node.
	/// </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when this node health was last updated.
	/// </summary>
	/// <value>
	/// The timestamp when this node health was last updated.
	/// </value>
	public DateTimeOffset LastUpdated { get; set; }
}
