// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IHealthBasedLeaderElection"/> that extends
/// the standard leader election with health-aware candidate tracking.
/// </summary>
/// <remarks>
/// <para>
/// This implementation wraps <see cref="PostgresLeaderElection"/> for the core lock
/// mechanism and adds a separate Postgres table for storing candidate health data.
/// Health records are stored with timestamps and automatically cleaned up based on expiration.
/// </para>
/// <para>
/// When a leader reports unhealthy status and <c>StepDownWhenUnhealthy</c> is enabled,
/// the leader will voluntarily release the lock, allowing a healthy candidate to take over.
/// </para>
/// </remarks>
public sealed partial class PostgresHealthBasedLeaderElection : IHealthBasedLeaderElection, IAsyncDisposable
{
	private static readonly PostgresLeaderElectionJsonContext JsonContext = PostgresLeaderElectionJsonContext.Default;

	[GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
	private static partial Regex SafeIdentifierRegex();

	private readonly PostgresLeaderElection _inner;
	private readonly PostgresLeaderElectionOptions _pgOptions;
	private readonly PostgresHealthBasedLeaderElectionOptions _healthOptions;
	private readonly ILogger<PostgresHealthBasedLeaderElection> _logger;
	private volatile bool _disposed;
	private bool _tableCreated;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresHealthBasedLeaderElection"/> class.
	/// </summary>
	/// <param name="pgOptions">The Postgres leader election options.</param>
	/// <param name="electionOptions">The leader election options.</param>
	/// <param name="healthOptions">The health-based leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="innerLogger">The logger for the inner leader election.</param>
	public PostgresHealthBasedLeaderElection(
		IOptions<PostgresLeaderElectionOptions> pgOptions,
		IOptions<LeaderElectionOptions> electionOptions,
		IOptions<PostgresHealthBasedLeaderElectionOptions> healthOptions,
		ILogger<PostgresHealthBasedLeaderElection> logger,
		ILogger<PostgresLeaderElection> innerLogger)
	{
		ArgumentNullException.ThrowIfNull(pgOptions);
		ArgumentNullException.ThrowIfNull(electionOptions);
		ArgumentNullException.ThrowIfNull(healthOptions);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(innerLogger);

		_pgOptions = pgOptions.Value;
		_healthOptions = healthOptions.Value;
		_logger = logger;

		ValidateIdentifier(_healthOptions.SchemaName, nameof(_healthOptions.SchemaName));
		ValidateIdentifier(_healthOptions.TableName, nameof(_healthOptions.TableName));

		_inner = new PostgresLeaderElection(pgOptions, electionOptions, innerLogger);
	}

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader
	{
		add => _inner.BecameLeader += value;
		remove => _inner.BecameLeader -= value;
	}

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership
	{
		add => _inner.LostLeadership += value;
		remove => _inner.LostLeadership -= value;
	}

	/// <inheritdoc/>
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged
	{
		add => _inner.LeaderChanged += value;
		remove => _inner.LeaderChanged -= value;
	}

	/// <inheritdoc/>
	public string CandidateId => _inner.CandidateId;

	/// <inheritdoc/>
	public bool IsLeader => _inner.IsLeader;

	/// <inheritdoc/>
	public string? CurrentLeaderId => _inner.CurrentLeaderId;

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_healthOptions.AutoCreateTable)
		{
			await EnsureTableCreatedAsync(cancellationToken).ConfigureAwait(false);
		}

		await _inner.StartAsync(cancellationToken).ConfigureAwait(false);

		LogStarted(CandidateId);
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _inner.StopAsync(cancellationToken).ConfigureAwait(false);

		// Remove health record on stop
		try
		{
			await RemoveHealthRecordAsync(CandidateId).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogHealthRemovalError(ex, CandidateId);
		}

