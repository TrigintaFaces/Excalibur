// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
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
	// ot72w3: optional split-brain-safety refinement (parity with SqlServerLeaderElection). When
	// supplied, a renewal fault classified as definitively Permanent relinquishes leadership IMMEDIATELY
	// (accelerate-only); transient, poison, unclassified, or absent-classifier faults all retain the full
	// grace-period wait. Classification can only SHORTEN time-to-relinquish, never extend it — the grace
	// period stays the hard upper bound, so no split-brain window is ever added.
	private readonly IMessageFailureClassifier? _failureClassifier;
	// umemwa/ADR-339: optional fencing-token provider. When supplied, a monotonic fencing token is issued
	// (atomically, store-side) at each leadership acquisition so a stale leader's token falls below the
	// high-water mark and its protected operations are rejected by FencingTokenMiddleware. Null when the
	// consumer did not enable fencing (no token issued — fully backward compatible, opt-in).
	private readonly IFencingTokenProvider? _fencingTokenProvider;

	// 762uzn: bounded retry budget for minting the fencing token on acquisition before relinquishing
	// (fail-closed). Small + fixed: the renewal loop supplies the outer retry cadence (RenewInterval),
	// this just rides out a transient store blip without instantly surrendering a freshly-acquired lease.
	private const int FencingTokenMintMaxAttempts = 3;

	private readonly Lock _lock = new();

	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _disposed;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	// Stored as UTC ticks accessed via Interlocked (a58yu6): the renewal task reads/writes this
	// lock-free while BecomeLeader writes it under _lock, so a multi-field DateTimeOffset would tear.
	private long _lastSuccessfulRenewalTicks;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisLeaderElection"/> class.
	/// </summary>
	/// <param name="redis">The Redis connection multiplexer.</param>
	/// <param name="lockKey">The Redis key for the leader lock (e.g., "myapp:leader").</param>
	/// <param name="options">The leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="failureClassifier">
	/// An optional <see cref="IMessageFailureClassifier"/> (ot72w3). When supplied, a renewal-loop fault
	/// classified <see cref="MessageFailureKind.Permanent"/> triggers an immediate self-demotion instead of
	/// waiting the full grace period; transient, poison, unclassified, or absent-classifier faults retain
	/// the grace-period wait. The classifier can only accelerate (never delay) relinquish, so the grace
	/// period remains the hard upper bound on stale-leader tenure. Defaults to <see langword="null"/>
	/// (grace-only behavior — fully backward compatible, opt-in).
	/// </param>
	/// <param name="fencingTokenProvider">
	/// An optional <see cref="IFencingTokenProvider"/>. When supplied, a monotonic fencing
	/// token is issued at each leadership acquisition (at the <c>BecomeLeader</c> transition), advancing the
	/// resource's high-water mark so a previous leader's token is rejected by the fencing middleware.
	/// Defaults to <see langword="null"/> (no token issued — fully backward compatible, opt-in).
	/// </param>
	public RedisLeaderElection(
		IConnectionMultiplexer redis,
		string lockKey,
		IOptions<LeaderElectionOptions> options,
		ILogger<RedisLeaderElection> logger,
		IMessageFailureClassifier? failureClassifier = null,
		IFencingTokenProvider? fencingTokenProvider = null)
	{
		_redis = redis ?? throw new ArgumentNullException(nameof(redis));
		_lockKey = lockKey ?? throw new ArgumentNullException(nameof(lockKey));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_failureClassifier = failureClassifier;
		_fencingTokenProvider = fencingTokenProvider;

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
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_lock)
		{
			if (_isStarted)
			{
				return;
			}

			_isStarted = true;
		}

		LogStarting(CandidateId, _lockKey);

		// Start the renewal loop BEFORE the initial acquire so leadership can never be held without a
		// live renewer. RunRenewalLoopAsync waits one RenewInterval before its first action, so it cannot
		// race the eager initial acquire (which StartAsync still awaits — preserving synchronous
		// IsLeader-on-return). This makes "leadership declared with no active renewer" structurally
		// inexpressible: the renewer task provably pre-exists any BecomeLeader().
		_renewalCts = new CancellationTokenSource();
		_renewalTask = RunRenewalLoopAsync(_renewalCts.Token);

		try
		{
			await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// A cancelled initial acquire must not leak the renewer we just started. Tear it down and
			// reset start state so the caller can retry, then surface the original exception.
			await StopRenewerAsync().ConfigureAwait(false);

			lock (_lock)
			{
				_isStarted = false;
			}

			throw;
		}
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await StopCoreAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Disposes the leader election resources asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await StopCoreAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Cancels, awaits, and disposes the renewal loop and its cancellation source, leaving both fields
	/// null. Idempotent and safe when no renewer is running. Shared by the normal stop path and the
	/// cancelled-start cleanup so a failed <see cref="StartAsync"/> never leaks a background renewer.
	/// </summary>
	private async Task StopRenewerAsync()
	{
		if (_renewalCts != null)
		{
			await _renewalCts.CancelAsync().ConfigureAwait(false);
			if (_renewalTask != null)
			{
				try
				{
					await _renewalTask.ConfigureAwait(false);
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					// Expected
				}
			}

			_renewalCts.Dispose();
			_renewalCts = null;
			_renewalTask = null;
		}
	}

	private async Task StopCoreAsync()
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

		await StopRenewerAsync().ConfigureAwait(false);

		// Release lock if we hold it
		if (wasLeader)
		{
			await ReleaseLockAsync().ConfigureAwait(false);

			string? previousLeader;

			lock (_lock)
			{
				_isLeader = false;
				previousLeader = _currentLeaderId;
				_currentLeaderId = null;
			}

			// Raise consumer event handlers OUTSIDE the lock to avoid reentrancy/deadlock; the
			// snapshot taken under the lock keeps the event args consistent (no torn read).
			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockKey));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, _lockKey));
		}
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
				// 762uzn/msmwr9 (fail-CLOSED fencing): when a fencing-token provider is configured the fence
				// MUST be advanced BEFORE leadership is declared. SET NX returning acquired=true is itself the
				// genuine-acquisition signal (this candidate transitioned from not-holding to holding the key),
				// so exactly one token is minted per acquisition (ADR-339 Decision 1).
				if (_fencingTokenProvider is not null)
				{
					long token;
					try
					{
						token = await MintFencingTokenWithRetryAsync(cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						throw;
					}
					catch (Exception ex)
					{
						// Bounded mint retries exhausted: RELINQUISH rather than lead with an un-advanced fence
						// (the split-brain hazard 762uzn left open as fail-OPEN). Release the lock we just
						// acquired and do NOT declare leadership; the next renewal iteration re-attempts
						// acquire+mint. This is the structural fail-closed coupling — the leadership grant below
						// is unreachable unless the mint above succeeded.
						LogFencingTokenIssueError(ex, CandidateId, _lockKey);
						await ReleaseLockAsync().ConfigureAwait(false);
						return;
					}

					// Fence advanced — only NOW declare leadership. BecomeLeader fires BecameLeader/
					// LeaderChanged, so (msmwr9) those events are observed strictly AFTER the fence advanced.
					if (BecomeLeader())
					{
						LogFencingTokenIssued(CandidateId, token, _lockKey);
					}
				}
				else
				{
					// No fencing configured (opt-in): there is no fence to advance, so declare leadership
					// directly — fully backward compatible with the no-fencing guarantee the consumer accepted.
					_ = BecomeLeader();
				}
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
					// Renew lease (3-state: rqntzf)
					var verify = await RenewLeaseAsync(cancellationToken).ConfigureAwait(false);
					if (verify == LeaderVerifyResult.StillLeader)
					{
						Interlocked.Exchange(ref _lastSuccessfulRenewalTicks, DateTimeOffset.UtcNow.UtcTicks);
					}
					else
					{
						// rqntzf: a DEFINITIVELY-lost renew (owner-token Lua returned 0 — another holder owns the
						// key) relinquishes immediately via the accelerate-only ShouldRelinquish seam; an
						// Indeterminate renew (connection/transport fault) passes definitivelyLost=false so it stays
						// grace-gated — no false-relinquish on a transient blip. Grace stays the hard upper bound.
						var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _lastSuccessfulRenewalTicks), TimeSpan.Zero);
						if (ShouldRelinquish(kind: null, definitivelyLost: verify == LeaderVerifyResult.DefinitivelyLost, elapsed, _options.GracePeriod))
						{
							LoseLeadership();
						}
					}
				}
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogRenewalError(ex, CandidateId);

				if (_isLeader)
				{
					// ot72w3: a definitively-Permanent fault (e.g. auth/config) cannot recover by waiting, so it
					// relinquishes UNCONDITIONALLY this iteration (immediate, no elapsed dependence). Transient/
					// poison/unclassified faults — and the absent-classifier default — relinquish only once the full
					// grace period has elapsed (split-brain-safe / false-relinquish avoidance). Classification can
					// only ADD the immediate-relinquish trigger, never relax the grace bound, so a stale leader can
					// never hold past GracePeriod regardless of fault.
					var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _lastSuccessfulRenewalTicks), TimeSpan.Zero);
					// An exception is ambiguous about lock ownership (we couldn't probe), so definitivelyLost=false:
					// only the classifier's Permanent verdict accelerates here; everything else stays grace-gated.
					if (ShouldRelinquish(_failureClassifier?.Classify(ex), definitivelyLost: false, elapsed, _options.GracePeriod))
					{
						LoseLeadership();
					}
				}
			}
		}
	}

	private async Task<LeaderVerifyResult> RenewLeaseAsync(CancellationToken cancellationToken)
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

			// rqntzf: the owner-token Lua returns 1 when we still own the lock (renewed), or 0 when the stored
			// value no longer matches our token — an affirmative, DEFINITIVE loss (another holder took it). A
			// thrown connection/transport error is ambiguous and handled below as Indeterminate.
			return (long)result == 1 ? LeaderVerifyResult.StillLeader : LeaderVerifyResult.DefinitivelyLost;
		}
		catch (Exception ex)
		{
			// Connection/transport fault — we could not establish ownership either way; grace-gated.
			LogRenewalWarning(ex, CandidateId);
			return LeaderVerifyResult.Indeterminate;
		}
	}

	/// <summary>
	/// Transitions this candidate to leadership if it is not already the leader.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if a leadership transition occurred (this call became leader);
	/// <see langword="false"/> if this candidate was already the leader (re-entrancy guard).
	/// </returns>
	private bool BecomeLeader()
	{
		string? previousLeader;

		lock (_lock)
		{
			if (_isLeader)
			{
				return false;
			}

			previousLeader = _currentLeaderId;
			_isLeader = true;
			_currentLeaderId = CandidateId;
			Interlocked.Exchange(ref _lastSuccessfulRenewalTicks, DateTimeOffset.UtcNow.UtcTicks);
		}

		LogBecameLeader(CandidateId, _lockKey);

		BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockKey));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, CandidateId, _lockKey));

		return true;
	}

	/// <summary>
	/// Mints a monotonic fencing token for the protected resource, retrying a bounded number of times
	/// (<see cref="FencingTokenMintMaxAttempts"/>) before failing (762uzn, fail-CLOSED). The caller mints
	/// BEFORE declaring leadership and relinquishes if this throws, so the fence is always advanced before
	/// fenced leadership is granted.
	/// </summary>
	/// <remarks>
	/// The store-atomic mint (Redis <c>INCR</c>) advances the resource's cluster-wide high-water mark so a
	/// previous leader's now-lower token is rejected by <c>FencingTokenMiddleware</c>. A bounded retry rides
	/// out a transient store blip without instantly surrendering leadership, but once exhausted the method
	/// throws so the caller can relinquish: fencing is a split-brain safety control, so we never proceed as
	/// leader with an un-advanced fence (Microsoft "cross-cutting fails open" does NOT apply to a safety
	/// boundary). The renewal loop re-attempts acquire+mint on its next iteration.
	/// </remarks>
	/// <param name="cancellationToken">A token to observe while minting.</param>
	/// <returns>The newly minted, strictly-monotonic fencing token.</returns>
	/// <exception cref="InvalidOperationException">The mint failed on every bounded attempt.</exception>
	private async Task<long> MintFencingTokenWithRetryAsync(CancellationToken cancellationToken)
	{
		// Caller guarantees a provider is configured before invoking.
		Exception? lastError = null;

		for (var attempt = 1; attempt <= FencingTokenMintMaxAttempts; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				return await _fencingTokenProvider!.IssueTokenAsync(_lockKey, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				lastError = ex;
			}
		}

		throw new InvalidOperationException(
			$"Failed to mint a fencing token for resource '{_lockKey}' after {FencingTokenMintMaxAttempts} attempt(s); " +
			"relinquishing leadership rather than acting as a fenced leader with an un-advanced fence.",
			lastError);
	}

	/// <summary>
	/// Decides whether leadership must be relinquished for a renewal-loop fault (ot72w3). A definitively
	/// <see cref="MessageFailureKind.Permanent"/> fault relinquishes <em>unconditionally</em> (immediate,
	/// independent of <paramref name="elapsed"/>); every other classification — transient, poison, or an
	/// absent/null classifier — relinquishes only once <paramref name="elapsed"/> exceeds the full
	/// <paramref name="gracePeriod"/>.
	/// </summary>
	/// <remarks>
	/// The Permanent <em>and</em> definitive-loss cases are additional <em>OR</em> triggers over the grace
	/// check, so each can only cause an <em>earlier</em> relinquish, never relax the grace bound — a stale
	/// leader can never hold past <paramref name="gracePeriod"/> regardless of the fault. Mirrors
	/// <c>SqlServerLeaderElection</c> (no fork); a pure static so the accelerate-only invariant is bound
	/// structurally. Using an unconditional boolean (not an <c>elapsed &gt; Zero</c> comparison) keeps immediate
	/// relinquish correct under a non-monotonic clock.
	/// </remarks>
	/// <param name="kind">The classification of the renewal fault, or <see langword="null"/> when no classifier is configured.</param>
	/// <param name="definitivelyLost">
	/// <see langword="true"/> when the renewal verify <em>definitively</em> established that this candidate no
	/// longer holds the lock (rqntzf: the owner-token Lua returned 0 — another holder owns the key). An
	/// ambiguous/indeterminate verify (connection/transport fault) passes <see langword="false"/> so it stays
	/// grace-gated and never false-relinquishes.
	/// </param>
	/// <param name="elapsed">Time since the last successful renewal.</param>
	/// <param name="gracePeriod">The configured grace period (the hard upper bound).</param>
	/// <returns><see langword="true"/> if leadership should be relinquished this iteration.</returns>
	internal static bool ShouldRelinquish(MessageFailureKind? kind, bool definitivelyLost, TimeSpan elapsed, TimeSpan gracePeriod)
		=> kind == MessageFailureKind.Permanent || definitivelyLost || elapsed > gracePeriod;

	/// <summary>
	/// The three-state outcome of a renewal-time lock-ownership verify (rqntzf). Distinguishes a
	/// <em>definitive</em> loss (the owner-token Lua affirmatively reported another holder owns the key) from an
	/// <em>indeterminate</em> result (a connection/transport fault) so that only the former accelerates
	/// relinquish; the latter stays grace-gated to avoid a false-relinquish on a transient blip.
	/// </summary>
	private enum LeaderVerifyResult
	{
		/// <summary>The renew confirmed this candidate still holds the lock (Lua returned 1, lease extended).</summary>
		StillLeader,

		/// <summary>The renew affirmatively established this candidate no longer holds the lock (Lua returned 0).</summary>
		DefinitivelyLost,

		/// <summary>Ownership could not be established (connection/transport fault) — grace-gated.</summary>
		Indeterminate,
	}

	private void LoseLeadership()
	{
		string? previousLeader;

		lock (_lock)
		{
			if (!_isLeader)
			{
				return;
			}

			_isLeader = false;
			previousLeader = _currentLeaderId;
			_currentLeaderId = null;
		}

		LogLostLeadership(CandidateId, _lockKey);

		// Raise consumer event handlers OUTSIDE the lock to avoid reentrancy/deadlock; the
		// snapshot taken under the lock keeps the event args consistent (no torn read).
		LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockKey));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, _lockKey));
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

	[LoggerMessage(LeaderElectionEventId.RedisFencingTokenIssued, LogLevel.Information,
		"Candidate {CandidateId} issued fencing token {Token} for resource {Resource}")]
	partial void LogFencingTokenIssued(string candidateId, long token, string resource);

	[LoggerMessage(LeaderElectionEventId.RedisFencingTokenIssueError, LogLevel.Error,
		"Candidate {CandidateId} failed to mint a fencing token for resource {Resource} after bounded retries; relinquishing leadership (fail-closed) rather than leading with an un-advanced fence")]
	partial void LogFencingTokenIssueError(Exception ex, string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.RedisLostLeadership, LogLevel.Warning, "Candidate {CandidateId} lost leadership for key {Key}")]
	partial void LogLostLeadership(string candidateId, string key);
}
