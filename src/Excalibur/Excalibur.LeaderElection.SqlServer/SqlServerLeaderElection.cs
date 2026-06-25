// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;
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
	private readonly Lock _lock = new();

	private SqlConnection? _connection;
	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	private int _disposed;
	private DateTimeOffset _lastSuccessfulRenewal;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerLeaderElection"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
	/// <param name="options">The leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerLeaderElection(
		string connectionString,
		string lockResource,
		IOptions<LeaderElectionOptions> options,
		ILogger<SqlServerLeaderElection> logger)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		_connectionString = BuildLockConnectionString(connectionString);
		_lockResource = lockResource ?? throw new ArgumentNullException(nameof(lockResource));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					// Expected
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

	/// <summary>
	/// Disposes the leader election resources asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		if (_isStarted)
		{
			try
			{
				await StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Safe cleanup during disposal
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
				// Lock acquired
				BecomeLeader();
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
					// Verify we still hold the lock
					var stillLeader = await VerifyLockAsync(cancellationToken).ConfigureAwait(false);
					if (!stillLeader)
					{
						// Check if we've exceeded the grace period
						var elapsed = DateTimeOffset.UtcNow - _lastSuccessfulRenewal;
						if (elapsed > _options.GracePeriod)
						{
							await LoseLeadershipAsync().ConfigureAwait(false);
						}
					}
					else
					{
						_lastSuccessfulRenewal = DateTimeOffset.UtcNow;
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
					var elapsed = DateTimeOffset.UtcNow - _lastSuccessfulRenewal;
					if (elapsed > _options.GracePeriod)
					{
						await LoseLeadershipAsync().ConfigureAwait(false);
					}
				}
			}
		}
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
	/// <see langword="true"/> if the current session still holds the application lock;
	/// <see langword="false"/> if it does not, the connection is closed/broken, or the check fails.
	/// </returns>
	private async Task<bool> VerifyLockAsync(CancellationToken cancellationToken)
	{
		if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
		{
			return false;
		}

		try
		{
			await using var command = new SqlCommand("SELECT APPLOCK_MODE('public', @Resource, 'Session')", _connection);
			_ = command.Parameters.AddWithValue("@Resource", _lockResource);

			var mode = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
			return mode is not null && !string.Equals(mode, "NoLock", StringComparison.OrdinalIgnoreCase);
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
			_lastSuccessfulRenewal = DateTimeOffset.UtcNow;
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
}
