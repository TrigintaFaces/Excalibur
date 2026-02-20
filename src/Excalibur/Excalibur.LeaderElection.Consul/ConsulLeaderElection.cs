// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Consul;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Consul-based implementation of leader election for distributed systems. Uses Consul's session and KV store for distributed leadership consensus.
/// </summary>
public sealed partial class ConsulLeaderElection : IHealthBasedLeaderElection, IDisposable, IAsyncDisposable
{
	private readonly string _resourceName;
	private readonly ConsulLeaderElectionOptions _options;
	private readonly IConsulClient _consulClient;
	private readonly ILogger<ConsulLeaderElection> _logger;
	private readonly Timer _renewalTimer;
	private readonly Timer _monitorTimer;
	private readonly ConcurrentDictionary<string, CandidateHealth> _candidateHealthCache = new(StringComparer.Ordinal);
	private readonly ResiliencePipeline _retryPolicy;

	private readonly ConcurrentBag<Task> _trackedTasks = [];
	private readonly CancellationTokenSource _shutdownTokenSource = new();

	private string? _sessionId;
	private volatile bool _isRunning;
	private volatile bool _disposed;
	private string? _lastKnownLeaderId;
	private volatile string? _cachedCurrentLeaderId;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsulLeaderElection" /> class.
	/// </summary>
	/// <param name="resourceName"> The resource to elect a leader for. </param>
	/// <param name="options"> The Consul leader election options. </param>
	/// <param name="consulClient"> The Consul client. </param>
	/// <param name="logger"> The logger. </param>
	public ConsulLeaderElection(
		string resourceName,
		IOptions<ConsulLeaderElectionOptions>? options,
		IConsulClient? consulClient,
		ILogger<ConsulLeaderElection>? logger)
	{
		_resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger<ConsulLeaderElection>.Instance;

		CandidateId = _options.InstanceId ?? $"{Environment.MachineName}_{Guid.NewGuid():N}";

		// Create or use provided Consul client
		_consulClient = consulClient ?? new ConsulClient(config =>
		{
			if (!string.IsNullOrEmpty(_options.ConsulAddress))
			{
				config.Address = new Uri(_options.ConsulAddress);
			}

			if (!string.IsNullOrEmpty(_options.Datacenter))
			{
				config.Datacenter = _options.Datacenter;
			}

			if (!string.IsNullOrEmpty(_options.Token))
			{
				config.Token = _options.Token;
			}
		});

		_retryPolicy = new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				MaxRetryAttempts = _options.MaxRetryAttempts,
				DelayGenerator = args =>
				{
					var delay = TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber));
					return ValueTask.FromResult<TimeSpan?>(delay);
				},
				ShouldHandle = new PredicateBuilder().Handle<Exception>(),
				OnRetry = args =>
				{
					LogRetryAttempt(args.Outcome.Exception, args.AttemptNumber, args.RetryDelay.TotalSeconds, _resourceName);
					return ValueTask.CompletedTask;
				},
			})
			.Build();

		_renewalTimer = new Timer(RenewSession, state: null, Timeout.Infinite, Timeout.Infinite);
		_monitorTimer = new Timer(MonitorLeadership, state: null, Timeout.Infinite, Timeout.Infinite);
	}

	/// <inheritdoc />
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <inheritdoc />
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <inheritdoc />
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <inheritdoc />
	public string CandidateId { get; }

	/// <inheritdoc />
	public bool IsLeader => string.Equals(CurrentLeaderId, CandidateId, StringComparison.Ordinal);

	/// <inheritdoc />
	public string? CurrentLeaderId => _cachedCurrentLeaderId;

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "UpdateHealthAsync uses JSON serialization for health metadata which is necessary for Consul health checks")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "UpdateHealthAsync uses JSON serialization for health metadata which is necessary for Consul health checks")]
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_isRunning)
		{
			LogAlreadyRunning(_resourceName);
			return;
		}

		_isRunning = true;
		LogStartingElection(_resourceName, CandidateId);

		// Create Consul session with health checks
		await CreateSessionAsync(cancellationToken).ConfigureAwait(false);

		// Update our health status
		await UpdateHealthAsync(isHealthy: true, metadata: null).ConfigureAwait(false);

		// Start trying to acquire leadership
		await TryAcquireLeadershipAsync(cancellationToken).ConfigureAwait(false);

		// Start renewal timer
		var renewInterval = _options.RenewInterval;
		_ = _renewalTimer.Change(renewInterval, renewInterval);

		// Start monitoring timer
		var monitorInterval = TimeSpan.FromSeconds(2);
		_ = _monitorTimer.Change(monitorInterval, monitorInterval);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "UpdateHealthAsync uses JSON serialization for health metadata which is necessary for Consul health checks")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "UpdateHealthAsync uses JSON serialization for health metadata which is necessary for Consul health checks")]
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!_isRunning)
		{
			return;
		}

		_isRunning = false;
		_ = _renewalTimer.Change(Timeout.Infinite, Timeout.Infinite);
		_ = _monitorTimer.Change(Timeout.Infinite, Timeout.Infinite);

		LogStoppingElection(_resourceName);

		// Release leadership if we hold it
		if (IsLeader)
		{
			await ReleaseLeadershipAsync().ConfigureAwait(false);
		}

		// Destroy session
		await DestroySessionAsync().ConfigureAwait(false);

		// Update our health status
		await UpdateHealthAsync(isHealthy: false, metadata: null).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[RequiresDynamicCode("JSON serialization of health metadata requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata)
	{
		var health = new CandidateHealth
		{
			CandidateId = CandidateId,
			IsHealthy = isHealthy,
			HealthScore = isHealthy ? 1.0 : 0.0,
			LastUpdated = DateTimeOffset.UtcNow,
			IsLeader = IsLeader,
			Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
		};

		_ = _candidateHealthCache.AddOrUpdate(CandidateId, static (_, h) => h, static (_, _, h) => h, health);

		// Update health in Consul
		var healthKeyPath = GetHealthKeyPath(CandidateId);
		var healthData = JsonSerializer.SerializeToUtf8Bytes(health, ConsulLeaderElectionJsonContext.Default.CandidateHealth);

		try
		{
			await _retryPolicy.ExecuteAsync(
				async ct => _ = await _consulClient.KV.Put(new KVPair(healthKeyPath) { Value = healthData }, ct).ConfigureAwait(false),
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogFailedToUpdateHealth(ex, _resourceName);
		}

		// If we're unhealthy and the leader, consider stepping down
		if (!isHealthy && IsLeader && _options.StepDownWhenUnhealthy)
		{
			LogSteppingDownUnhealthy(_resourceName);
			await ReleaseLeadershipAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	[RequiresDynamicCode("JSON serialization of candidate health requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	public async Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken)
	{
		var healthPrefix = GetHealthKeyPrefix();

		try
		{
			var result = await _consulClient.KV.List(healthPrefix, cancellationToken).ConfigureAwait(false);

			if (result.Response == null)
			{
				return _candidateHealthCache.Values.ToList();
			}

			var healthList = new List<CandidateHealth>();

			foreach (var entry in result.Response)
			{
				if (entry.Value != null)
				{
					try
					{
						var health = JsonSerializer.Deserialize(entry.Value, ConsulLeaderElectionJsonContext.Default.CandidateHealth);
						if (health != null)
						{
							healthList.Add(health);
							_ = _candidateHealthCache.AddOrUpdate(health.CandidateId, static (_, h) => h, static (_, _, h) => h, health);
						}
					}
					catch (JsonException ex)
					{
						LogFailedToDeserializeHealth(ex, entry.Key);
					}
				}
			}

			return healthList;
		}
		catch (Exception ex)
		{
			LogErrorGettingCandidateHealth(ex, _resourceName);
			return _candidateHealthCache.Values.ToList();
		}
	}

	/// <summary>
	/// Disposes the resources used by the Consul leader election service.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isRunning = false;
		_renewalTimer.Dispose();
		_monitorTimer.Dispose();
		_shutdownTokenSource.Dispose();
	}

	/// <summary>
	/// Asynchronously disposes the resources, ensuring proper cleanup of Consul session and leadership.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isRunning = false;

		// 1. Disable timers and wait for in-flight callbacks
		await _renewalTimer.DisposeAsync().ConfigureAwait(false);
		await _monitorTimer.DisposeAsync().ConfigureAwait(false);

		// 2. Cancel shutdown token to signal tracked tasks
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);

		// 3. Wait for all tracked tasks to complete
		try
		{
			await Task.WhenAll(_trackedTasks).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}

		// 4. Clean up session and leadership
		if (IsLeader)
		{
			await ReleaseLeadershipAsync().ConfigureAwait(false);
		}

		await DestroySessionAsync().ConfigureAwait(false);

		_shutdownTokenSource.Dispose();
	}

	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.ConsulRetryAttempt, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Retry {RetryCount} after {TimeSpan}s for resource '{Resource}'")]
	partial void LogRetryAttempt(Exception exception, int retryCount, double timeSpan, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulAlreadyRunning, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Leader election for resource '{Resource}' is already running")]
	partial void LogAlreadyRunning(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulStartingElection, Microsoft.Extensions.Logging.LogLevel.Information,
		"Starting Consul leader election for resource '{Resource}' with candidate ID '{CandidateId}'")]
	partial void LogStartingElection(string resource, string candidateId);

	[LoggerMessage(LeaderElectionEventId.ConsulStoppingElection, Microsoft.Extensions.Logging.LogLevel.Information,
		"Stopping Consul leader election for resource '{Resource}'")]
	partial void LogStoppingElection(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorGettingCurrentLeader, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error getting current leader for resource '{Resource}'")]
	partial void LogErrorGettingCurrentLeader(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToUpdateHealth, Microsoft.Extensions.Logging.LogLevel.Error,
		"Failed to update health status in Consul for resource '{Resource}'")]
	partial void LogFailedToUpdateHealth(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulSteppingDownUnhealthy, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Leader is unhealthy, stepping down from leadership for resource '{Resource}'")]
	partial void LogSteppingDownUnhealthy(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToDeserializeHealth, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Failed to deserialize health data from key '{Key}'")]
	partial void LogFailedToDeserializeHealth(Exception ex, string key);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorGettingCandidateHealth, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error getting candidate health from Consul for resource '{Resource}'")]
	partial void LogErrorGettingCandidateHealth(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulCreatedSession, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Created Consul session '{SessionId}' for resource '{Resource}'")]
	partial void LogCreatedSession(string sessionId, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToCreateSession, Microsoft.Extensions.Logging.LogLevel.Error,
		"Failed to create Consul session for resource '{Resource}'")]
	partial void LogFailedToCreateSession(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulDestroyedSession, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Destroyed Consul session '{SessionId}' for resource '{Resource}'")]
	partial void LogDestroyedSession(string sessionId, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToDestroySession, Microsoft.Extensions.Logging.LogLevel.Error,
		"Failed to destroy Consul session for resource '{Resource}'")]
	partial void LogFailedToDestroySession(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulCannotAcquireWithoutSession, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Cannot acquire leadership without a valid session for resource '{Resource}'")]
	partial void LogCannotAcquireWithoutSession(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulAcquiredLeadership, Microsoft.Extensions.Logging.LogLevel.Information,
		"Acquired leadership for resource '{Resource}'")]
	partial void LogAcquiredLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToAcquireLeadership, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Failed to acquire leadership for resource '{Resource}' - another leader exists")]
	partial void LogFailedToAcquireLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorAcquiringLeadership, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error trying to acquire leadership for resource '{Resource}'")]
	partial void LogErrorAcquiringLeadership(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulReleasedLeadership, Microsoft.Extensions.Logging.LogLevel.Information,
		"Released leadership for resource '{Resource}'")]
	partial void LogReleasedLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorReleasingLeadership, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error releasing leadership for resource '{Resource}'")]
	partial void LogErrorReleasingLeadership(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulSessionRenewalFailed, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Session renewal failed for resource '{Resource}', recreating session")]
	partial void LogSessionRenewalFailed(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulRenewedSession, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Renewed Consul session for resource '{Resource}'")]
	partial void LogRenewedSession(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorDuringRenewal, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error during session renewal for resource '{Resource}'")]
	partial void LogErrorDuringRenewal(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorDuringMonitoring, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error during leadership monitoring for resource '{Resource}'")]
	partial void LogErrorDuringMonitoring(Exception ex, string resource);

	/// <summary>
	/// Creates a new Consul session for leader election with configured health checks and TTL.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task CreateSessionAsync(CancellationToken cancellationToken)
	{
		try
		{
			var sessionRequest = new SessionEntry
			{
				Name = $"leader-election-{_resourceName}-{CandidateId}",
				Behavior = SessionBehavior.Delete,
				TTL = _options.SessionTTL,
				LockDelay = _options.LockDelay,
			};

			// Add health checks if configured
			if (_options.EnableHealthChecks && !string.IsNullOrEmpty(_options.HealthCheckId))
			{
				sessionRequest.Checks = [_options.HealthCheckId, "serfHealth"];
			}

			var result = await _retryPolicy.ExecuteAsync(
				async ct => await _consulClient.Session.Create(sessionRequest, ct).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			_sessionId = result.Response;
			LogCreatedSession(_sessionId, _resourceName);
		}
		catch (Exception ex)
		{
			LogFailedToCreateSession(ex, _resourceName);
			throw;
		}
	}

	/// <summary>
	/// Destroys the current Consul session and releases associated resources.
	/// </summary>
	private async Task DestroySessionAsync()
	{
		if (string.IsNullOrEmpty(_sessionId))
		{
			return;
		}

		try
		{
			_ = await _consulClient.Session.Destroy(_sessionId).ConfigureAwait(false);
			LogDestroyedSession(_sessionId, _resourceName);
		}
		catch (Exception ex)
		{
			LogFailedToDestroySession(ex, _resourceName);
		}
		finally
		{
			_sessionId = null;
		}
	}

	/// <summary>
	/// Attempts to acquire leadership by creating a lock in Consul's KV store using the current session.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task TryAcquireLeadershipAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(_sessionId))
		{
			LogCannotAcquireWithoutSession(_resourceName);
			return;
		}

		var keyPath = GetLeaderKeyPath();
		var leaderInfo = new LeaderInfo
		{
			CandidateId = CandidateId,
			SessionId = _sessionId,
			AcquiredAt = DateTimeOffset.UtcNow,
			Metadata = _options.CandidateMetadata,
		};

		var leaderData = JsonSerializer.SerializeToUtf8Bytes(leaderInfo, ConsulLeaderElectionJsonContext.Default.LeaderInfo);

		try
		{
			// Try to acquire the lock
			var acquired = await _retryPolicy.ExecuteAsync(
				async ct =>
				{
					var result = await _consulClient.KV.Acquire(
						new KVPair(keyPath) { Value = leaderData, Session = _sessionId },
						ct).ConfigureAwait(false);

					return result.Response;
				},
				cancellationToken).ConfigureAwait(false);

			if (acquired)
			{
				_cachedCurrentLeaderId = CandidateId;
				LogAcquiredLeadership(_resourceName);

				// Raise events
				BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));

				if (!string.Equals(_lastKnownLeaderId, CandidateId, StringComparison.Ordinal))
				{
					LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(_lastKnownLeaderId, CandidateId, _resourceName));
					_lastKnownLeaderId = CandidateId;
				}
			}
			else
			{
				LogFailedToAcquireLeadership(_resourceName);
			}
		}
		catch (Exception ex)
		{
			LogErrorAcquiringLeadership(ex, _resourceName);
		}
	}

	/// <summary>
	/// Releases the current leadership lock in Consul and raises the appropriate events.
	/// </summary>
	private async Task ReleaseLeadershipAsync()
	{
		if (string.IsNullOrEmpty(_sessionId))
		{
			return;
		}

		var keyPath = GetLeaderKeyPath();

		try
		{
			var released = await _consulClient.KV.Release(new KVPair(keyPath) { Session = _sessionId }).ConfigureAwait(false);

			if (released.Response)
			{
				_cachedCurrentLeaderId = null;
				LogReleasedLeadership(_resourceName);

				// Raise events
				LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
				LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(CandidateId, newLeaderId: null, _resourceName));
				_lastKnownLeaderId = null;
			}
		}
		catch (Exception ex)
		{
			LogErrorReleasingLeadership(ex, _resourceName);
		}
	}

	/// <summary>
	/// Timer callback that renews the Consul session to maintain leadership eligibility.
	/// </summary>
	/// <param name="state"> The timer callback state (unused). </param>
	private void RenewSession(object? state)
	{
		if (!_isRunning || _disposed || string.IsNullOrEmpty(_sessionId))
		{
			return;
		}

		var task = Task.Run(async () =>
		{
			try
			{
				var result = await _consulClient.Session.Renew(_sessionId).ConfigureAwait(false);

				if (result.Response == null)
				{
					LogSessionRenewalFailed(_resourceName);

					// Session expired, recreate it
					await CreateSessionAsync(_shutdownTokenSource.Token).ConfigureAwait(false);

					// Try to reacquire leadership
					if (!IsLeader)
					{
						await TryAcquireLeadershipAsync(_shutdownTokenSource.Token).ConfigureAwait(false);
					}
				}
				else
				{
					LogRenewedSession(_resourceName);
				}
			}
			catch (OperationCanceledException)
			{
				// Shutdown requested — expected during disposal
			}
			catch (Exception ex)
			{
				LogErrorDuringRenewal(ex, _resourceName);
			}
		});
		_trackedTasks.Add(task);
	}

	/// <summary>
	/// Timer callback that monitors leadership changes and raises appropriate events.
	/// </summary>
	/// <param name="state"> The timer callback state (unused). </param>
	private void MonitorLeadership(object? state)
	{
		if (!_isRunning || _disposed)
		{
			return;
		}

		var task = Task.Run(async () =>
		{
			try
			{
				var currentLeader = await FetchCurrentLeaderIdAsync(_shutdownTokenSource.Token).ConfigureAwait(false);
				_cachedCurrentLeaderId = currentLeader;

				// Check if leadership changed
				if (!string.Equals(_lastKnownLeaderId, currentLeader, StringComparison.Ordinal))
				{
					LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(_lastKnownLeaderId, currentLeader, _resourceName));

					if (string.Equals(currentLeader, CandidateId, StringComparison.Ordinal) &&
						!string.Equals(_lastKnownLeaderId, CandidateId, StringComparison.Ordinal))
					{
						BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
					}
					else if (string.Equals(_lastKnownLeaderId, CandidateId, StringComparison.Ordinal) &&
							 !string.Equals(currentLeader, CandidateId, StringComparison.Ordinal))
					{
						LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
					}

					_lastKnownLeaderId = currentLeader;
				}

				// If no leader and we're running, try to acquire
				if (string.IsNullOrEmpty(currentLeader) && !string.IsNullOrEmpty(_sessionId))
				{
					await TryAcquireLeadershipAsync(_shutdownTokenSource.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				// Shutdown requested — expected during disposal
			}
			catch (Exception ex)
			{
				LogErrorDuringMonitoring(ex, _resourceName);
			}
		});
		_trackedTasks.Add(task);
	}

	/// <summary>
	/// Asynchronously fetches the current leader ID from Consul's KV store.
	/// </summary>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> The current leader's candidate ID, or null if no leader exists. </returns>
	private async Task<string?> FetchCurrentLeaderIdAsync(CancellationToken cancellationToken)
	{
		try
		{
			var keyPath = GetLeaderKeyPath();
			var result = await _consulClient.KV.Get(keyPath, cancellationToken).ConfigureAwait(false);

			if (result.Response is { Value: not null })
			{
				var leaderInfo = JsonSerializer.Deserialize(result.Response.Value, ConsulLeaderElectionJsonContext.Default.LeaderInfo);
				return leaderInfo?.CandidateId;
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogErrorGettingCurrentLeader(ex, _resourceName);
		}

		return null;
	}

	/// <summary>
	/// Gets the Consul KV path for the leader lock key.
	/// </summary>
	/// <returns> The leader key path. </returns>
	private string GetLeaderKeyPath() => $"{_options.KeyPrefix}/leader/{_resourceName}";

	/// <summary>
	/// Gets the Consul KV prefix path for candidate health data.
	/// </summary>
	/// <returns> The health key prefix path. </returns>
	private string GetHealthKeyPrefix() => $"{_options.KeyPrefix}/health/{_resourceName}/";

	/// <summary>
	/// Gets the Consul KV path for a specific candidate's health data.
	/// </summary>
	/// <param name="candidateId"> The candidate identifier. </param>
	/// <returns> The health key path for the specified candidate. </returns>
	private string GetHealthKeyPath(string candidateId) => GetHealthKeyPrefix() + candidateId;

	/// <summary>
	/// Information about the current leader stored in Consul.
	/// </summary>
	internal sealed class LeaderInfo
	{
		/// <summary>
		/// Gets or sets the unique identifier of the candidate that holds leadership.
		/// </summary>
		/// <value> The candidate identifier. </value>
		public string CandidateId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the Consul session identifier associated with the leader lock.
		/// </summary>
		/// <value> The session identifier. </value>
		public string SessionId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the timestamp when leadership was acquired.
		/// </summary>
		/// <value> The leadership acquisition timestamp. </value>
		public DateTimeOffset AcquiredAt { get; set; }

		/// <summary>
		/// Gets or sets additional metadata associated with the leader.
		/// </summary>
		/// <value> The leader metadata dictionary. </value>
		public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
	}
}
