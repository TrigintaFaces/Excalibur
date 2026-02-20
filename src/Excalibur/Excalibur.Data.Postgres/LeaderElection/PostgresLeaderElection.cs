// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Diagnostics;
using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.LeaderElection;

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

#if NET9_0_OR_GREATER
	private readonly Lock _lock = new();
#else
	private readonly object _lock = new();
#endif

	private NpgsqlConnection? _connection;
	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	private volatile bool _disposed;
	private DateTimeOffset _lastSuccessfulRenewal;

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

		CandidateId = _electionOptions.InstanceId ?? (Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8]);
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
				catch (OperationCanceledException)
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
			lock (_lock)
			{
				_isLeader = false;
				var previousLeader = _currentLeaderId;
				_currentLeaderId = null;

				LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _pgOptions.LockKey.ToString(System.Globalization.CultureInfo.InvariantCulture)));
				LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, _pgOptions.LockKey.ToString(System.Globalization.CultureInfo.InvariantCulture)));
			}
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
			_connection = new NpgsqlConnection(_pgOptions.ConnectionString);
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
						var elapsed = DateTimeOffset.UtcNow - _lastSuccessfulRenewal;
						if (elapsed > _electionOptions.GracePeriod)
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
					if (elapsed > _electionOptions.GracePeriod)
					{
						LoseLeadership();
					}
				}
			}
		}
	}

	/// <summary>
	/// Verifies that the Postgres connection holding the advisory lock is still alive.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Postgres advisory locks are session-scoped and released when the connection closes.
	/// Verifying the connection is alive is a reliable proxy for lock ownership.
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
			await using var command = new NpgsqlCommand("SELECT 1", _connection)
			{
				CommandTimeout = _pgOptions.CommandTimeoutSeconds
			};

			_ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return true;
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

		var resource = _pgOptions.LockKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
		LogBecameLeader(CandidateId, resource);

		BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, resource));
		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, CandidateId, resource));
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

			var resource = _pgOptions.LockKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
			LogLostLeadership(CandidateId, resource);

			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, resource));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeader, null, resource));
		}

		if (_connection != null)
		{
			try
			{
				_connection.Close();
			}
			catch
			{
				// Ignore connection close errors during leadership loss
			}

			_connection.Dispose();
			_connection = null;
		}
	}

	[LoggerMessage(DataPostgresEventId.LeaderElectionStarted, LogLevel.Information,
		"Starting Postgres leader election for candidate {CandidateId} on lock key {LockKey}")]
	private partial void LogStarting(string candidateId, long lockKey);

	[LoggerMessage(DataPostgresEventId.LeaderElectionStopped, LogLevel.Information,
		"Stopping Postgres leader election for candidate {CandidateId}")]
	private partial void LogStopping(string candidateId);

	[LoggerMessage(DataPostgresEventId.LockAcquisitionFailed, LogLevel.Debug,
		"Failed to acquire advisory lock for {CandidateId}, lock key: {LockKey}")]
	private partial void LogLockAcquisitionFailed(string candidateId, long lockKey);

	[LoggerMessage(DataPostgresEventId.LockAcquisitionError, LogLevel.Error,
		"Error acquiring advisory lock for {CandidateId}")]
	private partial void LogLockAcquisitionError(Exception ex, string candidateId);

	[LoggerMessage(DataPostgresEventId.LockReleased, LogLevel.Debug,
		"Released advisory lock for {CandidateId}")]
	private partial void LogLockReleased(string candidateId);

	[LoggerMessage(DataPostgresEventId.LockReleaseError, LogLevel.Warning,
		"Error releasing advisory lock for {CandidateId}")]
	private partial void LogLockReleaseError(Exception ex, string candidateId);

	[LoggerMessage(DataPostgresEventId.LeaderElectionError, LogLevel.Error,
		"Error in renewal loop for {CandidateId}")]
	private partial void LogRenewalError(Exception ex, string candidateId);

	[LoggerMessage(DataPostgresEventId.BecameLeader, LogLevel.Information,
		"Candidate {CandidateId} became leader for resource {Resource}")]
	private partial void LogBecameLeader(string candidateId, string resource);

	[LoggerMessage(DataPostgresEventId.LostLeadership, LogLevel.Warning,
		"Candidate {CandidateId} lost leadership for resource {Resource}")]
	private partial void LogLostLeadership(string candidateId, string resource);

	[LoggerMessage(DataPostgresEventId.LeaderElectionDisposeError, LogLevel.Warning,
		"Error during leader election dispose for {CandidateId}")]
	private partial void LogDisposeError(Exception ex, string candidateId);
}
