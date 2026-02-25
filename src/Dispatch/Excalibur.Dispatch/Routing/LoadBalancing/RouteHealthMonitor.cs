// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Net.Sockets;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Routing.LoadBalancing;

/// <summary>
/// Monitors the health of routes and maintains health statistics.
/// </summary>
public partial class RouteHealthMonitor : IRouteHealthMonitor, IHostedService, IDisposable
{
	private readonly ILogger<RouteHealthMonitor> _logger;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly RouteHealthMonitorOptions _options;
	private readonly ConcurrentDictionary<string, RouteHealthInfo> _routeHealth;
	private readonly ConcurrentDictionary<string, RouteDefinition> _registeredRoutes;
	private readonly SemaphoreSlim _healthCheckSemaphore;
	private Timer? _healthCheckTimer;

	/// <summary>
	/// Initializes a new instance of the <see cref="RouteHealthMonitor" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="httpClientFactory"> The HTTP client factory. </param>
	/// <param name="options"> The monitor options. </param>
	public RouteHealthMonitor(
		ILogger<RouteHealthMonitor> logger,
		IHttpClientFactory httpClientFactory,
		IOptions<RouteHealthMonitorOptions>? options)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_routeHealth = new ConcurrentDictionary<string, RouteHealthInfo>(StringComparer.Ordinal);
		_registeredRoutes = new ConcurrentDictionary<string, RouteDefinition>(StringComparer.Ordinal);
		_healthCheckSemaphore = new SemaphoreSlim(_options.MaxConcurrentHealthChecks);
	}

	/// <inheritdoc />
	public async Task<RouteHealthStatus> CheckHealthAsync(
		RouteDefinition route,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(route);

		await _healthCheckSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var stopwatch = ValueStopwatch.StartNew();
			var isHealthy = false;
			var errorMessage = string.Empty;

			try
			{
				// Perform health check based on endpoint type
				if (IsHttpEndpoint(route.Endpoint))
				{
					isHealthy = await CheckHttpHealthAsync(route, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					// For non-HTTP endpoints, use custom health check if available
					isHealthy = await CheckCustomHealthAsync(route, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				LogHealthCheckFailed(ex, route.RouteId);
				errorMessage = ex.Message;
			}

			// Update health info
			var healthInfo = _routeHealth.GetOrAdd(route.RouteId, static _ => new RouteHealthInfo());
			healthInfo.UpdateHealth(isHealthy, stopwatch.Elapsed);

			var status = new RouteHealthStatus
			{
				RouteId = route.RouteId,
				IsHealthy = isHealthy,
				LastCheck = DateTimeOffset.UtcNow,
				ConsecutiveFailures = healthInfo.ConsecutiveFailures,
				AverageLatency = TimeSpan.FromMilliseconds(healthInfo.AverageLatency),
				SuccessRate = healthInfo.SuccessRate,
				Metadata = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{
					["total_checks"] = healthInfo.TotalChecks,
					["successful_checks"] = healthInfo.SuccessfulChecks,
				},
			};

			if (!string.IsNullOrEmpty(errorMessage))
			{
				status.Metadata["last_error"] = errorMessage;
			}

			return status;
		}
		finally
		{
			_ = _healthCheckSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, RouteHealthStatus> GetHealthStatuses()
	{
		var statuses = new Dictionary<string, RouteHealthStatus>(StringComparer.Ordinal);

		foreach (var (routeId, healthInfo) in _routeHealth)
		{
			statuses[routeId] = new RouteHealthStatus
			{
				RouteId = routeId,
				IsHealthy = healthInfo.IsHealthy,
				LastCheck = healthInfo.LastCheck,
				ConsecutiveFailures = healthInfo.ConsecutiveFailures,
				AverageLatency = TimeSpan.FromMilliseconds(healthInfo.AverageLatency),
				SuccessRate = healthInfo.SuccessRate,
				Metadata = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{
					["total_checks"] = healthInfo.TotalChecks,
					["successful_checks"] = healthInfo.SuccessfulChecks,
				},
			};
		}

		return statuses;
	}

	/// <inheritdoc />
	public void RegisterRoute(RouteDefinition route)
	{
		ArgumentNullException.ThrowIfNull(route);

		_ = _registeredRoutes.TryAdd(route.RouteId, route);
		LogRouteRegistered(route.RouteId);
	}

	/// <inheritdoc />
	public void UnregisterRoute(string routeId)
	{
		ArgumentException.ThrowIfNullOrEmpty(routeId);

		if (_registeredRoutes.TryRemove(routeId, out _))
		{
			_ = _routeHealth.TryRemove(routeId, out _);
			LogRouteUnregistered(routeId);
		}
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken) => StartMonitoringAsync(cancellationToken);

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => StopMonitoringAsync(cancellationToken);

	/// <inheritdoc />
	public Task StartMonitoringAsync(CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Parameter required by interface but not used in synchronous timer setup

		LogHealthMonitorStarting();

		_healthCheckTimer = new Timer(
			PerformHealthChecks,
			state: null,
			_options.InitialDelay,
			_options.CheckInterval);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopMonitoringAsync(CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Parameter required by interface but not used in synchronous timer stop

		LogHealthMonitorStopping();

		_ = (_healthCheckTimer?.Change(Timeout.Infinite, 0));
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="RouteHealthMonitor" /> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"> true to release both managed and unmanaged resources; false to release only unmanaged resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_healthCheckTimer?.Dispose();
			_healthCheckSemaphore?.Dispose();
		}
	}

	private static bool IsHttpEndpoint(string endpoint) =>
		endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
		endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

	private static string GetHealthEndpoint(RouteDefinition route)
	{
		// Check if custom health endpoint is specified
		if (route.Metadata.TryGetValue("health_endpoint", out var healthEndpoint))
		{
			return healthEndpoint.ToString()!;
		}

		// Default to /health suffix
		var baseEndpoint = route.Endpoint.TrimEnd('/');
		return $"{baseEndpoint}/health";
	}

	private async Task<bool> CheckTcpHealthAsync(RouteDefinition route, CancellationToken cancellationToken)
	{
		var (host, port) = ParseTcpEndpoint(route.Endpoint, route.Metadata);
		if (string.IsNullOrEmpty(host))
		{
			LogInvalidTcpEndpoint(route.Endpoint);
			return false;
		}

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(_options.HttpTimeout);

		try
		{
			using var client = new TcpClient();
			await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
			return true;
		}
		catch (OperationCanceledException)
		{
			LogTcpHealthCheckTimeout(route.RouteId, host, port);
			return false;
		}
		catch (SocketException ex)
		{
			LogTcpHealthCheckFailed(ex, route.RouteId, host, port);
			return false;
		}
	}

	private static (string Host, int Port) ParseTcpEndpoint(string endpoint, IReadOnlyDictionary<string, object> metadata)
	{
		// Check metadata for explicit host/port override
		if (metadata.TryGetValue("tcp_host", out var hostObj) &&
			metadata.TryGetValue("tcp_port", out var portObj))
		{
			if (int.TryParse(portObj?.ToString(), out var metaPort))
			{
				return (hostObj?.ToString() ?? string.Empty, metaPort);
			}
		}

		// Parse from endpoint
		if (endpoint.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
		{
			var uri = new Uri(endpoint);
			return (uri.Host, uri.Port > 0 ? uri.Port : 80);
		}

		// Handle host:port format
		var colonIndex = endpoint.LastIndexOf(':');
		if (colonIndex > 0 && int.TryParse(endpoint.AsSpan(colonIndex + 1), out var parsedPort))
		{
			return (endpoint[..colonIndex], parsedPort);
		}

		return (endpoint, 80); // Default port
	}

	private Task<bool> CheckQueueHealthAsync(RouteDefinition route, CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Reserved for future connectivity checks

		if (!route.Metadata.TryGetValue("queue_type", out var queueTypeObj))
		{
			// No queue type specified, assume healthy
			return Task.FromResult(true);
		}

		var queueType = queueTypeObj?.ToString() ?? string.Empty;

		// Validate queue connection exists
		if (!route.Metadata.TryGetValue("queue_connection", out var connectionObj) ||
			string.IsNullOrWhiteSpace(connectionObj?.ToString()))
		{
			LogMissingQueueConnection(route.RouteId, queueType);
			return Task.FromResult(false);
		}

		// Validate known queue types (case-insensitive comparison)
		var knownQueueTypes = new[] { "rabbitmq", "kafka", "azureservicebus", "googlepubsub", "awssqs" };
		if (!knownQueueTypes.Contains(queueType, StringComparer.OrdinalIgnoreCase))
		{
			LogUnknownQueueType(queueType, route.RouteId);
			// Unknown queue type, but configuration exists - assume healthy
			return Task.FromResult(true);
		}

		// Configuration is valid - actual transport connectivity is left to transport-specific packages
		LogQueueConfigurationValid(route.RouteId, queueType);
		return Task.FromResult(true);
	}

	private async void PerformHealthChecks(object? state)
	{
		try
		{
			var tasks = _registeredRoutes.Values
				.Select(route => CheckHealthAsync(route, CancellationToken.None))
				.ToList();

			_ = await Task.WhenAll(tasks).ConfigureAwait(false);

			LogHealthChecksCompleted(tasks.Count);
		}
		catch (Exception ex)
		{
			LogScheduledHealthCheckError(ex);
		}
	}

	private async Task<bool> CheckHttpHealthAsync(RouteDefinition route, CancellationToken cancellationToken)
	{
		var healthEndpoint = GetHealthEndpoint(route);
		var httpClient = _httpClientFactory.CreateClient("HealthCheck");

		try
		{
			using var response = await httpClient.GetAsync(new Uri(healthEndpoint), cancellationToken).ConfigureAwait(false);
			return response.IsSuccessStatusCode;
		}
		catch (HttpRequestException)
		{
			return false;
		}
		catch (TaskCanceledException)
		{
			return false;
		}
	}

	private async Task<bool> CheckCustomHealthAsync(RouteDefinition route, CancellationToken cancellationToken)
	{
		// For non-HTTP endpoints, check if a custom health check is registered
		if (route.Metadata.TryGetValue("health_check_type", out var checkType))
		{
			switch (checkType.ToString())
			{
				case "tcp":
					return await CheckTcpHealthAsync(route, cancellationToken).ConfigureAwait(false);

				case "queue":
					return await CheckQueueHealthAsync(route, cancellationToken).ConfigureAwait(false);

				default:
					LogUnknownHealthCheckType(checkType?.ToString() ?? "unknown");
					return true; // Assume healthy if unknown
			}
		}

		// No custom health check, assume healthy
		return true;
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteHealthCheckFailed, LogLevel.Warning,
		"Health check failed for route {RouteId}")]
	private partial void LogHealthCheckFailed(Exception ex, string routeId);

	[LoggerMessage(MiddlewareEventId.RouteRegistered, LogLevel.Information,
		"Registered route {RouteId} for health monitoring")]
	private partial void LogRouteRegistered(string routeId);

	[LoggerMessage(MiddlewareEventId.RouteUnregistered, LogLevel.Information,
		"Unregistered route {RouteId} from health monitoring")]
	private partial void LogRouteUnregistered(string routeId);

	[LoggerMessage(MiddlewareEventId.HealthMonitorStarting, LogLevel.Information,
		"Starting route health monitor")]
	private partial void LogHealthMonitorStarting();

	[LoggerMessage(MiddlewareEventId.HealthMonitorStopping, LogLevel.Information,
		"Stopping route health monitor")]
	private partial void LogHealthMonitorStopping();

	[LoggerMessage(MiddlewareEventId.HealthChecksCompleted, LogLevel.Debug,
		"Completed health checks for {Count} routes")]
	private partial void LogHealthChecksCompleted(int count);

	[LoggerMessage(MiddlewareEventId.ScheduledHealthCheckError, LogLevel.Error,
		"Error performing scheduled health checks")]
	private partial void LogScheduledHealthCheckError(Exception ex);

	[LoggerMessage(MiddlewareEventId.UnknownHealthCheckType, LogLevel.Warning,
		"Unknown health check type: {Type}")]
	private partial void LogUnknownHealthCheckType(string type);

	// TCP health check logging
	[LoggerMessage(MiddlewareEventId.TcpHealthCheckTimeout, LogLevel.Warning,
		"TCP health check timed out for route {RouteId} ({Host}:{Port})")]
	private partial void LogTcpHealthCheckTimeout(string routeId, string host, int port);

	[LoggerMessage(MiddlewareEventId.TcpHealthCheckFailed, LogLevel.Warning,
		"TCP health check failed for route {RouteId} ({Host}:{Port})")]
	private partial void LogTcpHealthCheckFailed(Exception ex, string routeId, string host, int port);

	[LoggerMessage(MiddlewareEventId.InvalidTcpEndpoint, LogLevel.Warning,
		"Invalid TCP endpoint format: {Endpoint}")]
	private partial void LogInvalidTcpEndpoint(string endpoint);

	// Queue health check logging
	[LoggerMessage(MiddlewareEventId.MissingQueueConnection, LogLevel.Warning,
		"Queue health check missing connection configuration for route {RouteId} (type: {QueueType})")]
	private partial void LogMissingQueueConnection(string routeId, string queueType);

	[LoggerMessage(MiddlewareEventId.QueueConfigurationValid, LogLevel.Debug,
		"Queue configuration valid for route {RouteId} (type: {QueueType})")]
	private partial void LogQueueConfigurationValid(string routeId, string queueType);

	[LoggerMessage(MiddlewareEventId.UnknownQueueType, LogLevel.Information,
		"Unknown queue type '{QueueType}' for route {RouteId}, assuming healthy")]
	private partial void LogUnknownQueueType(string queueType, string routeId);

	private sealed class RouteHealthInfo
	{
		private const int MaxLatencyWindowSize = 100;
#if NET9_0_OR_GREATER

		private readonly Lock _lock = new();

#else

		private readonly object _lock = new();

#endif
		private readonly Queue<double> _latencyWindow = new();

		public bool IsHealthy { get; private set; } = true;

		public DateTimeOffset LastCheck { get; private set; } = DateTimeOffset.UtcNow;

		public int ConsecutiveFailures { get; private set; }

		public long TotalChecks { get; private set; }

		public long SuccessfulChecks { get; private set; }

		public double AverageLatency { get; private set; }

		public double SuccessRate => TotalChecks > 0 ? (double)SuccessfulChecks / TotalChecks : 0;

		public void UpdateHealth(bool isHealthy, TimeSpan latency)
		{
			lock (_lock)
			{
				TotalChecks++;
				LastCheck = DateTimeOffset.UtcNow;
				IsHealthy = isHealthy;

				if (isHealthy)
				{
					SuccessfulChecks++;
					ConsecutiveFailures = 0;
				}
				else
				{
					ConsecutiveFailures++;
				}

				// Update latency window
				_latencyWindow.Enqueue(latency.TotalMilliseconds);
				if (_latencyWindow.Count > MaxLatencyWindowSize)
				{
					_ = _latencyWindow.Dequeue();
				}

				AverageLatency = _latencyWindow.Average();
			}
		}
	}
}
