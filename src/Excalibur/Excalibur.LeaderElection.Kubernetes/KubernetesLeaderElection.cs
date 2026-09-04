// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
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
/// <remarks>
/// <para>
/// <b>Split-brain safety invariant (at-most-one-leader-EFFECT).</b> Every leader-gated side effect is
/// protected by EITHER (a) a monotonic fencing token checked at the action's authority, OR (b) a monotonic
/// self-relinquish bound at the incumbent (the leader ceases before any challenger can acquire; grace period
/// &gt; clock skew). This provider satisfies <b>(a)</b>: it is a <em>native-authority</em> election, so the
/// fencing token is the Lease's native <c>leaseTransitions</c> counter — monotonic, and int32-exhaustion
/// fails closed by relinquishing rather than wrapping into a non-monotonic token. A takeover increments the
/// counter, so a stale (partitioned) incumbent's fenced writes carry a strictly lower token and are rejected
/// fail-closed at the resource.
/// </para>
/// <para>
/// This is why the challenger's takeover comparison is <em>correctly</em> wall-clock
/// (<c>now &gt; RenewTime + LeaseDuration + GracePeriod</c>): <c>RenewTime</c> is the API server's
/// server-stamped, inherently cross-process timestamp, and the added grace period is the clock-skew cushion.
/// A monotonic self-relinquish is <em>not</em> required for correctness here because safety is carried by the
/// fencing token, not by the incumbent demoting itself — though adding one would make <c>IsLeader</c> honest
/// during a partition (a defense-in-depth liveness improvement, not a safety fix).
/// </para>
/// </remarks>
public sealed partial class KubernetesLeaderElection : IHealthBasedLeaderElection, IDisposable, IAsyncDisposable
{
	private readonly TimeProvider _timeProvider;
	private readonly IKubernetes _kubernetesClient;
	private readonly KubernetesLeaderElectionOptions _options;
	private readonly ILogger<KubernetesLeaderElection> _logger;
	private readonly string _resourceName;
	private readonly string _leaseName;
	private readonly string _namespace;
	private readonly Timer _renewalTimer;
	private readonly ResiliencePipeline _retryPolicy;
	private readonly SemaphoreSlim _leaseLock = new(1, 1);

	private ConcurrentBag<Task> _trackedTasks = [];

	private CancellationTokenSource? _runningTokenSource;
	private V1Lease? _currentLease;
	private volatile bool _isRunning;
	private volatile bool _disposed;
	private volatile bool _isLeader;
	private volatile string? _currentLeaderId;
	// the native Lease.spec.leaseTransitions counter observed at the most recent leadership
	// transition (the fencing token per SA ruling), and UTC ticks of the instant that tenure began.
	// Accessed lock-free via Interlocked, mirroring the existing volatile-field pattern on this type.
	private long _currentFencingToken;
	private long _leadershipAcquiredAtTicks;