		LogStopped(CandidateId);
	}

	/// <inheritdoc/>
	public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var metadataJson = metadata != null
			? JsonSerializer.Serialize(metadata, JsonContext.IDictionaryStringString)
			: "{}";

		await UpsertHealthRecordAsync(CandidateId, isHealthy, metadataJson).ConfigureAwait(false);

		LogHealthUpdated(CandidateId, isHealthy);

		// Step down if unhealthy and configured to do so
		if (!isHealthy && _healthOptions.StepDownWhenUnhealthy && _inner.IsLeader)
		{
			LogSteppingDown(CandidateId);
			await _inner.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<CandidateHealth>();
		var qualifiedTableName = $"\"{_healthOptions.SchemaName}\".\"{_healthOptions.TableName}\"";
		var expirationThreshold = DateTimeOffset.UtcNow.AddSeconds(-_healthOptions.HealthExpirationSeconds);

		await using var connection = new NpgsqlConnection(_pgOptions.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(
			$"SELECT candidate_id, is_healthy, health_score, last_updated, is_leader, metadata_json FROM {qualifiedTableName} WHERE last_updated >= @ExpirationThreshold",
			connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		command.Parameters.AddWithValue("ExpirationThreshold", expirationThreshold);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var metadataJson = await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? "{}" : reader.GetString(5);
			var metadataDict = JsonSerializer.Deserialize(metadataJson, JsonContext.DictionaryStringString)
				?? new Dictionary<string, string>(StringComparer.Ordinal);

			var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(3, cancellationToken).ConfigureAwait(false);

			results.Add(new CandidateHealth
			{
				CandidateId = reader.GetString(0),
				IsHealthy = reader.GetBoolean(1),
				HealthScore = reader.GetDouble(2),
				LastUpdated = lastUpdated,
				IsLeader = reader.GetBoolean(4),
				Metadata = metadataDict,
			});
		}

		return results;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		await _inner.DisposeAsync().ConfigureAwait(false);
	}

	private async Task EnsureTableCreatedAsync(CancellationToken cancellationToken)
	{
		if (_tableCreated)
		{
			return;
		}

		var qualifiedTableName = $"\"{_healthOptions.SchemaName}\".\"{_healthOptions.TableName}\"";

		await using var connection = new NpgsqlConnection(_pgOptions.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
CREATE SCHEMA IF NOT EXISTS ""{_healthOptions.SchemaName}"";

CREATE TABLE IF NOT EXISTS {qualifiedTableName} (
    candidate_id VARCHAR(256) NOT NULL PRIMARY KEY,
    is_healthy BOOLEAN NOT NULL DEFAULT TRUE,
    health_score DOUBLE PRECISION NOT NULL DEFAULT 1.0,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_leader BOOLEAN NOT NULL DEFAULT FALSE,
    metadata_json TEXT NULL
);";

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(sql, connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

		_tableCreated = true;
	}

	private async Task UpsertHealthRecordAsync(string candidateId, bool isHealthy, string metadataJson)
	{
		var qualifiedTableName = $"\"{_healthOptions.SchemaName}\".\"{_healthOptions.TableName}\"";

		await using var connection = new NpgsqlConnection(_pgOptions.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		var sql = $@"
INSERT INTO {qualifiedTableName} (candidate_id, is_healthy, health_score, last_updated, is_leader, metadata_json)
VALUES (@CandidateId, @IsHealthy, @HealthScore, NOW(), @IsLeader, @MetadataJson)
ON CONFLICT (candidate_id)
DO UPDATE SET is_healthy = @IsHealthy, health_score = @HealthScore, last_updated = NOW(), is_leader = @IsLeader, metadata_json = @MetadataJson;";

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(sql, connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		command.Parameters.AddWithValue("CandidateId", candidateId);
		command.Parameters.AddWithValue("IsHealthy", isHealthy);
		command.Parameters.AddWithValue("HealthScore", isHealthy ? 1.0 : 0.0);
		command.Parameters.AddWithValue("IsLeader", _inner.IsLeader);
		command.Parameters.AddWithValue("MetadataJson", metadataJson);

		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task RemoveHealthRecordAsync(string candidateId)
	{
		var qualifiedTableName = $"\"{_healthOptions.SchemaName}\".\"{_healthOptions.TableName}\"";

		await using var connection = new NpgsqlConnection(_pgOptions.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(
			$"DELETE FROM {qualifiedTableName} WHERE candidate_id = @CandidateId",
			connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		command.Parameters.AddWithValue("CandidateId", candidateId);
		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private static void ValidateIdentifier(string value, string parameterName)
	{
		if (!SafeIdentifierRegex().IsMatch(value))
		{
			throw new ArgumentException($"Invalid SQL identifier: '{value}'. Only alphanumeric characters, underscores, and leading letters/underscores are allowed.", parameterName);
		}
	}

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionStarted + 20, LogLevel.Information,
		"Starting health-based Postgres leader election for candidate {CandidateId}")]
	private partial void LogStarted(string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionStopped + 20, LogLevel.Information,
		"Stopping health-based Postgres leader election for candidate {CandidateId}")]
	private partial void LogStopped(string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.BecameLeader + 20, LogLevel.Debug,
		"Updated health for Postgres LE candidate {CandidateId}: {IsHealthy}")]
	private partial void LogHealthUpdated(string candidateId, bool isHealthy);

	[LoggerMessage(LeaderElectionPostgresEventId.LostLeadership + 20, LogLevel.Warning,
		"Postgres LE leader {CandidateId} stepping down due to unhealthy status")]
	private partial void LogSteppingDown(string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LockReleaseError + 20, LogLevel.Warning,
		"Error removing health record for Postgres LE candidate {CandidateId}")]
	private partial void LogHealthRemovalError(Exception ex, string candidateId);
}
