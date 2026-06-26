// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Postgres implementation of <see cref="ILeaderElection"/> using advisory locks.
/// </summary>
/// <remarks>
/// <para>
/// Uses Postgres session-level advisory locks (<c>pg_try_advisory_lock</c>) for distributed
/// leader election. The lock is held for the duration of the connection and automatically
/// released when the connection drops or is closed.
/// </para>
/// <para>
/// Advisory locks are lightweight and do not conflict with regular table locks.
/// They are ideal for coordination scenarios like leader election because:
/// </para>
/// <list type="bullet">
/// <item>They survive transaction boundaries (session-scoped).</item>
/// <item>They are automatically released on connection loss.</item>
/// <item>They have minimal overhead compared to row-level locks.</item>
/// </list>
/// </remarks>
public sealed partial class PostgresLeaderElection : ILeaderElection, IAsyncDisposable
{
	private readonly PostgresLeaderElectionOptions _pgOptions;
	private readonly LeaderElectionOptions _electionOptions;
	private readonly ILogger<PostgresLeaderElection> _logger;
	private readonly string _connectionString;

	private readonly Lock _lock = new();

	private NpgsqlConnection? _connection;
	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	private volatile bool _disposed;
	// Stored as UTC ticks accessed via Interlocked (a58yu6): the renewal task reads/writes this
	// lock-free while BecomeLeader writes it under _lock, so a multi-field DateTimeOffset would tear.
	private long _lastSuccessfulRenewalTicks;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresLeaderElection"/> class.
	/// </summary>
	/// <param name="pgOptions">The Postgres leader election options.</param>
	/// <param name="electionOptions">The leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresLeaderElection(
		IOptions<PostgresLeaderElectionOptions> pgOptions,
		IOptions<LeaderElectionOptions> electionOptions,
		ILogger<PostgresLeaderElection> logger)
	{
		ArgumentNullException.ThrowIfNull(pgOptions);
		ArgumentNullException.ThrowIfNull(electionOptions);
		ArgumentNullException.ThrowIfNull(logger);

		_pgOptions = pgOptions.Value;
		_pgOptions.Validate();
		_electionOptions = electionOptions.Value;
		_logger = logger;
		_connectionString = BuildLockConnectionString(_pgOptions.ConnectionString);

		CandidateId = _electionOptions.InstanceId ?? (Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8]);
	}

	/// <summary>
	/// Builds the connection string used for the lock-holding connection, hardened so that a lost
	/// session-scoped advisory lock is never masked by a pooled/reused connection.
	/// </summary>
	/// <remarks>
	/// A Postgres advisory lock is session-scoped: it lives on the backend session and is released when
	/// that session ends. <see cref="NpgsqlConnectionStringBuilder.Pooling"/> is disabled so the lock
	/// connection is dedicated (never reset/reused by the pool as a different backend session), ensuring a
	/// dropped session surfaces as a broken connection rather than silently continuing on a new backend
	/// that does not hold the advisory lock — which would otherwise yield false-positive leadership
	/// (split-brain).
	/// </remarks>
	/// <param name="connectionString">The caller-supplied connection string.</param>
	/// <returns>The hardened connection string.</returns>
	private static string BuildLockConnectionString(string connectionString)
	{
		var builder = new NpgsqlConnectionStringBuilder(connectionString)
		{
			Pooling = false,
		};

		return builder.ConnectionString;
	}

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <inheritdoc/>
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

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

		LogStarting(CandidateId, _pgOptions.LockKey);

		await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);

		_renewalCts = new CancellationTokenSource();
		_renewalTask = RunRenewalLoopAsync(_renewalCts.Token);
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

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

		if (_renewalCts != null)
		{
			await _renewalCts.CancelAsync().ConfigureAwait(false);
			if (_renewalTask != null)
			{
				try
				{
					await _renewalTask.ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					// Expected
				}
			}

			_renewalCts.Dispose();
			_renewalCts = null;
			_renewalTask = null;
		}

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

			// Raise consumer event handlers OUTSIDE the lock to avoid reentrancy/deadlock; the
			// snapshot taken under the lock keeps the event args consistent (no torn read).
			var resource = _pgOptions.LockKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, resource));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, resource));
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_isStarted)
		{
			try
			{
				await StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogDisposeError(ex, CandidateId);
			}
		}

		if (_connection != null)
		{
			await _connection.DisposeAsync().ConfigureAwait(false);
			_connection = null;
		}

		_renewalCts?.Dispose();
	}

	private async Task TryAcquireLockAsync(CancellationToken cancellationToken)
	{
		try
		{
			_connection = new NpgsqlConnection(_connectionString);
			await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lockKey)", _connection)
			{
				CommandTimeout = _pgOptions.CommandTimeoutSeconds
			};

			command.Parameters.AddWithValue("lockKey", _pgOptions.LockKey);

			var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

			if (result is true)
			{
				BecomeLeader();
			}
			else
			{
				LogLockAcquisitionFailed(CandidateId, _pgOptions.LockKey);

				await _connection.CloseAsync().ConfigureAwait(false);
				await _connection.DisposeAsync().ConfigureAwait(false);
				_connection = null;
			}
		}
		catch (Exception ex)
		{
			LogLockAcquisitionError(ex, CandidateId);

			if (_connection != null)
			{
				await _connection.CloseAsync().ConfigureAwait(false);
				await _connection.DisposeAsync().ConfigureAwait(false);
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
			await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@lockKey)", _connection)
			{
				CommandTimeout = _pgOptions.CommandTimeoutSeconds
			};

			command.Parameters.AddWithValue("lockKey", _pgOptions.LockKey);

			_ = await command.ExecuteScalarAsync().ConfigureAwait(false);

			LogLockReleased(CandidateId);
		}
		catch (Exception ex)
		{
			LogLockReleaseError(ex, CandidateId);
		}
		finally
		{
			await _connection.CloseAsync().ConfigureAwait(false);
			await _connection.DisposeAsync().ConfigureAwait(false);
			_connection = null;
		}
	}

	private async Task RunRenewalLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_electionOptions.RenewInterval, cancellationToken).ConfigureAwait(false);

				if (!_isLeader)
				{
					await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);
				}
				else
				{
					var stillLeader = await VerifyLockAsync(cancellationToken).ConfigureAwait(false);
					if (!stillLeader)
					{
						var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _lastSuccessfulRenewalTicks), TimeSpan.Zero);
						if (elapsed > _electionOptions.GracePeriod)
						{
							await LoseLeadershipAsync().ConfigureAwait(false);
						}
					}
					else
					{
						Interlocked.Exchange(ref _lastSuccessfulRenewalTicks, DateTimeOffset.UtcNow.UtcTicks);
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
					var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _lastSuccessfulRenewalTicks), TimeSpan.Zero);
					if (elapsed > _electionOptions.GracePeriod)
					{
						await LoseLeadershipAsync().ConfigureAwait(false);
					}
				}
			}
		}
	}

	/// <summary>
	/// Verifies that the current backend session still actually holds the advisory lock — ownership, not
	/// merely connection liveness.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The probe queries <c>pg_locks</c> for a granted advisory lock owned by the current backend
	/// (<c>pid = pg_backend_pid()</c>). Because this connection is dedicated (pooling disabled, see
	/// <see cref="BuildLockConnectionString"/>) and only ever takes this one advisory lock, "this backend
	/// holds a granted advisory lock" is equivalent to "we still hold our lock" — no lock-key decoding is
	/// required.
	/// </para>
	/// <para>
	/// A plain connection-liveness check (<c>SELECT 1</c>) is insufficient: across a failover or a
	/// reconnect, the connection can answer queries on a <em>new</em> backend that never re-acquired the
	/// advisory lock. Liveness would falsely report continued ownership while another node acquires the
	/// lock — a silent split-brain. The new backend holds no advisory lock, so this probe correctly
	/// returns <see langword="false"/> and leadership is relinquished.
	/// </para>
	/// <para>
	/// <b>Structural guarantee (bd-zg4zga, Npgsql):</b> unlike <c>Microsoft.Data.SqlClient</c>, Npgsql has no
	/// transparent connection-resiliency, and this lock connection disables pooling
	/// (<see cref="BuildLockConnectionString"/>). Consequently a lost backend session cannot resurface as a
	/// <em>live-but-not-owning</em> connection — it can only surface as a broken connection, caught by the
	/// <c>State != Open</c> guard below (<c>session loss ⟹ State≠Open ⟹ leadership relinquished</c>). The
	/// "alive-but-not-owning" false-positive is therefore <em>structurally inexpressible</em> here, which is a
	/// stronger guarantee than a behavioral check; the <c>pg_locks</c> ownership probe is the honest
	/// ownership verification (and defense-in-depth for any future reconnect-capable configuration).
	/// </para>
	/// <para>
	/// This addresses the reachable failover/pool-reset split-brain. The orthogonal paused/stalled-leader
	/// split-brain (which no connectivity check can catch) is mitigated by fencing tokens, tracked
	/// separately as <c>umemwa</c>.
	/// </para>
	/// </remarks>
	private async Task<bool> VerifyLockAsync(CancellationToken cancellationToken)
	{
		if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
		{
			return false;
		}

		try
		{
			await using var command = new NpgsqlCommand(
				"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid() AND granted)",
				_connection)
			{
				CommandTimeout = _pgOptions.CommandTimeoutSeconds
			};

			var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return result is true;
		}
		catch
		{
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
			Interlocked.Exchange(ref _lastSuccessfulRenewalTicks, DateTimeOffset.UtcNow.UtcTicks);
		}

		var resource = _pgOptions.LockKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
		LogBecameLeader(CandidateId, resource);

		BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, resource));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, CandidateId, resource));
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

		var resource = _pgOptions.LockKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
		LogLostLeadership(CandidateId, resource);

		// Raise consumer event handlers OUTSIDE the lock to avoid reentrancy/deadlock; the
		// snapshot taken under the lock keeps the event args consistent (no torn read).
		LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, resource));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, resource));

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

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionStarted, LogLevel.Information,
		"Starting Postgres leader election for candidate {CandidateId} on lock key {LockKey}")]
	private partial void LogStarting(string candidateId, long lockKey);

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionStopped, LogLevel.Information,
		"Stopping Postgres leader election for candidate {CandidateId}")]
	private partial void LogStopping(string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LockAcquisitionFailed, LogLevel.Debug,
		"Failed to acquire advisory lock for {CandidateId}, lock key: {LockKey}")]
	private partial void LogLockAcquisitionFailed(string candidateId, long lockKey);

	[LoggerMessage(LeaderElectionPostgresEventId.LockAcquisitionError, LogLevel.Error,
		"Error acquiring advisory lock for {CandidateId}")]
	private partial void LogLockAcquisitionError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LockReleased, LogLevel.Debug,
		"Released advisory lock for {CandidateId}")]
	private partial void LogLockReleased(string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LockReleaseError, LogLevel.Warning,
		"Error releasing advisory lock for {CandidateId}")]
	private partial void LogLockReleaseError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionError, LogLevel.Error,
		"Error in renewal loop for {CandidateId}")]
	private partial void LogRenewalError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.BecameLeader, LogLevel.Information,
		"Candidate {CandidateId} became leader for resource {Resource}")]
	private partial void LogBecameLeader(string candidateId, string resource);

	[LoggerMessage(LeaderElectionPostgresEventId.LostLeadership, LogLevel.Warning,
		"Candidate {CandidateId} lost leadership for resource {Resource}")]
	private partial void LogLostLeadership(string candidateId, string resource);

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionDisposeError, LogLevel.Warning,
		"Error during leader election dispose for {CandidateId}")]
	private partial void LogDisposeError(Exception ex, string candidateId);
}
