// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.LeaderElection;

using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.InMemory;

/// <summary>
/// In-memory implementation of leader election for single-process scenarios.
/// </summary>
public sealed partial class InMemoryLeaderElection : IHealthBasedLeaderElection, IDisposable, IAsyncDisposable
{
	private readonly TimeProvider _timeProvider;
	private readonly ConcurrentDictionary<string, string?> _leaders;
	private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CandidateHealth>> _candidates;
	private readonly string _resourceName;
	private readonly LeaderElectionOptions _options;
	private readonly ILogger<InMemoryLeaderElection> _logger;
	private readonly ITimer _leaseRenewalTimer;
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private volatile int _state; // 0 = stopped, 1 = running
	private volatile bool _disposed;
	// UTC ticks of the instant this candidate most recently acquired leadership, accessed via
	// Interlocked lock-free. No fencing-token provider exists for this single-process implementation, so
	// CurrentLeadership always carries a null fencing token (fencing is genuinely unavailable here — there
	// is no distributed store to mint a monotonic token against). null, never an in-band 0: a 0 would read
	// as a valid low token and could be presented to a fencing store, defeating split-brain.
	private long _leadershipAcquiredAtTicks;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryLeaderElection" /> class.
	/// </summary>
	/// <param name="resourceName"> The name of the resource to elect a leader for. </param>
	/// <param name="options"> The leader election options. </param>
	/// <param name="logger"> Optional logger for diagnostic output. </param>
	/// <param name="sharedState"> Optional shared state for coordinating multiple instances in the same process. </param>
	/// <param name="timeProvider">
	/// Optional time provider used for event timestamps. Defaults to <see cref="TimeProvider.System"/>.
	/// Inject a controllable provider to make emitted timestamps deterministic in tests.
	/// </param>
	public InMemoryLeaderElection(
		string resourceName,
		IOptions<LeaderElectionOptions> options,
		ILogger<InMemoryLeaderElection>? logger,
		InMemoryLeaderElectionSharedState? sharedState = null,
		TimeProvider? timeProvider = null)
	{
		_resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger<InMemoryLeaderElection>.Instance;
		_timeProvider = timeProvider ?? TimeProvider.System;
		var state = sharedState ?? InMemoryLeaderElectionSharedState.Default;
		_leaders = state.Leaders;
		_candidates = state.Candidates;

		CandidateId = _options.InstanceId;

		_leaseRenewalTimer = _timeProvider.CreateTimer(RenewLeaseCallback, state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		// Initialize candidate tracking
		_ = _candidates.TryAdd(_resourceName, new ConcurrentDictionary<string, CandidateHealth>(StringComparer.Ordinal));
	}

	/// <inheritdoc />
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <inheritdoc />
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <inheritdoc />
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <inheritdoc />
	public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

	/// <inheritdoc />
	public string CandidateId { get; }

	/// <inheritdoc />
	public bool IsLeader => _leaders.TryGetValue(_resourceName, out var leaderId) && string.Equals(leaderId, CandidateId, StringComparison.Ordinal);

	/// <inheritdoc />
	public Leadership? CurrentLeadership
	{
		get
		{
			if (!IsLeader)
			{
				return null;
			}

			var acquiredAtTicks = Interlocked.Read(ref _leadershipAcquiredAtTicks);
			return new Leadership(FencingToken: null, new DateTimeOffset(acquiredAtTicks, TimeSpan.Zero));
		}
	}

	/// <inheritdoc />
	public string? CurrentLeaderId => _leaders.GetValueOrDefault(_resourceName);

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
		{
			return;
		}

		// Register this candidate
		var candidateDict = _candidates.GetOrAdd(_resourceName, _ => new ConcurrentDictionary<string, CandidateHealth>(StringComparer.Ordinal));
		_ = candidateDict.AddOrUpdate(
			CandidateId,
			static (key, metadata) => new CandidateHealth
			{
				CandidateId = key,
				IsHealthy = true,
				HealthScore = 1.0,
				LastUpdated = DateTimeOffset.UtcNow,
				Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
			},
			static (key, existing, metadata) => new CandidateHealth
			{
				CandidateId = key,
				IsHealthy = existing.IsHealthy,
				HealthScore = existing.HealthScore,
				LastUpdated = DateTimeOffset.UtcNow,
				Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
			},
			_options.CandidateMetadata);

		// Try to acquire leadership
		await TryAcquireLeadershipAsync().ConfigureAwait(false);

		// Start lease renewal timer
		_ = _leaseRenewalTimer.Change(_options.RenewInterval, _options.RenewInterval);

		LogStarted(_resourceName, CandidateId);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref _state, 0, 1) != 1)
		{
			return;
		}

		// Stop lease renewal
		_ = _leaseRenewalTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		// Release leadership if we still hold it. Reading IsLeader and then removing by key alone is two
		// steps: another candidate can acquire in between, and the removal would then delete *its*
		// leadership record. ReleaseLeadershipIfHeld collapses both into one compare-and-remove.
		var wasLeader = ReleaseLeadershipIfHeld();
		if (wasLeader)
		{
			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(CandidateId, newLeaderId: null, _resourceName));
		}

		// Remove from candidates
		if (_candidates.TryGetValue(_resourceName, out var candidateDict))
		{
			_ = candidateDict.TryRemove(CandidateId, out _);
		}

		LogStopped(_resourceName, wasLeader);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
	{
		if (_state == 0)
		{
			return Task.CompletedTask;
		}

		if (_candidates.TryGetValue(_resourceName, out var candidateDict))
		{
			var combinedMetadata = new Dictionary<string, string>(_options.CandidateMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
			if (metadata != null)
			{
				foreach (var kvp in metadata)
				{
					combinedMetadata[kvp.Key] = kvp.Value;
				}
			}

			_ = candidateDict.AddOrUpdate(
				CandidateId,
				static (key, state) => new CandidateHealth
				{
					CandidateId = key,
					IsHealthy = state.isHealthy,
					HealthScore = state.isHealthy ? 1.0 : 0.0,
					LastUpdated = DateTimeOffset.UtcNow,
					Metadata = state.metadata,
				},
				static (key, _, state) => new CandidateHealth
				{
					CandidateId = key,
					IsHealthy = state.isHealthy,
					HealthScore = state.isHealthy ? 1.0 : 0.0,
					LastUpdated = DateTimeOffset.UtcNow,
					Metadata = state.metadata,
				},
				(isHealthy, metadata: combinedMetadata));

			LogHealthUpdated(CandidateId, isHealthy);

			// If we're unhealthy and configured to step down, release leadership.
			// The step-down is a single compare-and-remove, so it can only ever remove OUR OWN record.
			// A lock here would be false comfort: three of the four release sites (start, stop, dispose)
			// never took it, so it never excluded the writer that actually mattered.
			//
			// ORDERING INVARIANT -- the health record is written above, BEFORE this release, and the
			// renewal callback reads the leader slot BEFORE the candidate map. That is what stops a
			// step-down being undone by the very next renewal tick: a callback that observes the slot
			// empty must have observed the write that preceded the release, so it sees this candidate
			// unhealthy and declines to reacquire. Swap either pair -- release before the health write,
			// or read the candidate map before the leader slot -- and the step-down becomes a step-down
			// followed immediately by a reacquisition.
			if (!isHealthy && _options.StepDownWhenUnhealthy && ReleaseLeadershipIfHeld())
			{
				LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
				LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(CandidateId, newLeaderId: null, _resourceName));

				LogSteppedDownUnhealthy();
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken)
	{
		if (_candidates.TryGetValue(_resourceName, out var candidateDict))
		{
			return candidateDict.Values.ToList();
		}

		return await Task.FromResult(Enumerable.Empty<CandidateHealth>()).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		await StopAsync(CancellationToken.None).ConfigureAwait(false);

		await _leaseRenewalTimer.DisposeAsync().ConfigureAwait(false);
		_cancellationTokenSource.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Release leadership synchronously (mirrors StopAsync behavior)
		Interlocked.Exchange(ref _state, 0);
		_ = _leaseRenewalTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		var wasLeader = ReleaseLeadershipIfHeld();
		if (wasLeader)
		{
			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(CandidateId, newLeaderId: null, _resourceName));
		}

		if (_candidates.TryGetValue(_resourceName, out var candidateDict))
		{
			_ = candidateDict.TryRemove(CandidateId, out _);
		}

		_leaseRenewalTimer.Dispose();
		_cancellationTokenSource.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Relinquishes leadership of this resource if — and only if — this candidate still holds it.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if this candidate held leadership and it was released by this call;
	/// otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// The check and the removal are one atomic compare-and-remove, so a candidate can never delete a
	/// leadership record belonging to a successor. Splitting them into a read followed by a remove-by-key
	/// would let a candidate that observed itself leader, and then lost the resource to a successor,
	/// delete the successor's record — leaving that successor believing it still leads while the
	/// resource is free for a third candidate to acquire. This provider has no lease expiry, so nothing
	/// downstream would ever correct that.
	/// </remarks>
	private bool ReleaseLeadershipIfHeld() =>
		_leaders.TryRemove(new KeyValuePair<string, string?>(_resourceName, CandidateId));

	/// <summary>
	/// Attempts to take the resource for this candidate, and gives it straight back if this candidate
	/// is no longer running by the time it has it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The invariant: <b>a candidate holds the resource only while it is running.</b> Both callers can
	/// be racing a shutdown -- the renewal callback because a callback already dispatched to the thread
	/// pool keeps running after <c>Timer.Change(Timeout.Infinite, ...)</c>, and <c>StartAsync</c>
	/// because a host may start and stop concurrently -- so an entry check cannot establish it. The
	/// state can go stopped between the check and the <c>TryAdd</c>, and the acquisition would then
	/// place a stopped candidate on the resource: <c>StopAsync</c> has already run its release and found
	/// nothing to release, and this provider has no lease expiry, so no later event corrects it. The
	/// candidate is deregistered and every other candidate fails forever.
	/// </para>
	/// <para>
	/// The re-check therefore comes <b>after</b> the acquisition, and the interleaving is closed by the
	/// order on both sides. Shutdown writes <c>_state = 0</c> and then releases; acquisition adds and
	/// then reads <c>_state</c>. Either the add lands before the shutdown's release -- which is a
	/// compare-and-remove, so it takes this candidate's own record away -- or it lands after, in which
	/// case the shutdown's <c>_state</c> write preceded its release and so precedes the add, and the
	/// read here sees the stopped state and hands the resource back. <b>Swapping either pair reopens
	/// the window.</b> The <c>Interlocked</c> exchange on <c>_state</c> and the dictionary mutation are
	/// both full fences, so neither pair can be reordered.
	/// </para>
	/// </remarks>
	private Task TryAcquireLeadershipAsync()
	{
		var wasLeader = IsLeader;
		var currentLeader = CurrentLeaderId;

		// Simple first-come-first-served election for in-memory implementation
		var acquired = _leaders.TryAdd(_resourceName, CandidateId);

		if (acquired && (_state == 0 || _disposed))
		{
			// Won the resource after this candidate stopped. Give it back with the same
			// compare-and-remove every other release path uses, so a successor that has already taken
			// it keeps it, and say nothing: a tenure that never legitimately began has no loss to
			// announce.
			_ = ReleaseLeadershipIfHeld();
			return Task.CompletedTask;
		}

		if (acquired && !wasLeader)
		{
			Interlocked.Exchange(ref _leadershipAcquiredAtTicks, _timeProvider.GetUtcNow().UtcTicks);
			LogAcquiredLeadership(_resourceName);
			BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(currentLeader, CandidateId, _resourceName));
		}
		else if (!acquired)
		{
			// Another candidate already holds leadership for this resource — lost the race.
			RaiseAcquisitionFailed("lost the acquisition race", exception: null);
		}

		return Task.CompletedTask;
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

	private void RenewLeaseCallback(object? state)
	{
		if (_state == 0 || _disposed)
		{
			return;
		}

		try
		{
			// In a real implementation, this would renew a lease in external storage. For in-memory, we just verify we're still the leader
			if (IsLeader)
			{
				LogRenewedLease(_resourceName);
			}
			else if (!(_options.StepDownWhenUnhealthy && IsCurrentCandidateUnhealthy()))
			{
				// Try to acquire leadership if no one has it,
				// but not if we stepped down due to being unhealthy
				_ = TryAcquireLeadershipAsync();
			}
		}
		catch (Exception ex)
		{
			LogRenewalError(ex, _resourceName);
		}
	}

	private bool IsCurrentCandidateUnhealthy()
	{
		return _candidates.TryGetValue(_resourceName, out var candidateDict) &&
			candidateDict.TryGetValue(CandidateId, out var health) &&
			!health.IsHealthy;
	}

	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.InMemoryStarted, LogLevel.Information, "Started leader election for resource '{ResourceName}' with candidate ID '{CandidateId}'")]
	partial void LogStarted(string resourceName, string candidateId);

	[LoggerMessage(LeaderElectionEventId.InMemoryStopped, LogLevel.Information, "Stopped leader election for resource '{ResourceName}', was leader: {WasLeader}")]
	partial void LogStopped(string resourceName, bool wasLeader);

	[LoggerMessage(LeaderElectionEventId.InMemoryHealthUpdated, LogLevel.Debug, "Updated health status for candidate '{CandidateId}': {IsHealthy}")]
	partial void LogHealthUpdated(string candidateId, bool isHealthy);

	[LoggerMessage(LeaderElectionEventId.InMemorySteppedDownUnhealthy, LogLevel.Warning, "Stepped down from leadership due to unhealthy status")]
	partial void LogSteppedDownUnhealthy();

	[LoggerMessage(LeaderElectionEventId.InMemoryAcquiredLeadership, LogLevel.Information, "Acquired leadership for resource '{ResourceName}'")]
	partial void LogAcquiredLeadership(string resourceName);

	[LoggerMessage(LeaderElectionEventId.InMemoryRenewedLease, LogLevel.Trace, "Renewed leadership lease for resource '{ResourceName}'")]
	partial void LogRenewedLease(string resourceName);

	[LoggerMessage(LeaderElectionEventId.InMemoryRenewalError, LogLevel.Error, "Error during lease renewal for resource '{ResourceName}'")]
	partial void LogRenewalError(Exception ex, string resourceName);
}
