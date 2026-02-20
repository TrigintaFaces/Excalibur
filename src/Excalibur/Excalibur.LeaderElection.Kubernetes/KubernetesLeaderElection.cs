// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Kubernetes-based implementation of leader election using the Lease API.
/// </summary>
public sealed partial class KubernetesLeaderElection : IHealthBasedLeaderElection, IDisposable, IAsyncDisposable
{
	private readonly IKubernetes _kubernetesClient;
	private readonly KubernetesLeaderElectionOptions _options;
	private readonly ILogger<KubernetesLeaderElection> _logger;
	private readonly string _resourceName;
	private readonly string _leaseName;
	private readonly string _namespace;
	private readonly Timer _renewalTimer;
	private readonly ResiliencePipeline _retryPolicy;
	private readonly SemaphoreSlim _leaseLock = new(1, 1);

	private readonly ConcurrentBag<Task> _trackedTasks = [];

	private CancellationTokenSource? _runningTokenSource;
	private V1Lease? _currentLease;
	private volatile bool _isRunning;
	private volatile bool _disposed;
	private volatile bool _isLeader;
	private volatile string? _currentLeaderId;

	/// <summary>
	/// Initializes a new instance of the <see cref="KubernetesLeaderElection" /> class.
	/// </summary>
	/// <param name="kubernetesClient"> The Kubernetes client. </param>
	/// <param name="resourceName"> The resource to elect a leader for. </param>
	/// <param name="options"> The Kubernetes leader election options. </param>
	/// <param name="logger"> The logger. </param>
	public KubernetesLeaderElection(
		IKubernetes kubernetesClient,
		string resourceName,
		IOptions<KubernetesLeaderElectionOptions>? options,
		ILogger<KubernetesLeaderElection>? logger)
	{
		_kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
		_resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger<KubernetesLeaderElection>.Instance;

		// Determine the lease name
		_leaseName = _options.LeaseName ?? $"{resourceName}-leader-election";

		// Determine namespace (from options, pod namespace, or default)
		_namespace = DetermineNamespace();

		// Set candidate ID (from options, pod name, or generated)
		CandidateId = DetermineCandidateId();

		// Create renewal timer (not started yet)
		_renewalTimer = new Timer(RenewLeadershipAsync, state: null, Timeout.Infinite, Timeout.Infinite);

		// Configure retry policy
		_retryPolicy = new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				MaxRetryAttempts = _options.MaxRetries,
				DelayGenerator = args =>
				{
					var delay = TimeSpan.FromMilliseconds(Math.Min(1000 * Math.Pow(2, args.AttemptNumber),
						_options.MaxRetryDelayMilliseconds));
					return ValueTask.FromResult<TimeSpan?>(delay);
				},
				ShouldHandle = new PredicateBuilder()
					.Handle<HttpOperationException>()
					.Handle<TaskCanceledException>(),
				OnRetry = args =>
				{
					LogRetryWarning(args.Outcome.Exception, args.AttemptNumber, args.RetryDelay.TotalMilliseconds, _leaseName);
					return ValueTask.CompletedTask;
				},
			})
			.Build();

		LogInitialized(_resourceName, _leaseName, _namespace);
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
	public bool IsLeader => _isLeader;

	/// <inheritdoc />
	public string? CurrentLeaderId => _currentLeaderId;

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_isRunning)
		{
			LogAlreadyRunning(_resourceName);
			return;
		}

		_isRunning = true;
		_runningTokenSource = new CancellationTokenSource();

		LogStarting(_resourceName, CandidateId);

		// Ensure the lease exists
		await EnsureLeaseExistsAsync(cancellationToken).ConfigureAwait(false);

		// Start the election loop and track for graceful disposal
		_trackedTasks.Add(Task.Run(() => RunElectionLoopAsync(_runningTokenSource.Token), cancellationToken));

		// Start renewal timer
		var renewInterval = TimeSpan.FromMilliseconds(_options.RenewIntervalMilliseconds);
		_ = _renewalTimer.Change(renewInterval, renewInterval);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!_isRunning)
		{
			return;
		}

		_isRunning = false;
		_ = _renewalTimer.Change(Timeout.Infinite, Timeout.Infinite);

		LogStopping(_resourceName);

		// Cancel the running token
		if (_runningTokenSource != null)
		{
			await _runningTokenSource.CancelAsync().ConfigureAwait(false);
		}

		// Release leadership if we hold it
		if (IsLeader)
		{
			await ReleaseLeadershipAsync(cancellationToken).ConfigureAwait(false);
		}

		_runningTokenSource?.Dispose();
		_runningTokenSource = null;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata)
	{
		if (!_isRunning)
		{
			return;
		}

		// Update health metadata in the lease annotation
		await _leaseLock.WaitAsync(_runningTokenSource.Token).ConfigureAwait(false);
		try
		{
			if (_currentLease != null && IsLeader)
			{
				var healthData = new
				{
					candidateId = CandidateId,
					isHealthy,
					healthScore = isHealthy ? 1.0 : 0.0,
					lastUpdated = DateTimeOffset.UtcNow,
					metadata,
				};

				// Add health data to lease annotations
				_currentLease.Metadata.Annotations ??= new Dictionary<string, string>(StringComparer.Ordinal);
				_currentLease.Metadata.Annotations[$"leader-election.excalibur.io/health-{CandidateId}"] =
					JsonSerializer.Serialize(healthData);

				// Update the lease
				await _retryPolicy.ExecuteAsync(
					async ct => _currentLease = await _kubernetesClient.CoordinationV1
						.ReplaceNamespacedLeaseAsync(_currentLease, _leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
					_runningTokenSource.Token).ConfigureAwait(false);

				// If unhealthy and configured to step down, release leadership
				if (!isHealthy && _options.StepDownWhenUnhealthy)
				{
					LogSteppingDownUnhealthy(_resourceName);
					await ReleaseLeadershipAsync(_runningTokenSource.Token).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			_ = _leaseLock.Release();
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	public async Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken)
	{
		var healthList = new List<CandidateHealth>();

		try
		{
			// Get the current lease
			var lease = await _retryPolicy.ExecuteAsync(
				async ct => await _kubernetesClient.CoordinationV1.ReadNamespacedLeaseAsync(_leaseName, _namespace,
					cancellationToken: ct).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			if (lease?.Metadata?.Annotations != null)
			{
				// Extract health data from annotations
				foreach (var annotation in lease.Metadata.Annotations)
				{
					if (annotation.Key.StartsWith("leader-election.excalibur.io/health-", StringComparison.Ordinal))
					{
						try
						{
							var healthData = JsonSerializer.Deserialize<Dictionary<string, object>>(annotation.Value);
							if (healthData != null)
							{
								healthList.Add(new CandidateHealth
								{
									CandidateId =
										healthData.TryGetValue("candidateId", out var id) ? id.ToString() ?? string.Empty : string.Empty,
									IsHealthy =
										healthData.TryGetValue("isHealthy", out var healthy) &&
										Convert.ToBoolean(healthy, CultureInfo.InvariantCulture),
									HealthScore = healthData.TryGetValue("healthScore", out var score)
										? Convert.ToDouble(score, CultureInfo.InvariantCulture)
										: 0.0,
									LastUpdated = healthData.TryGetValue("lastUpdated", out var updated)
										? DateTimeOffset.Parse(
											updated.ToString() ?? DateTimeOffset.UtcNow.ToString(CultureInfo.InvariantCulture),
											CultureInfo.InvariantCulture)
										: DateTimeOffset.UtcNow,
									IsLeader =
										string.Equals(
											lease.Spec?.HolderIdentity,
											healthData.TryGetValue("candidateId", out var cid) ? cid.ToString() : string.Empty,
											StringComparison.Ordinal),
									Metadata = healthData.TryGetValue("metadata", out var meta) &&
									           meta is Dictionary<string, string> metadata
										? metadata
										: [],
								});
							}
						}
						catch (Exception ex)
						{
							LogHealthAnnotationParseFailed(ex, annotation.Key);
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogGetHealthFailed(ex, _leaseName);
		}

		return healthList;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isRunning = false;
		_runningTokenSource?.Cancel();
		_renewalTimer.Dispose();
		_leaseLock.Dispose();
		_runningTokenSource?.Dispose();
	}

	/// <summary>
	/// Asynchronously disposes resources, ensuring tracked tasks complete and leadership is released.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isRunning = false;

		// 1. Disable timer and wait for in-flight callbacks
		await _renewalTimer.DisposeAsync().ConfigureAwait(false);

		// 2. Cancel running token to signal tracked tasks
		if (_runningTokenSource != null)
		{
			await _runningTokenSource.CancelAsync().ConfigureAwait(false);
		}

		// 3. Wait for all tracked tasks to complete
		try
		{
			await Task.WhenAll(_trackedTasks).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}

		// 4. Release leadership if we hold it
		if (IsLeader)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			try
			{
				await ReleaseLeadershipAsync(cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Timed out — lease will expire naturally
			}
		}

		_leaseLock.Dispose();
		_runningTokenSource?.Dispose();
	}

	private string DetermineNamespace()
	{
		// Priority: Options > Pod namespace > Default
		if (!string.IsNullOrEmpty(_options.Namespace))
		{
			return _options.Namespace;
		}

		// Try to read from pod namespace file (when running in-cluster)
		const string namespaceFile = "/var/run/secrets/kubernetes.io/serviceaccount/namespace";
		if (File.Exists(namespaceFile))
		{
			try
			{
				return File.ReadAllText(namespaceFile).Trim();
			}
			catch (Exception ex)
			{
				LogNamespaceReadFailed(ex, namespaceFile);
			}
		}

		return "default";
	}

	private string DetermineCandidateId()
	{
		// Priority: Options > Pod name (from env) > Machine name + GUID
		if (!string.IsNullOrEmpty(_options.CandidateId))
		{
			return _options.CandidateId;
		}

		// Try to get pod name from environment (set by Kubernetes)
		var podName = Environment.GetEnvironmentVariable("HOSTNAME") ??
		              Environment.GetEnvironmentVariable("POD_NAME");

		if (!string.IsNullOrEmpty(podName))
		{
			return podName;
		}

		// Fallback to machine name with GUID
		return $"{Environment.MachineName}-{Guid.NewGuid():N}";
	}

	private async Task EnsureLeaseExistsAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Try to get the lease
			_currentLease = await _retryPolicy.ExecuteAsync(
				async ct => await _kubernetesClient.CoordinationV1.ReadNamespacedLeaseAsync(
					_leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			LogLeaseExists(_leaseName, _namespace);
		}
		catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
		{
			// Create the lease if it doesn't exist
			LogCreatingLease(_leaseName, _namespace);

			var lease = new V1Lease
			{
				Metadata = new V1ObjectMeta
				{
					Name = _leaseName,
					NamespaceProperty = _namespace,
					Labels = new Dictionary<string, string>(StringComparer.Ordinal)
					{
						["app.kubernetes.io/managed-by"] = "excalibur-leader-election", ["excalibur.io/resource"] = _resourceName,
					},
					Annotations = new Dictionary<string, string>(StringComparer.Ordinal),
				},
				Spec = new V1LeaseSpec
				{
					HolderIdentity = null, LeaseDurationSeconds = _options.LeaseDurationSeconds, AcquireTime = null, RenewTime = null,
				},
			};

			_currentLease = await _retryPolicy.ExecuteAsync(
				async ct => await _kubernetesClient.CoordinationV1.CreateNamespacedLeaseAsync(
					lease, _namespace, cancellationToken: ct).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task RunElectionLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && _isRunning)
		{
			try
			{
				await TryAcquireOrRenewLeaseAsync(cancellationToken).ConfigureAwait(false);

				// Wait before next attempt
				await Task.Delay(TimeSpan.FromMilliseconds(_options.RetryIntervalMilliseconds), cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				LogElectionLoopError(ex, _leaseName);
				await Task.Delay(TimeSpan.FromMilliseconds(_options.RetryIntervalMilliseconds), cancellationToken).ConfigureAwait(false);
			}
		}
	}

	// Justification: Leader election state machine with K8s API calls, lease acquisition/renewal, and error recovery requires sequential orchestration for correctness
	// R0.8: Method is too long
#pragma warning disable MA0051
	private async Task TryAcquireOrRenewLeaseAsync(CancellationToken cancellationToken)
	{
		await _leaseLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Get the current lease
			var lease = await _retryPolicy.ExecuteAsync(
				async ct => await _kubernetesClient.CoordinationV1.ReadNamespacedLeaseAsync(
					_leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			_currentLease = lease;
			var now = DateTimeOffset.UtcNow;
			var previousLeaderId = CurrentLeaderId;

			// Check if we can acquire or renew the lease
			var canAcquire = false;

			if (string.IsNullOrEmpty(lease.Spec.HolderIdentity))
			{
				// No current holder, we can acquire
				canAcquire = true;
				LogAttemptingAcquire(_leaseName);
			}
			else if (string.Equals(lease.Spec.HolderIdentity, CandidateId, StringComparison.Ordinal))
			{
				// We are the current holder, renew
				canAcquire = true;
				LogRenewingLease(_leaseName);
			}
			else if (lease.Spec.RenewTime.HasValue)
			{
				// Check if the lease has expired
				var expiry = lease.Spec.RenewTime.Value.AddSeconds(lease.Spec.LeaseDurationSeconds ?? _options.LeaseDurationSeconds);
				if (now > expiry.AddSeconds(_options.GracePeriodSeconds))
				{
					canAcquire = true;
					LogLeaseExpired(_leaseName, lease.Spec.HolderIdentity, lease.Spec.RenewTime, expiry);
				}
			}

			if (canAcquire)
			{
				// Update the lease
				lease.Spec.HolderIdentity = CandidateId;
				lease.Spec.LeaseDurationSeconds = _options.LeaseDurationSeconds;
				lease.Spec.RenewTime = now.UtcDateTime;

				if (!lease.Spec.AcquireTime.HasValue || !string.Equals(previousLeaderId, CandidateId, StringComparison.Ordinal))
				{
					lease.Spec.AcquireTime = now.UtcDateTime;
				}

				// Add candidate metadata
				lease.Metadata.Annotations ??= new Dictionary<string, string>(StringComparer.Ordinal);
				foreach (var kvp in _options.CandidateMetadata)
				{
					lease.Metadata.Annotations[$"leader-election.excalibur.io/metadata-{kvp.Key}"] = kvp.Value;
				}

				// Try to update the lease
				try
				{
					_currentLease = await _retryPolicy.ExecuteAsync(
						async ct => await _kubernetesClient.CoordinationV1.ReplaceNamespacedLeaseAsync(
							lease, _leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
						cancellationToken).ConfigureAwait(false);

					// Update our state
					var wasLeader = IsLeader;
					_isLeader = true;
					_currentLeaderId = CandidateId;

					if (!wasLeader)
					{
						LogAcquiredLeadership(_resourceName);
						BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
					}

					if (!string.Equals(previousLeaderId, CandidateId, StringComparison.Ordinal))
					{
						LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeaderId, CandidateId, _resourceName));
					}
				}
				catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
				{
					// Another candidate won the race
					LogLostRace(_leaseName);
					await UpdateLeadershipStateFromLeaseAsync(lease).ConfigureAwait(false);
				}
			}
			else
			{
				// Update our state based on the current lease
				await UpdateLeadershipStateFromLeaseAsync(lease).ConfigureAwait(false);
			}
		}
		finally
		{
			_ = _leaseLock.Release();
		}
	}

	private Task UpdateLeadershipStateFromLeaseAsync(V1Lease lease)
	{
		var previousLeaderId = CurrentLeaderId;
		var wasLeader = IsLeader;

		_currentLeaderId = lease.Spec?.HolderIdentity;
		_isLeader = string.Equals(_currentLeaderId, CandidateId, StringComparison.Ordinal);

		if (wasLeader && !IsLeader)
		{
			LogLostLeadership(_resourceName);
			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
		}

		if (!string.Equals(previousLeaderId, CurrentLeaderId, StringComparison.Ordinal))
		{
			LogLeaderChanged(previousLeaderId, CurrentLeaderId, _resourceName);
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeaderId, CurrentLeaderId, _resourceName));
		}

		return Task.CompletedTask;
	}

	private void RenewLeadershipAsync(object? state)
	{
		if (!_isRunning || _disposed || !IsLeader)
		{
			return;
		}

		var task = Task.Run(async () =>
		{
			try
			{
				await TryAcquireOrRenewLeaseAsync(_runningTokenSource?.Token ?? CancellationToken.None).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Shutdown requested — expected during disposal
			}
			catch (Exception ex)
			{
				LogRenewalError(ex, _leaseName);
			}
		});
		_trackedTasks.Add(task);
	}

	private async Task ReleaseLeadershipAsync(CancellationToken cancellationToken)
	{
		await _leaseLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_currentLease?.Spec != null && string.Equals(_currentLease.Spec.HolderIdentity, CandidateId, StringComparison.Ordinal))
			{
				LogReleasingLeadership(_resourceName);

				// Clear the holder identity
				_currentLease.Spec.HolderIdentity = null;
				_currentLease.Spec.AcquireTime = null;
				_currentLease.Spec.RenewTime = null;

				try
				{
					await _retryPolicy.ExecuteAsync(
						async ct => await _kubernetesClient.CoordinationV1.ReplaceNamespacedLeaseAsync(
							_currentLease, _leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
						cancellationToken).ConfigureAwait(false);

					var previousLeaderId = CurrentLeaderId;
					_isLeader = false;
					_currentLeaderId = null;

					LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
					LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeaderId, newLeaderId: null, _resourceName));
				}
				catch (Exception ex)
				{
					LogReleaseError(ex, _leaseName);
				}
			}
		}
		finally
		{
			_ = _leaseLock.Release();
		}
	}

	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.KubernetesRetryWarning, LogLevel.Warning,
		"Retry {RetryCount} after {TimeSpan}ms for Kubernetes operation on lease '{LeaseName}'")]
	partial void LogRetryWarning(Exception exception, int retryCount, double timeSpan, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesInitialized, LogLevel.Information,
		"Initialized Kubernetes leader election for resource '{Resource}' with lease '{LeaseName}' in namespace '{Namespace}'")]
	partial void LogInitialized(string resource, string leaseName, string @namespace);

	[LoggerMessage(LeaderElectionEventId.KubernetesAlreadyRunning, LogLevel.Warning,
		"Leader election for resource '{Resource}' is already running")]
	partial void LogAlreadyRunning(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesStarting, LogLevel.Information,
		"Starting leader election for resource '{Resource}' with candidate ID '{CandidateId}'")]
	partial void LogStarting(string resource, string candidateId);

	[LoggerMessage(LeaderElectionEventId.KubernetesStopping, LogLevel.Information, "Stopping leader election for resource '{Resource}'")]
	partial void LogStopping(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesStoppedNotLeader, LogLevel.Warning,
		"Leader election for resource '{Resource}' stopped but candidate '{CandidateId}' was not the leader")]
	partial void LogStoppedNotLeader(string resource, string candidateId);

	[LoggerMessage(LeaderElectionEventId.KubernetesSteppingDownUnhealthy, LogLevel.Warning,
		"Leader is unhealthy, stepping down from leadership for resource '{Resource}'")]
	partial void LogSteppingDownUnhealthy(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesHealthAnnotationParseFailed, LogLevel.Warning,
		"Failed to parse health annotation: {Annotation}")]
	partial void LogHealthAnnotationParseFailed(Exception ex, string annotation);

	[LoggerMessage(LeaderElectionEventId.KubernetesGetHealthFailed, LogLevel.Error,
		"Failed to get candidate health for lease '{LeaseName}'")]
	partial void LogGetHealthFailed(Exception ex, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesNamespaceReadFailed, LogLevel.Warning, "Failed to read namespace from {File}")]
	partial void LogNamespaceReadFailed(Exception ex, string file);

	[LoggerMessage(LeaderElectionEventId.KubernetesLeaseExists, LogLevel.Debug,
		"Lease '{LeaseName}' already exists in namespace '{Namespace}'")]
	partial void LogLeaseExists(string leaseName, string @namespace);

	[LoggerMessage(LeaderElectionEventId.KubernetesCreatingLease, LogLevel.Information,
		"Creating lease '{LeaseName}' in namespace '{Namespace}'")]
	partial void LogCreatingLease(string leaseName, string @namespace);

	[LoggerMessage(LeaderElectionEventId.KubernetesElectionLoopError, LogLevel.Error, "Error in election loop for lease '{LeaseName}'")]
	partial void LogElectionLoopError(Exception ex, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesAttemptingAcquire, LogLevel.Information,
		"Lease '{LeaseName}' has no holder, attempting to acquire")]
	partial void LogAttemptingAcquire(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewingLease, LogLevel.Debug, "Renewing lease '{LeaseName}' as current holder")]
	partial void LogRenewingLease(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesLeaseExpired, LogLevel.Information,
		"Lease '{LeaseName}' held by '{Holder}' has expired (last renewed: {RenewTime}, expiry: {Expiry})")]
	partial void LogLeaseExpired(string leaseName, string? holder, DateTime? renewTime, DateTime? expiry);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewedLease, LogLevel.Information,
		"Successfully renewed lease '{LeaseName}' as leader of '{Resource}'")]
	partial void LogRenewedLease(string leaseName, string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesAcquiredLeadership, LogLevel.Information,
		"Acquired leadership for resource '{Resource}'")]
	partial void LogAcquiredLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesLostRace, LogLevel.Debug, "Lost race to acquire lease '{LeaseName}'")]
	partial void LogLostRace(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesLostLeadership, LogLevel.Warning, "Lost leadership for resource '{Resource}'")]
	partial void LogLostLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesLeaderChanged, LogLevel.Information,
		"Leader changed from '{Previous}' to '{Current}' for resource '{Resource}'")]
	partial void LogLeaderChanged(string? previous, string? current, string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewalFailed, LogLevel.Information,
		"Failed to renew lease '{LeaseName}', will retry in election loop")]
	partial void LogRenewalFailed(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewalError, LogLevel.Error, "Error renewing leadership for lease '{LeaseName}'")]
	partial void LogRenewalError(Exception ex, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesReleasingLeadership, LogLevel.Information,
		"Releasing leadership for resource '{Resource}'")]
	partial void LogReleasingLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesReleaseError, LogLevel.Error, "Failed to release leadership for lease '{LeaseName}'")]
	partial void LogReleaseError(Exception ex, string leaseName);
}