	/// <summary>
	/// Initializes a new instance of the <see cref="KubernetesLeaderElection" /> class.
	/// </summary>
	/// <param name="kubernetesClient"> The Kubernetes client. </param>
	/// <param name="resourceName"> The resource to elect a leader for. </param>
	/// <param name="options"> The Kubernetes leader election options. </param>
	/// <param name="logger"> The logger. </param>
	/// <param name="timeProvider">
	/// Optional time provider used for event timestamps. Defaults to <see cref="TimeProvider.System"/>.
	/// Inject a controllable provider to make emitted timestamps deterministic in tests.
	/// </param>
	public KubernetesLeaderElection(
		IKubernetes kubernetesClient,
		string resourceName,
		IOptions<KubernetesLeaderElectionOptions>? options,
		ILogger<KubernetesLeaderElection>? logger,
		TimeProvider? timeProvider = null)
	{
		_kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
		_resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger<KubernetesLeaderElection>.Instance;
		_timeProvider = timeProvider ?? TimeProvider.System;

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
						_options.MaxRetryDelay.TotalMilliseconds));
					return ValueTask.FromResult<TimeSpan?>(delay);
				},
				ShouldHandle = new PredicateBuilder()
					.Handle<HttpOperationException>()
					.Handle<TaskCanceledException>(),
				OnRetry = args =>
				{
					LogRetryWarning(args.Outcome.Exception!, args.AttemptNumber, args.RetryDelay.TotalMilliseconds, _leaseName);
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
	public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

	/// <inheritdoc/>
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <inheritdoc />
	public string CandidateId { get; }

	/// <inheritdoc />
	public bool IsLeader => _isLeader;

	/// <inheritdoc />
	public Leadership? CurrentLeadership
	{
		get
		{
			if (!_isLeader)
			{
				return null;
			}

			var token = Interlocked.Read(ref _currentFencingToken);
			var acquiredAtTicks = Interlocked.Read(ref _leadershipAcquiredAtTicks);
			return new Leadership(token == 0 ? null : token, new DateTimeOffset(acquiredAtTicks, TimeSpan.Zero));
		}
	}

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
		cancellationToken.ThrowIfCancellationRequested();
		_trackedTasks.Add(RunElectionLoopAsync(_runningTokenSource.Token));

		// Start renewal timer
		var renewInterval = _options.RenewInterval;
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
	public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
	{
		if (!_isRunning)
		{
			return;
		}

		// Update health metadata in the lease annotation
		await _leaseLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_currentLease != null && IsLeader)
			{
				var healthData = new KubernetesHealthAnnotation
				{
					CandidateId = CandidateId,
					IsHealthy = isHealthy,
					HealthScore = isHealthy ? 1.0 : 0.0,
					LastUpdated = DateTimeOffset.UtcNow,
					Metadata = metadata,
				};

				// Add health data to lease annotations
				_currentLease.Metadata.Annotations ??= new Dictionary<string, string>(StringComparer.Ordinal);
				_currentLease.Metadata.Annotations[$"leader-election.excalibur.io/health-{CandidateId}"] =
					JsonSerializer.Serialize(healthData, KubernetesLeaderElectionJsonContext.Default.KubernetesHealthAnnotation);

				// Update the lease
				await _retryPolicy.ExecuteAsync(
					async ct => _currentLease = await _kubernetesClient.CoordinationV1
						.ReplaceNamespacedLeaseAsync(_currentLease, _leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
					cancellationToken).ConfigureAwait(false);

				// If unhealthy and configured to step down, release leadership
				if (!isHealthy && _options.StepDownWhenUnhealthy)
				{
					LogSteppingDownUnhealthy(_resourceName);
					await ReleaseLeadershipAsync(_runningTokenSource!.Token).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			_ = _leaseLock.Release();
		}
	}

	/// <inheritdoc />
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
							var healthData = JsonSerializer.Deserialize(
								annotation.Value,
								KubernetesLeaderElectionJsonContext.Default.KubernetesHealthAnnotation);
							if (healthData != null)
							{
								healthList.Add(new CandidateHealth
								{
									CandidateId = healthData.CandidateId,
									IsHealthy = healthData.IsHealthy,
									HealthScore = healthData.HealthScore,
									LastUpdated = healthData.LastUpdated,
									IsLeader = string.Equals(
										lease.Spec?.HolderIdentity,
										healthData.CandidateId,
										StringComparison.Ordinal),
									Metadata = healthData.Metadata is not null
										? new Dictionary<string, string>(healthData.Metadata, StringComparer.Ordinal)
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
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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
						["app.kubernetes.io/managed-by"] = "excalibur-leader-election",
						["excalibur.io/resource"] = _resourceName,
					},
					Annotations = new Dictionary<string, string>(StringComparer.Ordinal),
				},
				Spec = new V1LeaseSpec
				{
					HolderIdentity = null,
					LeaseDurationSeconds = (int)_options.LeaseDuration.TotalSeconds,
					AcquireTime = null,
					RenewTime = null,
				},
			};

			try
			{
				_currentLease = await _retryPolicy.ExecuteAsync(
					async ct => await _kubernetesClient.CoordinationV1.CreateNamespacedLeaseAsync(
						lease, _namespace, cancellationToken: ct).ConfigureAwait(false),
					cancellationToken).ConfigureAwait(false);
			}
			catch (HttpOperationException createEx) when (createEx.Response.StatusCode == HttpStatusCode.Conflict)
			{
				// Another candidate's StartAsync raced this one between the read-404 above and this
				// create: both saw no lease, both tried to create it, and Kubernetes admits exactly one
				// (409 AlreadyExists for the rest). That candidate's create is authoritative -- this is
				// not a failure, it is the lease now existing, which is what this method promises on
				// return. Falling through to a read (rather than treating the 409 as terminal) is what
				// lets every losing candidate proceed to the normal acquire path instead of StartAsync
				// throwing outright for N-1 of N concurrent starters.
				LogLeaseExists(_leaseName, _namespace);

				_currentLease = await _retryPolicy.ExecuteAsync(
					async ct => await _kubernetesClient.CoordinationV1.ReadNamespacedLeaseAsync(
						_leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
					cancellationToken).ConfigureAwait(false);
			}
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
				await Task.Delay(_options.RetryInterval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogElectionLoopError(ex, _leaseName);

				// An error while not holding leadership is a failed acquisition attempt.
				if (!IsLeader)
				{
					RaiseAcquisitionFailed("error during acquisition", ex);
				}

				await Task.Delay(_options.RetryInterval, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Raises <see cref="AcquisitionFailed"/>, guarding the invocation so a throwing subscriber
	/// can never break the acquisition/renewal loop.
	/// </summary>
	private void RaiseAcquisitionFailed(string reason, Exception? exception)
	{
		try
		{
			AcquisitionFailed?.Invoke(this, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _resourceName, reason, _timeProvider.GetUtcNow(), exception));
		}
		catch (Exception)
		{
			// A throwing subscriber must never break the acquire loop.
		}
	}

	// Justification: Leader election state machine with K8s API calls, lease acquisition/renewal, and error recovery requires sequential orchestration for correctness
	// Method is too long
#pragma warning disable MA0051
	// internal so the split-brain regression can drive one acquisition attempt directly. Driving it
	// through StartAsync would mean waiting on a renewal timer, which puts a clock back into a test
	// whose subject is a race, not a duration.
	internal async Task TryAcquireOrRenewLeaseAsync(CancellationToken cancellationToken)
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
			var now = _timeProvider.GetUtcNow();
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
				var expiry = lease.Spec.RenewTime.Value.AddSeconds(lease.Spec.LeaseDurationSeconds ?? (int)_options.LeaseDuration.TotalSeconds);
				if (now > expiry.Add(_options.GracePeriod))
				{
					canAcquire = true;
					LogLeaseExpired(_leaseName, lease.Spec.HolderIdentity, lease.Spec.RenewTime, expiry);
				}
			}

			if (canAcquire)
			{
				// A leadership TRANSITION (takeover from a different/absent prior holder, not a self-renew)
				// advances the native Lease.spec.leaseTransitions counter — the monotonic fencing token
				// (SA ruling: K8s reads the native counter, never a self-minted one). If the int32 counter is
				// exhausted, relinquish (fail-closed) rather than wrap it into a non-monotonic token.
				var isTransition = !string.Equals(previousLeaderId, CandidateId, StringComparison.Ordinal);
				if (isTransition)
				{
					var currentTransitions = lease.Spec.LeaseTransitions ?? 0;
					if (currentTransitions >= int.MaxValue)
					{
						LogFencingTokenExhausted(CandidateId, _leaseName);
						RaiseAcquisitionFailed("fencing token (leaseTransitions) domain exhausted", exception: null);
						return;
					}

					lease.Spec.LeaseTransitions = currentTransitions + 1;
				}

				// Update the lease
				lease.Spec.HolderIdentity = CandidateId;
				lease.Spec.LeaseDurationSeconds = (int)_options.LeaseDuration.TotalSeconds;
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
					Interlocked.Exchange(ref _currentFencingToken, lease.Spec.LeaseTransitions ?? 0);

					if (!wasLeader)
					{
						Interlocked.Exchange(ref _leadershipAcquiredAtTicks, _timeProvider.GetUtcNow().UtcTicks);
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
					// Another candidate won the race.
					//
					// `lease` is OUR copy, and its HolderIdentity was overwritten with CandidateId above
					// before the write was attempted. Deriving leadership from it here reads our own
					// identity back and concludes we won -- so EVERY loser of the race declared itself
					// leader, and the !IsLeader guard below could never fire. That is a split-brain: the
					// conformance kit observed three leaders among four candidates against a real API
					// server. The authoritative holder is what the server has, so re-read it.
					LogLostRace(_leaseName);

					try
					{
						var authoritative = await _retryPolicy.ExecuteAsync(
							async ct => await _kubernetesClient.CoordinationV1.ReadNamespacedLeaseAsync(
								_leaseName, _namespace, cancellationToken: ct).ConfigureAwait(false),
							cancellationToken).ConfigureAwait(false);

						_currentLease = authoritative;
						await UpdateLeadershipStateFromLeaseAsync(authoritative).ConfigureAwait(false);
					}
					catch (Exception readEx) when (readEx is not OperationCanceledException)
					{
						// Fail CLOSED. We lost the race and could not learn who won, so we cannot be the
						// leader -- claiming it on an unconfirmed read is the same defect one layer down.
						if (IsLeader)
						{
							_isLeader = false;
							LogLostLeadership(_resourceName);
							LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
						}

						_currentLeaderId = null;
					}

					if (!IsLeader)
					{
						RaiseAcquisitionFailed("lost the acquisition race", exception: null);
					}
				}
			}
			else
			{
				// Another candidate holds a valid lease — we could not acquire this attempt.
				await UpdateLeadershipStateFromLeaseAsync(lease).ConfigureAwait(false);
				if (!IsLeader)
				{
					RaiseAcquisitionFailed("lost the acquisition race", exception: null);
				}
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

		var task = RenewLeadershipCoreAsync(_runningTokenSource?.Token ?? CancellationToken.None);
		_trackedTasks.Add(task);

		// Drain completed tasks to prevent unbounded growth
		var snapshot = Interlocked.Exchange(ref _trackedTasks, new ConcurrentBag<Task>());
		foreach (var t in snapshot)
		{
			if (!t.IsCompleted)
			{
				_trackedTasks.Add(t);
			}
		}
	}

	private async Task RenewLeadershipCoreAsync(CancellationToken cancellationToken)
	{
		try
		{
			await TryAcquireOrRenewLeaseAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown requested — expected during disposal
		}
		catch (Exception ex)
		{
			LogRenewalError(ex, _leaseName);
		}
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

	[LoggerMessage(LeaderElectionEventId.KubernetesFencingTokenExhausted, LogLevel.Critical,
		"Candidate {CandidateId} cannot take leadership of lease {LeaseName} — leaseTransitions (int32 fencing token) is exhausted; relinquishing (fail-closed) rather than wrapping to a non-monotonic token")]
	partial void LogFencingTokenExhausted(string candidateId, string leaseName);

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
