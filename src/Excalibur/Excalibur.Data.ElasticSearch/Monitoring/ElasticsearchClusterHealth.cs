// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Represents comprehensive health information for an Elasticsearch cluster.
/// </summary>
public sealed class ElasticsearchClusterHealth
{
	/// <summary>
	/// Gets or sets a value indicating whether the cluster is considered healthy.
	/// </summary>
	/// <value>
	/// A value indicating whether the cluster is considered healthy.
	/// </value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets the cluster health status.
	/// </summary>
	/// <value>
	/// The cluster health status.
	/// </value>
	public HealthStatus Status { get; set; }

	/// <summary>
	/// Gets or sets the cluster name.
	/// </summary>
	/// <value>
	/// The cluster name.
	/// </value>
	public string? ClusterName { get; set; }

	/// <summary>
	/// Gets or sets the total number of nodes in the cluster.
	/// </summary>
	/// <value>
	/// The total number of nodes in the cluster.
	/// </value>
	public int? NumberOfNodes { get; set; }

	/// <summary>
	/// Gets or sets the number of data nodes in the cluster.
	/// </summary>
	/// <value>
	/// The number of data nodes in the cluster.
	/// </value>
	public int? NumberOfDataNodes { get; set; }

	/// <summary>
	/// Gets or sets the number of active primary shards.
	/// </summary>
	/// <value>
	/// The number of active primary shards.
	/// </value>
	public int? ActivePrimaryShards { get; set; }

	/// <summary>
	/// Gets or sets the total number of active shards.
	/// </summary>
	/// <value>
	/// The total number of active shards.
	/// </value>
	public int? ActiveShards { get; set; }

	/// <summary>
	/// Gets or sets the number of relocating shards.
	/// </summary>
	/// <value>
	/// The number of relocating shards.
	/// </value>
	public int? RelocatingShards { get; set; }

	/// <summary>
	/// Gets or sets the number of initializing shards.
	/// </summary>
	/// <value>
	/// The number of initializing shards.
	/// </value>
	public int? InitializingShards { get; set; }

	/// <summary>
	/// Gets or sets the number of unassigned shards.
	/// </summary>
	/// <value>
	/// The number of unassigned shards.
	/// </value>
	public int? UnassignedShards { get; set; }

	/// <summary>
	/// Gets or sets the number of delayed unassigned shards.
	/// </summary>
	/// <value>
	/// The number of delayed unassigned shards.
	/// </value>
	public int? DelayedUnassignedShards { get; set; }

	/// <summary>
	/// Gets or sets the number of pending tasks.
	/// </summary>
	/// <value>
	/// The number of pending tasks.
	/// </value>
	public int? NumberOfPendingTasks { get; set; }

	/// <summary>
	/// Gets or sets the number of in-flight fetch operations.
	/// </summary>
	/// <value>
	/// The number of in-flight fetch operations.
	/// </value>
	public int? NumberOfInFlightFetch { get; set; }

	/// <summary>
	/// Gets or sets the maximum time a task has been waiting in the queue.
	/// </summary>
	/// <value>
	/// The maximum time a task has been waiting in the queue.
	/// </value>
	public long? TaskMaxWaitingInQueueMillis { get; set; }

	/// <summary>
	/// Gets or sets the percentage of active shards.
	/// </summary>
	/// <value>
	/// The percentage of active shards.
	/// </value>
	public double? ActiveShardsPercentAsNumber { get; set; }

	/// <summary>
	/// Gets or sets the total number of documents in the cluster.
	/// </summary>
	/// <value>
	/// The total number of documents in the cluster.
	/// </value>
	public long? TotalDocuments { get; set; }

	/// <summary>
	/// Gets or sets the total size of the cluster in bytes.
	/// </summary>
	/// <value>
	/// The total size of the cluster in bytes.
	/// </value>
	public long? TotalSizeInBytes { get; set; }

	/// <summary>
	/// Gets or sets the total number of nodes (from cluster stats).
	/// </summary>
	/// <value>
	/// The total number of nodes (from cluster stats).
	/// </value>
	public int? NodeCount { get; set; }

	/// <summary>
	/// Gets or sets the error message if the health check failed.
	/// </summary>
	/// <value>
	/// The error message if the health check failed.
	/// </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when this health check was performed.
	/// </summary>
	/// <value>
	/// The timestamp when this health check was performed.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }
}
