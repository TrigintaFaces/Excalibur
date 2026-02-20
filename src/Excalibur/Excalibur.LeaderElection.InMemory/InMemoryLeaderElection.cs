// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

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
	private readonly ConcurrentDictionary<string, string?> _leaders;
	private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CandidateHealth>> _candidates;
	private readonly string _resourceName;
	private readonly LeaderElectionOptions _options;
	private readonly ILogger<InMemoryLeaderElection> _logger;
	private readonly Timer _leaseRenewalTimer;
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private bool _isRunning;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryLeaderElection" /> class.
	/// </summary>
	/// <param name="resourceName"> The name of the resource to elect a leader for. </param>
	/// <param name="options"> The leader election options. </param>
	/// <param name="logger"> Optional logger for diagnostic output. </param>
	/// <param name="sharedState"> Optional shared state for coordinating multiple instances in the same process. </param>
	public InMemoryLeaderElection(
		string resourceName,
		IOptions<LeaderElectionOptions> options,
		ILogger<InMemoryLeaderElection>? logger,
		InMemoryLeaderElectionSharedState? sharedState = null)
	{
		_resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger<InMemoryLeaderElection>.Instance;
		var state = sharedState ?? InMemoryLeaderElectionSharedState.Default;
		_leaders = state.Leaders;
		_candidates = state.Candidates;

		CandidateId = _options.InstanceId;

		_leaseRenewalTimer = new Timer(RenewLeaseCallback, state: null, Timeout.Infinite, Timeout.Infinite);

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
	public string CandidateId { get; }

	/// <inheritdoc />
	public bool IsLeader => _leaders.TryGetValue(_resourceName, out var leaderId) && string.Equals(leaderId, CandidateId, StringComparison.Ordinal);

	/// <inheritdoc />
	public string? CurrentLeaderId => _leaders.GetValueOrDefault(_resourceName);

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_isRunning)
		{
			return;
		}

		_isRunning = true;

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
		if (!_isRunning)
		{
			return;
		}

		_isRunning = false;

		// Stop lease renewal
		_ = _leaseRenewalTimer.Change(Timeout.Infinite, Timeout.Infinite);

		// Release leadership if we have it
		var wasLeader = IsLeader;
		if (wasLeader)
		{
			_ = _leaders.TryRemove(_resourceName, out _);
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
	[RequiresDynamicCode("JSON serialization of health metadata requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	public Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata)
	{
		if (!_isRunning)
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

			// If we're unhealthy and configured to step down, release leadership
			if (!isHealthy && _options.StepDownWhenUnhealthy && IsLeader)
			{
				_ = _leaders.TryRemove(_resourceName, out _);
				LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
				LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(CandidateId, newLeaderId: null, _resourceName));

				LogSteppedDownUnhealthy();
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	[RequiresDynamicCode("JSON serialization of candidate health requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
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
		_isRunning = false;
		_ = _leaseRenewalTimer.Change(Timeout.Infinite, Timeout.Infinite);

		var wasLeader = IsLeader;
		if (wasLeader)
		{
			_ = _leaders.TryRemove(_resourceName, out _);
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

	private Task TryAcquireLeadershipAsync()
	{
		var wasLeader = IsLeader;
		var currentLeader = CurrentLeaderId;

		// Simple first-come-first-served election for in-memory implementation
		var acquired = _leaders.TryAdd(_resourceName, CandidateId);

		if (acquired && !wasLeader)
		{
			LogAcquiredLeadership(_resourceName);
			BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(currentLeader, CandidateId, _resourceName));
		}

		return Task.CompletedTask;
	}

	private void RenewLeaseCallback(object? state)
	{
		if (!_isRunning || _disposed)
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
				_ = Task.Run(TryAcquireLeadershipAsync, _cancellationTokenSource.Token);
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
