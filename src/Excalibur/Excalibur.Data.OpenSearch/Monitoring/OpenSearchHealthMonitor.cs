// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Excalibur.Data.OpenSearch.Monitoring;

/// <summary>
/// Provides comprehensive health monitoring for OpenSearch cluster and individual nodes with background polling.
/// </summary>
internal sealed class OpenSearchHealthMonitor(
	OpenSearchClient client,
	OpenSearchMetrics metrics,
	ILogger<OpenSearchHealthMonitor> logger,
	IOptions<OpenSearchMonitoringOptions> options) : BackgroundService
{
	private readonly OpenSearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	#pragma warning disable CA2213 // Metrics lifetime is managed by DI container
	private readonly OpenSearchMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
#pragma warning restore CA2213
	private readonly ILogger<OpenSearchHealthMonitor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly HealthMonitoringOptions _settings = options?.Value?.Health ?? throw new ArgumentNullException(nameof(options));
	private readonly ConcurrentDictionary<string, NodeHealthInfo> _nodeHealthCache = new(StringComparer.Ordinal);
	private volatile OpenSearchClusterHealth? _lastClusterHealth;

	public OpenSearchClusterHealth? LastClusterHealth => _lastClusterHealth;

	public DateTimeOffset LastHealthCheckTime { get; private set; } = DateTimeOffset.MinValue;

	public IReadOnlyDictionary<string, NodeHealthInfo> GetNodeHealthStatus() =>
		_nodeHealthCache.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);

	public async Task<OpenSearchClusterHealth> CheckHealthAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var timeout = new CancellationTokenSource(_settings.HealthCheckTimeout);
			using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

			var healthResponse = await _client.Cluster.HealthAsync(new ClusterHealthRequest(), combinedToken.Token).ConfigureAwait(false);
			var clusterHealth = ProcessClusterHealthResponse(healthResponse);
			_metrics.UpdateHealthStatus(clusterHealth.IsHealthy, clusterHealth.Status);

			if (_settings.MonitorNodeHealth)
			{
				await CheckNodeHealthAsync(combinedToken.Token).ConfigureAwait(false);
			}

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
			var errorHealth = new OpenSearchClusterHealth
			{
				IsHealthy = false, Status = "red", ErrorMessage = ex.Message, Timestamp = DateTimeOffset.UtcNow,
			};
			_metrics.UpdateHealthStatus(isHealthy: false, "error");
			_lastClusterHealth = errorHealth;
			LastHealthCheckTime = DateTimeOffset.UtcNow;
			return errorHealth;
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_settings.Enabled)
		{
			_logger.LogInformation("OpenSearch health monitoring is disabled");
			return;
		}

		_logger.LogInformation("Starting OpenSearch health monitoring with {Interval} interval", _settings.HealthCheckInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				_ = await CheckHealthAsync(stoppingToken).ConfigureAwait(false);
				await Task.Delay(_settings.HealthCheckInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during health monitoring execution");
				try { await Task.Delay(_settings.HealthCheckInterval, stoppingToken).ConfigureAwait(false); }
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
			}
		}

		_logger.LogInformation("OpenSearch health monitoring stopped");
	}

	private static NodeHealthInfo CreateNodeHealthInfo(string nodeId, NodeInfo nodeInfo, NodesStatsResponse statsResponse)
	{
		var nodeHealth = new NodeHealthInfo
		{
			NodeId = nodeId, NodeName = nodeInfo.Name, IsHealthy = statsResponse.IsValid, LastUpdated = DateTimeOffset.UtcNow,
		};

		if (!statsResponse.IsValid)
		{
			nodeHealth.ErrorMessage = statsResponse.ServerError?.ToString();
			return nodeHealth;
		}

		if (statsResponse.Nodes.TryGetValue(nodeId, out var nodeStats))
		{
			nodeHealth.CpuUsagePercent = nodeStats.OperatingSystem?.Cpu?.Percent;
			nodeHealth.MemoryUsagePercent = nodeStats.OperatingSystem?.Memory?.UsedPercent;
			nodeHealth.DiskUsagePercent = CalculateMaxDiskUsage(nodeStats);
			nodeHealth.HeapUsagePercent = nodeStats.Jvm?.Memory?.HeapUsedPercent;
			nodeHealth.LoadAverage = nodeStats.OperatingSystem?.Cpu?.LoadAverage?.FifteenMinute;
		}

		nodeHealth.IsHealthy = IsNodeHealthy(nodeHealth);
		return nodeHealth;
	}

	private static double? CalculateMaxDiskUsage(NodeStats nodeStats)
	{
		var fileSystem = nodeStats.FileSystem;
		if (fileSystem?.Total != null)
		{
			var totalBytes = fileSystem.Total.TotalInBytes;
			var freeBytes = fileSystem.Total.FreeInBytes;
			if (totalBytes > 0)
			{
				var used = totalBytes - freeBytes;
				return (double)used / totalBytes * 100.0;
			}
		}

		return null;
	}

	private static bool IsNodeHealthy(NodeHealthInfo nodeHealth)
	{
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

	private OpenSearchClusterHealth ProcessClusterHealthResponse(ClusterHealthResponse healthResponse)
	{
		var statusStr = healthResponse.Status.ToString();
		var isRed = string.Equals(statusStr, "red", StringComparison.OrdinalIgnoreCase);
		var clusterHealth = new OpenSearchClusterHealth
		{
			IsHealthy = healthResponse.IsValid && !isRed,
			Status = statusStr,
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
			TaskMaxWaitingInQueueMillis = healthResponse.TaskMaxWaitTimeInQueueInMilliseconds,
			ActiveShardsPercentAsNumber = healthResponse.ActiveShardsPercentAsNumber,
			Timestamp = DateTimeOffset.UtcNow,
		};

		if (!healthResponse.IsValid)
		{
			clusterHealth.ErrorMessage = healthResponse.ServerError?.ToString();
		}

		if (_lastClusterHealth?.Status != clusterHealth.Status)
		{
			var logLevel = statusStr switch
			{
				_ when string.Equals(statusStr, "green", StringComparison.OrdinalIgnoreCase) => LogLevel.Information,
				_ when string.Equals(statusStr, "yellow", StringComparison.OrdinalIgnoreCase) => LogLevel.Warning,
				_ when isRed => LogLevel.Error,
				_ => LogLevel.Warning,
			};

			_logger.Log(logLevel, "OpenSearch cluster health status changed from {OldStatus} to {NewStatus} {@ClusterHealth}",
				_lastClusterHealth?.Status ?? "Unknown", clusterHealth.Status, clusterHealth);
		}

		return clusterHealth;
	}

	private async Task CheckNodeHealthAsync(CancellationToken cancellationToken)
	{
		try
		{
			var nodesResponse = await _client.Nodes.InfoAsync(new NodesInfoRequest(), cancellationToken).ConfigureAwait(false);
			if (!nodesResponse.IsValid)
			{
				_logger.LogWarning("Failed to retrieve node information: {Error}", nodesResponse.ServerError?.ToString());
				return;
			}

			var nodeHealthTasks = nodesResponse.Nodes.Select(async kvp =>
			{
				var nodeId = kvp.Key;
				var nodeInfo = kvp.Value;
				try
				{
					var statsResponse = await _client.Nodes.StatsAsync(new NodesStatsRequest(nodeId), cancellationToken).ConfigureAwait(false);
					var nodeHealth = CreateNodeHealthInfo(nodeId, nodeInfo, statsResponse);
					_ = _nodeHealthCache.AddOrUpdate(nodeId, static (_, nh) => nh, static (_, _, nh) => nh, nodeHealth);
					return nodeHealth;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to get health for node {NodeId}", nodeId);
					var errorHealth = new NodeHealthInfo
					{
						NodeId = nodeId, NodeName = nodeInfo.Name, IsHealthy = false,
						ErrorMessage = ex.Message, LastUpdated = DateTimeOffset.UtcNow,
					};
					_ = _nodeHealthCache.AddOrUpdate(nodeId, static (_, nh) => nh, static (_, _, nh) => nh, errorHealth);
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

	private async Task GetClusterStatsAsync(OpenSearchClusterHealth clusterHealth, CancellationToken cancellationToken)
	{
		try
		{
			var statsResponse = await _client.Cluster.StatsAsync(new ClusterStatsRequest(), cancellationToken).ConfigureAwait(false);
			if (statsResponse.IsValid)
			{
				clusterHealth.TotalDocuments = statsResponse.Indices?.Documents?.Count;
				clusterHealth.TotalSizeInBytes = statsResponse.Indices?.Store?.SizeInBytes != null
					? (long)statsResponse.Indices.Store.SizeInBytes
					: null;
				clusterHealth.NodeCount = statsResponse.Nodes?.Count?.Total;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get cluster statistics");
		}
	}
}
