// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.LeaderElection.Redis;

/// <summary>
/// Redis implementation of <see cref="ILeaderElection"/> using SET NX with TTL.
/// </summary>
/// <remarks>
/// Uses Redis SET NX (set if not exists) with expiration for distributed leader election.
/// The lease is automatically renewed while the candidate holds leadership.
/// </remarks>
public sealed partial class RedisLeaderElection : ILeaderElection, IAsyncDisposable
{
	private readonly IConnectionMultiplexer _redis;
	private readonly string _lockKey;
	private readonly LeaderElectionOptions _options;
	private readonly ILogger<RedisLeaderElection> _logger;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else
	private readonly object _lock = new();

#endif

	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	private DateTimeOffset _lastSuccessfulRenewal;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisLeaderElection"/> class.
	/// </summary>
	/// <param name="redis">The Redis connection multiplexer.</param>
	/// <param name="lockKey">The Redis key for the leader lock (e.g., "myapp:leader").</param>
	/// <param name="options">The leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	public RedisLeaderElection(
		IConnectionMultiplexer redis,
		string lockKey,
		IOptions<LeaderElectionOptions> options,
		ILogger<RedisLeaderElection> logger)
	{
		_redis = redis ?? throw new ArgumentNullException(nameof(redis));
		_lockKey = lockKey ?? throw new ArgumentNullException(nameof(lockKey));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		CandidateId = _options.InstanceId ?? (Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8]);
	}

	/// <inheritdoc/>
	public string CandidateId { get; }

	/// <inheritdoc/>
	public bool IsLeader => _isLeader;

	/// <inheritdoc/>
	public string? CurrentLeaderId
	{
		get
		{
			lock (_lock)
			{
				return _currentLeaderId;
			}
		}
	}

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <inheritdoc/>
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			if (_isStarted)
			{
				return;
			}

			_isStarted = true;
		}

		LogStarting(CandidateId, _lockKey);

		await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);

		// Start renewal loop
		_renewalCts = new CancellationTokenSource();
		_renewalTask = RunRenewalLoopAsync(_renewalCts.Token);
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		bool wasLeader;

		lock (_lock)
		{
			if (!_isStarted)
			{
				return;
			}

			_isStarted = false;
			wasLeader = _isLeader;
		}

		LogStopping(CandidateId);

		// Stop renewal loop
		if (_renewalCts != null)
		{
			await _renewalCts.CancelAsync().ConfigureAwait(false);
			if (_renewalTask != null)
			{
				try
				{
					await _renewalTask.ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
			}

			_renewalCts.Dispose();
			_renewalCts = null;
			_renewalTask = null;
		}

		// Release lock if we hold it
		if (wasLeader)
		{
			await ReleaseLockAsync().ConfigureAwait(false);

			lock (_lock)
			{
				_isLeader = false;
				var previousLeader = _currentLeaderId;
				_currentLeaderId = null;

				LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockKey));
				LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, _lockKey));
			}
		}
	}

	/// <summary>
	/// Disposes the leader election resources asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await StopAsync(CancellationToken.None).ConfigureAwait(false);
	}

	private async Task TryAcquireLockAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var db = _redis.GetDatabase();

			// SET key value NX PX milliseconds
			var acquired = await db.StringSetAsync(
				_lockKey,
				CandidateId,
				_options.LeaseDuration,
				When.NotExists).ConfigureAwait(false);

			if (acquired)
			{
				BecomeLeader();
			}
			else
			{
				// Check who the current leader is
				var currentLeader = await db.StringGetAsync(_lockKey).ConfigureAwait(false);
				lock (_lock)
				{
					_currentLeaderId = currentLeader.HasValue ? currentLeader.ToString() : null;
				}

				LogLockAcquisitionFailed(CandidateId, _currentLeaderId);
			}
		}
		catch (Exception ex)
		{
			LogLockAcquisitionError(ex, CandidateId);
		}
	}

	private async Task ReleaseLockAsync()
	{
		try
		{
			var db = _redis.GetDatabase();

			// Only delete if we own the lock (Lua script for atomicity)
			const string script = @"
				if redis.call('get', KEYS[1]) == ARGV[1] then
					return redis.call('del', KEYS[1])
				else
					return 0
				end";

			_ = await db.ScriptEvaluateAsync(
				script,
				[_lockKey],
				[CandidateId]).ConfigureAwait(false);

			LogLockReleased(CandidateId);
		}
		catch (Exception ex)
		{
			LogLockReleaseError(ex, CandidateId);
		}
	}

	private async Task RunRenewalLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_options.RenewInterval, cancellationToken).ConfigureAwait(false);

				if (!_isLeader)
				{
					// Try to acquire lock
					await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);
				}
				else
				{
					// Renew lease
					var renewed = await RenewLeaseAsync(cancellationToken).ConfigureAwait(false);
					if (!renewed)
					{
						// Check if we've exceeded the grace period
						var elapsed = DateTimeOffset.UtcNow - _lastSuccessfulRenewal;
						if (elapsed > _options.GracePeriod)
						{
							LoseLeadership();
						}
					}
					else
					{
						_lastSuccessfulRenewal = DateTimeOffset.UtcNow;
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogRenewalError(ex, CandidateId);

				if (_isLeader)
				{
					var elapsed = DateTimeOffset.UtcNow - _lastSuccessfulRenewal;
					if (elapsed > _options.GracePeriod)
					{
						LoseLeadership();
					}
				}
			}
		}
	}

	private async Task<bool> RenewLeaseAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var db = _redis.GetDatabase();

			// Only extend TTL if we own the lock (Lua script for atomicity)
			const string script = @"
				if redis.call('get', KEYS[1]) == ARGV[1] then
					return redis.call('pexpire', KEYS[1], ARGV[2])
				else
					return 0
				end";

			var result = await db.ScriptEvaluateAsync(
				script,
				[_lockKey],
				[CandidateId, (long)_options.LeaseDuration.TotalMilliseconds]).ConfigureAwait(false);

			return (long)result == 1;
		}
		catch (Exception ex)
		{
			LogRenewalWarning(ex, CandidateId);
			return false;
		}
	}

	private void BecomeLeader()
	{
		string? previousLeader;

		lock (_lock)
		{
			if (_isLeader)
			{
				return;
			}

			previousLeader = _currentLeaderId;
			_isLeader = true;
			_currentLeaderId = CandidateId;
			_lastSuccessfulRenewal = DateTimeOffset.UtcNow;
		}

		LogBecameLeader(CandidateId, _lockKey);

		BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockKey));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, CandidateId, _lockKey));
	}

	private void LoseLeadership()
	{
		lock (_lock)
		{
			if (!_isLeader)
			{
				return;
			}

			_isLeader = false;
			var previousLeader = _currentLeaderId;
			_currentLeaderId = null;

			LogLostLeadership(CandidateId, _lockKey);

			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockKey));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, _lockKey));
		}
	}

	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.RedisStarting, LogLevel.Information,
		"Starting leader election for candidate {CandidateId} on key {Key}")]
	partial void LogStarting(string candidateId, string key);

	[LoggerMessage(LeaderElectionEventId.RedisStopping, LogLevel.Information, "Stopping leader election for candidate {CandidateId}")]
	partial void LogStopping(string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisLockAcquisitionFailed, LogLevel.Debug,
		"Failed to acquire lock for {CandidateId}, current leader: {Leader}")]
	partial void LogLockAcquisitionFailed(string candidateId, string? leader);

	[LoggerMessage(LeaderElectionEventId.RedisLockAcquisitionError, LogLevel.Error, "Error acquiring lock for {CandidateId}")]
	partial void LogLockAcquisitionError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisLockReleased, LogLevel.Debug, "Released lock for {CandidateId}")]
	partial void LogLockReleased(string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisLockReleaseError, LogLevel.Warning, "Error releasing lock for {CandidateId}")]
	partial void LogLockReleaseError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisRenewalError, LogLevel.Error, "Error in renewal loop for {CandidateId}")]
	partial void LogRenewalError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisRenewalWarning, LogLevel.Warning, "Error renewing lease for {CandidateId}")]
	partial void LogRenewalWarning(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisBecameLeader, LogLevel.Information, "Candidate {CandidateId} became leader for key {Key}")]
	partial void LogBecameLeader(string candidateId, string key);

	[LoggerMessage(LeaderElectionEventId.RedisLostLeadership, LogLevel.Warning, "Candidate {CandidateId} lost leadership for key {Key}")]
	partial void LogLostLeadership(string candidateId, string key);
}
