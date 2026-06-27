// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="ILeaderElection"/> using sp_getapplock.
/// </summary>
/// <remarks>
/// Uses SQL Server application locks (sp_getapplock/sp_releaseapplock) for distributed
/// leader election. The lock is held for the duration of the connection and automatically
/// released if the connection drops.
/// </remarks>
public sealed partial class SqlServerLeaderElection : ILeaderElection, IAsyncDisposable
{
	private readonly string _connectionString;
	private readonly string _lockResource;
	private readonly LeaderElectionOptions _options;
	private readonly ILogger<SqlServerLeaderElection> _logger;
	// ot72w3: optional split-brain-safety refinement. When supplied, a renewal fault classified
	// as definitively Permanent relinquishes leadership IMMEDIATELY (accelerate-only); transient,
	// poison, unclassified, or absent-classifier faults all retain the full grace-period wait.
	// Classification can only SHORTEN time-to-relinquish, never extend it — the grace period stays
	// the hard upper bound, so no split-brain window is ever added.
	private readonly IMessageFailureClassifier? _failureClassifier;
	// nxmjpm/ADR-339: optional fencing-token provider. When supplied, a monotonic fencing token is minted
	// (atomically, store-side via a SQL SEQUENCE) BEFORE leadership is declared at each acquisition, so a stale
	// leader's token falls below the high-water mark and its protected operations are rejected by
	// FencingTokenMiddleware. Null when the consumer did not enable fencing (opt-in, fully backward compatible).
	private readonly IFencingTokenProvider? _fencingTokenProvider;

	// nxmjpm: bounded retry budget for minting the fencing token on acquisition before relinquishing
	// (fail-closed). Small + fixed: the renewal loop supplies the outer retry cadence (RenewInterval); this
	// just rides out a transient store blip without instantly surrendering a freshly-acquired lock. Mirrors
	// the Redis reference (RedisLeaderElection, bd-762uzn).
	private const int FencingTokenMintMaxAttempts = 3;

	private readonly Lock _lock = new();

	private SqlConnection? _connection;
	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	private int _disposed;
	// Stored as UTC ticks accessed via Interlocked (a58yu6): the renewal task reads/writes this
	// lock-free while BecomeLeader writes it under _lock, so a multi-field DateTimeOffset would tear.
	private long _lastSuccessfulRenewalTicks;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerLeaderElection"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
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
	/// An optional <see cref="IFencingTokenProvider"/> (nxmjpm/ADR-339). When supplied, a monotonic fencing
	/// token is minted at each leadership acquisition (BEFORE leadership is declared); if the mint cannot be
	/// advanced after bounded retries the candidate relinquishes rather than leading with a stale fence.
	/// Defaults to <see langword="null"/> (no token issued; opt-in, backward compatible).
	/// </param>
	public SqlServerLeaderElection(
		string connectionString,
		string lockResource,
		IOptions<LeaderElectionOptions> options,
		ILogger<SqlServerLeaderElection> logger,
		IMessageFailureClassifier? failureClassifier = null,
		IFencingTokenProvider? fencingTokenProvider = null)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		_connectionString = BuildLockConnectionString(connectionString);
		_lockResource = lockResource ?? throw new ArgumentNullException(nameof(lockResource));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_failureClassifier = failureClassifier;
		_fencingTokenProvider = fencingTokenProvider;

