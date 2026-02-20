// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Cluster;
using Elastic.Clients.Elasticsearch.Nodes;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Provides comprehensive health monitoring for Elasticsearch cluster and individual nodes with background polling.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ElasticsearchHealthMonitor" /> class.
/// </remarks>
/// <param name="client"> The Elasticsearch client for health checks. </param>
/// <param name="metrics"> The metrics collector for health status. </param>
/// <param name="logger"> The logger for health monitoring. </param>
/// <param name="options"> The monitoring configuration options. </param>
public class ElasticsearchHealthMonitor(
	ElasticsearchClient client,
	ElasticsearchMetrics metrics,
	ILogger<ElasticsearchHealthMonitor> logger,
	IOptions<ElasticsearchMonitoringOptions> options) : BackgroundService
{
	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ElasticsearchMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
	private readonly ILogger<ElasticsearchHealthMonitor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly HealthMonitoringOptions _settings = options?.Value?.Health ?? throw new ArgumentNullException(nameof(options));
	private readonly ConcurrentDictionary<string, NodeHealthInfo> _nodeHealthCache = new(StringComparer.Ordinal);
	private volatile ElasticsearchClusterHealth? _lastClusterHealth;

	/// <summary>
	/// Gets the last known cluster health information.
	/// </summary>
	/// <value>
	/// The last known cluster health information.
	/// </value>
	public ElasticsearchClusterHealth? LastClusterHealth => _lastClusterHealth;

	/// <summary>
	/// Gets the timestamp of the last health check.
	/// </summary>
	/// <value>
	/// The timestamp of the last health check.
	/// </value>
	public DateTimeOffset LastHealthCheckTime { get; private set; } = DateTimeOffset.MinValue;

	/// <summary>
	/// Gets the current health status of all monitored nodes.
	/// </summary>
	/// <returns> A dictionary of node IDs and their health information. </returns>
	public IReadOnlyDictionary<string, NodeHealthInfo> GetNodeHealthStatus() =>
		_nodeHealthCache.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);

	/// <summary>
	/// Performs an immediate health check of the Elasticsearch cluster.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the health check operation with cluster health information. </returns>
	public async Task<ElasticsearchClusterHealth> CheckHealthAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var timeout = new CancellationTokenSource(_settings.HealthCheckTimeout);
			using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

			var healthResponse = await _client.Cluster.HealthAsync(cancellationToken: combinedToken.Token).ConfigureAwait(false);

			var clusterHealth = await ProcessClusterHealthResponse(healthResponse).ConfigureAwait(false);

			// Update metrics
			_metrics.UpdateHealthStatus(clusterHealth.IsHealthy, clusterHealth.Status.ToString());

			// Check individual nodes if enabled
			if (_settings.MonitorNodeHealth)
			{
				await CheckNodeHealthAsync(combinedToken.Token).ConfigureAwait(false);
			}

			// Get cluster stats if enabled
			if (_settings.MonitorClusterStats)
			{
				await GetClusterStatsAsync(clusterHealth, combinedToken.Token).ConfigureAwait(false);
			}

			_lastClusterHealth = clusterHealth;
			LastHealthCheckTime = DateTimeOffset.UtcNow;

			return clusterHealth;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to perform cluster health check");

			var errorHealth = new ElasticsearchClusterHealth
			{
				IsHealthy = false,
				Status = HealthStatus.Red,
				ErrorMessage = ex.Message,
				Timestamp = DateTimeOffset.UtcNow,
			};

			_metrics.UpdateHealthStatus(isHealthy: false, "error");
			_lastClusterHealth = errorHealth;
			LastHealthCheckTime = DateTimeOffset.UtcNow;

			return errorHealth;
		}
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_settings.Enabled)
		{
			_logger.LogInformation("Elasticsearch health monitoring is disabled");
			return;
		}

		_logger.LogInformation(
			"Starting Elasticsearch health monitoring with {Interval} interval",
			_settings.HealthCheckInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				_ = await CheckHealthAsync(stoppingToken).ConfigureAwait(false);
				await Task.Delay(_settings.HealthCheckInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during health monitoring execution");
				try
				{
					await Task.Delay(_settings.HealthCheckInterval, stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		_logger.LogInformation("Elasticsearch health monitoring stopped");
	}

	/// <summary>
	/// Creates node health information from node info and stats.
	/// </summary>
	/// <param name="nodeId"> The node ID. </param>
	/// <param name="nodeInfo"> The node information. </param>
	/// <param name="statsResponse"> The node stats response. </param>
	/// <returns> The node health information. </returns>
	private static NodeHealthInfo CreateNodeHealthInfo(
		string nodeId,
		NodeInfo nodeInfo,
		NodesStatsResponse statsResponse)
	{
		var nodeHealth = new NodeHealthInfo
		{
			NodeId = nodeId,
			NodeName = nodeInfo.Name,
			IsHealthy = statsResponse.IsValidResponse,
			LastUpdated = DateTimeOffset.UtcNow,
		};

		if (!statsResponse.IsValidResponse)
		{
			nodeHealth.ErrorMessage = statsResponse.ElasticsearchServerError?.ToString();
			return nodeHealth;
		}

		// Extract relevant stats if available
		if (statsResponse.Nodes.TryGetValue(nodeId, out var nodeStats))
		{
			nodeHealth.CpuUsagePercent = nodeStats.Os?.Cpu?.Percent;
			nodeHealth.MemoryUsagePercent = nodeStats.Os?.Mem?.UsedPercent;
			nodeHealth.DiskUsagePercent = CalculateMaxDiskUsage(nodeStats);
			nodeHealth.HeapUsagePercent = nodeStats.Jvm?.Mem?.HeapUsedPercent;

			// LoadAverage is typically accessed differently in newer Elasticsearch versions Using the first value from LoadAverage array if available
			nodeHealth.LoadAverage = nodeStats.Os?.Cpu?.LoadAverage?.FirstOrDefault().Value;
		}

		// Determine health based on thresholds
		nodeHealth.IsHealthy = IsNodeHealthy(nodeHealth);

		return nodeHealth;
	}

	/// <summary>
	/// Calculates the maximum disk usage percentage across all data paths.
	/// </summary>
	/// <param name="nodeStats"> The node statistics. </param>
	/// <returns> The maximum disk usage percentage, or null if not available. </returns>
	private static double? CalculateMaxDiskUsage(Stats nodeStats) =>

		// FileSystem data is accessed differently in newer Elasticsearch versions Returning null for now as the API has changed significantly
		// This will be updated to use the correct API once the new structure is determined
		null;

	/// <summary>
	/// Determines if a node is healthy based on its metrics.
	/// </summary>
	/// <param name="nodeHealth"> The node health information. </param>
	/// <returns> True if the node is considered healthy, false otherwise. </returns>
	private static bool IsNodeHealthy(NodeHealthInfo nodeHealth)
	{
		// Consider unhealthy if any critical metric exceeds threshold
		if (nodeHealth.CpuUsagePercent > 90)
		{
			return false;
		}

		if (nodeHealth.MemoryUsagePercent > 95)
		{
			return false;
		}

		if (nodeHealth.DiskUsagePercent > 95)
		{
			return false;
		}

		if (nodeHealth.HeapUsagePercent > 90)
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Processes the cluster health response and creates health information.
	/// </summary>
	/// <param name="healthResponse"> The health response from Elasticsearch. </param>
	/// <returns> A task representing the processing operation with cluster health information. </returns>
	private async Task<ElasticsearchClusterHealth> ProcessClusterHealthResponse(HealthResponse healthResponse)
	{
		await Task.CompletedTask.ConfigureAwait(false);
		var clusterHealth = new ElasticsearchClusterHealth
		{
			IsHealthy = healthResponse.IsValidResponse && healthResponse.Status != HealthStatus.Red,
			Status = healthResponse.Status,
			ClusterName = healthResponse.ClusterName,
			NumberOfNodes = healthResponse.NumberOfNodes,
			NumberOfDataNodes = healthResponse.NumberOfDataNodes,
			ActivePrimaryShards = healthResponse.ActivePrimaryShards,
			ActiveShards = healthResponse.ActiveShards,
			RelocatingShards = healthResponse.RelocatingShards,
			InitializingShards = healthResponse.InitializingShards,
			UnassignedShards = healthResponse.UnassignedShards,
			DelayedUnassignedShards = healthResponse.DelayedUnassignedShards,
			NumberOfPendingTasks = healthResponse.NumberOfPendingTasks,
			NumberOfInFlightFetch = healthResponse.NumberOfInFlightFetch,
			TaskMaxWaitingInQueueMillis = healthResponse.TaskMaxWaitingInQueueMillis,
			ActiveShardsPercentAsNumber = healthResponse.ActiveShardsPercentAsNumber,
			Timestamp = DateTimeOffset.UtcNow,
		};

		if (!healthResponse.IsValidResponse)
		{
			clusterHealth.ErrorMessage = healthResponse.ElasticsearchServerError?.ToString();
		}

		// Log health status changes
		if (_lastClusterHealth?.Status != clusterHealth.Status)
		{
			var logLevel = clusterHealth.Status switch
			{
				HealthStatus.Green => LogLevel.Information,
				HealthStatus.Yellow => LogLevel.Warning,
				HealthStatus.Red => LogLevel.Error,
				_ => LogLevel.Warning,
			};

			_logger.Log(
				logLevel,
				"Elasticsearch cluster health status changed from {OldStatus} to {NewStatus} {@ClusterHealth}",
				_lastClusterHealth?.Status.ToString() ?? "Unknown",
				clusterHealth.Status,
				clusterHealth);
		}

		return clusterHealth;
	}

	/// <summary>
	/// Checks the health of individual nodes in the cluster.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the node health check operation. </returns>
	private async Task CheckNodeHealthAsync(CancellationToken cancellationToken)
	{
		try
		{
			var nodesResponse = await _client.Nodes.InfoAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			if (!nodesResponse.IsValidResponse)
			{
				_logger.LogWarning(
					"Failed to retrieve node information: {Error}",
					nodesResponse.ElasticsearchServerError?.ToString());
				return;
			}

			var nodeHealthTasks = nodesResponse.Nodes.Select(async kvp =>
			{
				var nodeId = kvp.Key;
				var nodeInfo = kvp.Value;

				try
				{
					// Get node stats for health assessment
					var statsResponse = await _client.Nodes.StatsAsync(
						new NodesStatsRequestDescriptor().NodeId(new NodeIds(new[] { nodeId })),
						cancellationToken).ConfigureAwait(false);

					var nodeHealth = CreateNodeHealthInfo(nodeId, nodeInfo, statsResponse);
					_ = _nodeHealthCache.AddOrUpdate(nodeId, static (_, newHealth) => newHealth, static (_, _, newHealth) => newHealth, nodeHealth);

					return nodeHealth;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to get health for node {NodeId}", nodeId);

					var errorHealth = new NodeHealthInfo
					{
						NodeId = nodeId,
						NodeName = nodeInfo.Name,
						IsHealthy = false,
						ErrorMessage = ex.Message,
						LastUpdated = DateTimeOffset.UtcNow,
					};

					_ = _nodeHealthCache.AddOrUpdate(nodeId, static (_, newHealth) => newHealth, static (_, _, newHealth) => newHealth, errorHealth);
					return errorHealth;
				}
			});

			_ = await Task.WhenAll(nodeHealthTasks).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check node health");
		}
	}

	/// <summary>
	/// Gets additional cluster statistics if monitoring is enabled.
	/// </summary>
	/// <param name="clusterHealth"> The cluster health to augment with statistics. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the cluster stats operation. </returns>
	private async Task GetClusterStatsAsync(ElasticsearchClusterHealth clusterHealth, CancellationToken cancellationToken)
	{
		try
		{
			var statsResponse = await _client.Cluster.StatsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			if (statsResponse.IsValidResponse)
			{
				clusterHealth.TotalDocuments = statsResponse.Indices?.Docs?.Count;
				clusterHealth.TotalSizeInBytes = statsResponse.Indices?.Store?.SizeInBytes;
				clusterHealth.NodeCount = statsResponse.Nodes?.Count?.Total;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get cluster statistics");
		}
	}
}