		CandidateId = _options.InstanceId ?? (Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8]);
	}

	/// <summary>
	/// Builds the connection string used for the lock-holding connection, hardened so that a lost
	/// session-scoped <c>sp_getapplock</c> is never masked by a transparently-reconnected connection.
	/// </summary>
	/// <remarks>
	/// The application lock is session-scoped: it lives on the physical connection's server session and
	/// is released when that session ends. <see cref="SqlConnectionStringBuilder.Pooling"/> is disabled so
	/// the lock connection is dedicated (never reset/reused by the pool), and
	/// <see cref="SqlConnectionStringBuilder.ConnectRetryCount"/> is set to 0 so a dropped session surfaces
	/// immediately as a closed/broken connection rather than silently reconnecting to a NEW session that
	/// does not hold the lock — which would otherwise yield false-positive leadership (split-brain).
	/// </remarks>
	/// <param name="connectionString">The caller-supplied connection string.</param>
	/// <returns>The hardened connection string.</returns>
	private static string BuildLockConnectionString(string connectionString)
	{
		var builder = new SqlConnectionStringBuilder(connectionString)
		{
			Pooling = false,
			ConnectRetryCount = 0,
		};

		return builder.ConnectionString;
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
		ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

		lock (_lock)
		{
			if (_isStarted)
			{
				return;
			}

			_isStarted = true;
		}

		LogStarting(CandidateId, _lockResource);

		await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);

		// Start renewal loop
		_renewalCts = new CancellationTokenSource();
		_renewalTask = RunRenewalLoopAsync(_renewalCts.Token);
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
		await StopCoreAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Disposes the leader election resources asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		// 8aef8d: call the SHARED core, never StopAsync — DisposeAsync has already set _disposed, so
		// StopAsync would throw ObjectDisposedException and abort before cancelling the renewal loop.
		// That left the loop running against a disposed CancellationTokenSource (Dispose does NOT cancel),
		// whose Task.Delay then threw ObjectDisposedException (not OperationCanceledException, so the
		// break-guard never fired) on every iteration — a leaked hot error-spin. Routing disposal through
		// StopCoreAsync cancels + awaits the loop deterministically.
		try
		{
			await StopCoreAsync().ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Safe cleanup during disposal — never throw from DisposeAsync.
		}

		if (_connection != null)
		{
			await _connection.DisposeAsync().ConfigureAwait(false);
			_connection = null;
		}

		_renewalCts?.Dispose();
	}

	/// <summary>
	/// Shared stop logic invoked by both <see cref="StopAsync"/> and <see cref="DisposeAsync"/>. Cancels and
	/// awaits the renewal loop, releases the application lock, and (if this candidate was leader) raises the
	/// loss events. Deliberately performs no <c>_disposed</c> check so disposal can reuse it (8aef8d).
	/// </summary>
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
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					// Expected: the renewal loop observed its OWN cancellation token (7npc0q-S2: filter on
					// the exception's token, not a caller token DisposeAsync never supplies).
				}
			}

			_renewalCts.Dispose();
			_renewalCts = null;
			_renewalTask = null;
		}

		// Release lock
		await ReleaseLockAsync().ConfigureAwait(false);

		if (wasLeader)
		{
			string? previousLeader;

			lock (_lock)
			{
				_isLeader = false;
				previousLeader = _currentLeaderId;
				_currentLeaderId = null;
			}

			// Raise consumer event handlers OUTSIDE the lock to avoid reentrancy/deadlock:
			// the snapshot taken under the lock keeps the event args consistent (no torn read).
			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockResource));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, _lockResource));
		}
	}

	private async Task TryAcquireLockAsync(CancellationToken cancellationToken)
	{
		try
		{
			_connection = new SqlConnection(_connectionString);
			await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var timeoutMs = (int)_options.RetryInterval.TotalMilliseconds;

			await using var command = new SqlCommand("sp_getapplock", _connection)
			{
				CommandType = System.Data.CommandType.StoredProcedure
			};

			_ = command.Parameters.AddWithValue("@Resource", _lockResource);
			_ = command.Parameters.AddWithValue("@LockMode", "Exclusive");
			_ = command.Parameters.AddWithValue("@LockOwner", "Session");
			_ = command.Parameters.AddWithValue("@LockTimeout", timeoutMs);

			var returnValue = command.Parameters.Add("@ReturnValue", System.Data.SqlDbType.Int);
			returnValue.Direction = System.Data.ParameterDirection.ReturnValue;

			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			var result = (int)returnValue.Value;

			if (result >= 0)
			{
				// nxmjpm/ADR-339 (fail-CLOSED fencing): when a provider is configured, advance the fence BEFORE
				// declaring leadership; on bounded-retry exhaustion RELINQUISH (release the applock, do NOT become
				// leader). BecomeLeader (and its events) is structurally unreachable unless the mint succeeded —
				// mirrors the Redis reference (RedisLeaderElection, bd-762uzn).
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
						// Bounded mint retries exhausted: relinquish rather than lead with an un-advanced fence.
						// ReleaseLockAsync releases sp_releaseapplock + closes the lock connection; the next
						// renewal iteration re-attempts acquire+mint.
						LogFencingTokenIssueError(ex, CandidateId, _lockResource);
						await ReleaseLockAsync().ConfigureAwait(false);
						return;
					}

					// Fence advanced — only NOW declare leadership (BecomeLeader fires BecameLeader/LeaderChanged
					// strictly AFTER the fence is advanced).
					BecomeLeader();
					LogFencingTokenIssued(CandidateId, token, _lockResource);
				}
				else
				{
					// No fencing configured (opt-in): declare leadership directly — backward compatible.
					BecomeLeader();
				}
			}
			else
			{
				LogLockAcquisitionFailed(CandidateId, result);

				// Close connection since we didn't get the lock
				await _connection.CloseAsync().ConfigureAwait(false);
				_connection = null;
			}
		}
		catch (Exception ex)
		{
			LogLockAcquisitionError(ex, CandidateId);

			if (_connection != null)
			{
				await _connection.CloseAsync().ConfigureAwait(false);
				_connection = null;
			}
		}
	}

	private async Task ReleaseLockAsync()
	{
		if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
		{
			return;
		}

		try
		{
			await using var command = new SqlCommand("sp_releaseapplock", _connection)
			{
				CommandType = System.Data.CommandType.StoredProcedure
			};

			_ = command.Parameters.AddWithValue("@Resource", _lockResource);
			_ = command.Parameters.AddWithValue("@LockOwner", "Session");

			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

			LogLockReleased(CandidateId);
		}
		catch (Exception ex)
		{
			LogLockReleaseError(ex, CandidateId);
		}
		finally
		{
			await _connection.CloseAsync().ConfigureAwait(false);
			_connection = null;
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
					// Verify we still hold the lock (3-state: rqntzf).
					var verify = await VerifyLockAsync(cancellationToken).ConfigureAwait(false);
					if (verify == LeaderVerifyResult.StillLeader)
					{
						Interlocked.Exchange(ref _lastSuccessfulRenewalTicks, DateTimeOffset.UtcNow.UtcTicks);
					}
					else
					{
						// rqntzf: a DEFINITIVELY-lost verify (APPLOCK_MODE probe succeeded and returned NoLock)
						// relinquishes immediately this iteration via the accelerate-only ShouldRelinquish seam;
						// an Indeterminate verify (connection down / probe threw) passes definitivelyLost=false so
						// it stays grace-gated — no false-relinquish on an ambiguous probe. Accelerate-only: the
						// grace period remains the hard split-brain upper bound.
						var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _lastSuccessfulRenewalTicks), TimeSpan.Zero);
						if (ShouldRelinquish(kind: null, definitivelyLost: verify == LeaderVerifyResult.DefinitivelyLost, elapsed, _options.GracePeriod))
						{
							await LoseLeadershipAsync().ConfigureAwait(false);
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
					// grace period has elapsed: the session-scoped applock may still be held during a transient blip,
					// so we must NOT relinquish early (split-brain-safe / false-relinquish avoidance). Classification
					// can only ADD the immediate-relinquish trigger, never relax the grace bound, so a stale leader
					// can never hold past GracePeriod regardless of fault.
					var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _lastSuccessfulRenewalTicks), TimeSpan.Zero);
					// An exception is ambiguous about lock ownership (we couldn't probe), so definitivelyLost=false:
					// only the classifier's Permanent verdict accelerates here; everything else stays grace-gated.
					if (ShouldRelinquish(_failureClassifier?.Classify(ex), definitivelyLost: false, elapsed, _options.GracePeriod))
					{
						await LoseLeadershipAsync().ConfigureAwait(false);
					}
				}
			}
		}
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
	/// leader can never hold leadership past <paramref name="gracePeriod"/> regardless of the fault (the grace
	/// period remains the hard split-brain upper bound). Extracted as a pure static so the accelerate-only
	/// invariant is bound structurally. Using an unconditional boolean (not an <c>elapsed &gt; Zero</c>
	/// comparison) keeps immediate relinquish correct even under a non-monotonic clock / zero-or-negative
	/// elapsed.
	/// </remarks>
	/// <param name="kind">The classification of the renewal fault, or <see langword="null"/> when no classifier is configured.</param>
	/// <param name="definitivelyLost">
	/// <see langword="true"/> when the renewal verify <em>definitively</em> established that this session no
	/// longer holds the lock (rqntzf: an <c>APPLOCK_MODE</c> probe that succeeded and returned <c>NoLock</c>).
	/// An ambiguous/indeterminate verify (connection down, probe threw) passes <see langword="false"/> so it
	/// stays grace-gated and never false-relinquishes.
	/// </param>
	/// <param name="elapsed">Time since the last successful renewal.</param>
	/// <param name="gracePeriod">The configured grace period (the hard upper bound).</param>
	/// <returns><see langword="true"/> if leadership should be relinquished this iteration.</returns>
	internal static bool ShouldRelinquish(MessageFailureKind? kind, bool definitivelyLost, TimeSpan elapsed, TimeSpan gracePeriod)
		=> kind == MessageFailureKind.Permanent || definitivelyLost || elapsed > gracePeriod;

	/// <summary>
	/// The three-state outcome of a renewal-time lock-ownership verify (rqntzf). Distinguishes a
	/// <em>definitive</em> loss (the store affirmatively reports we do not hold the lock) from an
	/// <em>indeterminate</em> result (we could not establish ownership) so that only the former accelerates
	/// relinquish; the latter stays grace-gated to avoid a false-relinquish on a transient blip.
	/// </summary>
	private enum LeaderVerifyResult
	{
		/// <summary>The verify confirmed this session still holds the lock.</summary>
		StillLeader,

		/// <summary>The verify affirmatively established this session no longer holds the lock.</summary>
		DefinitivelyLost,

		/// <summary>Ownership could not be established (connection down, probe threw) — grace-gated.</summary>
		Indeterminate,
	}

	/// <summary>
	/// Verifies that the current session still actually holds the <c>sp_getapplock</c> application lock —
	/// ownership, not merely connection liveness.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>APPLOCK_MODE('public', resource, 'Session')</c> returns the lock mode held by the <em>current
	/// session</em> for the resource, or <c>'NoLock'</c> if this session does not hold it. This is a
	/// reliable, non-blocking ownership probe.
	/// </para>
	/// <para>
	/// A plain connection-liveness check (<c>SELECT 1</c>) is insufficient: across an AlwaysOn/failover
	/// event, a pool reset, or a transparent reconnect, the connection can answer queries on a <em>new</em>
	/// server session that never re-acquired the application lock. Liveness would falsely report continued
	/// ownership while another node acquires the lock on the new primary — a silent split-brain. Combined
	/// with the hardened connection (pooling and connection-resiliency disabled, see
	/// <see cref="BuildLockConnectionString"/>), a lost session now surfaces as either a closed connection
	/// or a <c>'NoLock'</c> result, both of which relinquish leadership.
	/// </para>
	/// <para>
	/// This addresses the reachable failover/pool-reset split-brain. The orthogonal paused/stalled-leader
	/// split-brain (which no connectivity check can catch) is mitigated by fencing tokens, tracked
	/// separately as <c>umemwa</c>.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Token to cancel the verification.</param>
	/// <returns>
	/// <see cref="LeaderVerifyResult.StillLeader"/> when the <c>APPLOCK_MODE</c> probe succeeded and reports a
	/// held lock; <see cref="LeaderVerifyResult.DefinitivelyLost"/> when the probe succeeded and reports
	/// <c>NoLock</c> (the server affirmatively says this session does not hold the lock — the only definitive
	/// loss signal); <see cref="LeaderVerifyResult.Indeterminate"/> when the connection is closed/broken or the
	/// probe threw (we could not establish ownership either way — stays grace-gated, rqntzf).
	/// </returns>
	private async Task<LeaderVerifyResult> VerifyLockAsync(CancellationToken cancellationToken)
	{
		if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
		{
			return LeaderVerifyResult.Indeterminate;
		}

		try
		{
			await using var command = new SqlCommand("SELECT APPLOCK_MODE('public', @Resource, 'Session')", _connection);
			_ = command.Parameters.AddWithValue("@Resource", _lockResource);

			if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not string mode)
			{
				// Unexpected null/non-string (APPLOCK_MODE normally returns 'NoLock', never null) — ambiguous.
				return LeaderVerifyResult.Indeterminate;
			}

			return string.Equals(mode, "NoLock", StringComparison.OrdinalIgnoreCase)
				? LeaderVerifyResult.DefinitivelyLost
				: LeaderVerifyResult.StillLeader;
		}
		catch
		{
			return LeaderVerifyResult.Indeterminate;
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
			Interlocked.Exchange(ref _lastSuccessfulRenewalTicks, DateTimeOffset.UtcNow.UtcTicks);
		}

		LogBecameLeader(CandidateId, _lockResource);

		BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockResource));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, CandidateId, _lockResource));
	}

	private async Task LoseLeadershipAsync()
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

		LogLostLeadership(CandidateId, _lockResource);

		// Raise consumer event handlers OUTSIDE the lock to avoid reentrancy/deadlock; the
		// snapshot taken under the lock keeps the event args consistent (no torn read).
		LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _lockResource));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, _lockResource));

		// Clean up connection asynchronously
		if (_connection != null)
		{
			try
			{
				await _connection.CloseAsync().ConfigureAwait(false);
			}
			catch
			{
				// Ignore connection close errors during leadership loss
			}

			await _connection.DisposeAsync().ConfigureAwait(false);
			_connection = null;
		}
	}

	/// <summary>
	/// Mints a monotonic fencing token for the protected resource, retrying a bounded number of times
	/// (<see cref="FencingTokenMintMaxAttempts"/>) before failing (nxmjpm, fail-CLOSED). The caller mints
	/// BEFORE declaring leadership and relinquishes if this throws, so the fence is always advanced before
	/// fenced leadership is granted (ADR-339). Mirrors the Redis reference.
	/// </summary>
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
				return await _fencingTokenProvider!.IssueTokenAsync(_lockResource, cancellationToken).ConfigureAwait(false);
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
			$"Failed to mint a fencing token for resource '{_lockResource}' after {FencingTokenMintMaxAttempts} attempt(s); " +
			"relinquishing leadership rather than acting as a fenced leader with an un-advanced fence.",
			lastError);
	}

	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.SqlServerStarting, LogLevel.Information,
		"Starting leader election for candidate {CandidateId} on resource {Resource}")]
	partial void LogStarting(string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerStopping, LogLevel.Information, "Stopping leader election for candidate {CandidateId}")]
	partial void LogStopping(string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockAcquisitionFailed, LogLevel.Debug,
		"Failed to acquire lock for {CandidateId}, result: {Result}")]
	partial void LogLockAcquisitionFailed(string candidateId, int result);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockAcquisitionError, LogLevel.Error, "Error acquiring lock for {CandidateId}")]
	partial void LogLockAcquisitionError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockReleased, LogLevel.Debug, "Released lock for {CandidateId}")]
	partial void LogLockReleased(string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockReleaseError, LogLevel.Warning, "Error releasing lock for {CandidateId}")]
	partial void LogLockReleaseError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerRenewalError, LogLevel.Error, "Error in renewal loop for {CandidateId}")]
	partial void LogRenewalError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerBecameLeader, LogLevel.Information,
		"Candidate {CandidateId} became leader for resource {Resource}")]
	partial void LogBecameLeader(string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerLostLeadership, LogLevel.Warning,
		"Candidate {CandidateId} lost leadership for resource {Resource}")]
	partial void LogLostLeadership(string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerFencingTokenIssued, LogLevel.Information,
		"Candidate {CandidateId} issued fencing token {Token} for resource {Resource}")]
	partial void LogFencingTokenIssued(string candidateId, long token, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerFencingTokenIssueError, LogLevel.Error,
		"Candidate {CandidateId} failed to mint a fencing token for resource {Resource} after bounded retries; relinquishing leadership (fail-closed) rather than leading with an un-advanced fence")]
	partial void LogFencingTokenIssueError(Exception ex, string candidateId, string resource);
}
