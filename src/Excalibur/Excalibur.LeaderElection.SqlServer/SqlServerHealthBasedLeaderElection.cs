// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IHealthBasedLeaderElection"/> that extends
/// the standard leader election with health-aware candidate tracking.
/// </summary>
/// <remarks>
/// <para>
/// This implementation wraps <see cref="SqlServerLeaderElection"/> for the core lock
/// mechanism and adds a separate SQL Server table for storing candidate health data.
/// Health records are stored with timestamps and automatically cleaned up based on expiration.
/// </para>
/// <para>
/// When a leader reports unhealthy status and <c>StepDownWhenUnhealthy</c> is enabled,
/// the leader will voluntarily release the lock, allowing a healthy candidate to take over.
/// </para>
/// </remarks>
public sealed partial class SqlServerHealthBasedLeaderElection : IHealthBasedLeaderElection, IAsyncDisposable
{
	private static readonly LeaderElectionJsonContext JsonContext = LeaderElectionJsonContext.Default;

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SafeIdentifierRegex();

	private readonly SqlServerLeaderElection _inner;
	private readonly string _connectionString;
	private readonly SqlServerHealthBasedLeaderElectionOptions _healthOptions;
	private readonly ILogger<SqlServerHealthBasedLeaderElection> _logger;
	private volatile bool _disposed;
	private bool _tableCreated;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerHealthBasedLeaderElection"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The lock resource name for sp_getapplock.</param>
	/// <param name="electionOptions">The leader election options.</param>
	/// <param name="healthOptions">The health-based leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="innerLogger">The logger for the inner leader election.</param>
	public SqlServerHealthBasedLeaderElection(
		string connectionString,
		string lockResource,
		IOptions<LeaderElectionOptions> electionOptions,
		IOptions<SqlServerHealthBasedLeaderElectionOptions> healthOptions,
		ILogger<SqlServerHealthBasedLeaderElection> logger,
		ILogger<SqlServerLeaderElection> innerLogger)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(lockResource);
		ArgumentNullException.ThrowIfNull(electionOptions);
		ArgumentNullException.ThrowIfNull(healthOptions);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(innerLogger);

		_connectionString = connectionString;
		_healthOptions = healthOptions.Value;
		_logger = logger;

		ValidateIdentifier(_healthOptions.SchemaName, nameof(_healthOptions.SchemaName));
		ValidateIdentifier(_healthOptions.TableName, nameof(_healthOptions.TableName));

		_inner = new SqlServerLeaderElection(connectionString, lockResource, electionOptions, innerLogger);
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
	public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata)
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
		var qualifiedTableName = $"[{_healthOptions.SchemaName}].[{_healthOptions.TableName}]";
		var expirationThreshold = DateTimeOffset.UtcNow.AddSeconds(-_healthOptions.HealthExpirationSeconds);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(
			$"SELECT CandidateId, IsHealthy, HealthScore, LastUpdated, IsLeader, MetadataJson FROM {qualifiedTableName} WHERE LastUpdated >= @ExpirationThreshold",
			connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("@ExpirationThreshold", expirationThreshold);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var metadataJson = await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? "{}" : reader.GetString(5);
			var metadataDict = JsonSerializer.Deserialize(metadataJson, JsonContext.DictionaryStringString)
				?? new Dictionary<string, string>(StringComparer.Ordinal);

			results.Add(new CandidateHealth
			{
				CandidateId = reader.GetString(0),
				IsHealthy = reader.GetBoolean(1),
				HealthScore = reader.GetDouble(2),
				LastUpdated = reader.GetDateTimeOffset(3),
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

		var qualifiedTableName = $"[{_healthOptions.SchemaName}].[{_healthOptions.TableName}]";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = @SchemaName AND t.name = @TableName)
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = @SchemaName)
    BEGIN
        EXEC('CREATE SCHEMA [{_healthOptions.SchemaName}]');
    END;

    CREATE TABLE {qualifiedTableName} (
        CandidateId NVARCHAR(256) NOT NULL PRIMARY KEY,
        IsHealthy BIT NOT NULL DEFAULT 1,
        HealthScore FLOAT NOT NULL DEFAULT 1.0,
        LastUpdated DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        IsLeader BIT NOT NULL DEFAULT 0,
        MetadataJson NVARCHAR(MAX) NULL
    );
END;";

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("@SchemaName", _healthOptions.SchemaName);
		_ = command.Parameters.AddWithValue("@TableName", _healthOptions.TableName);

		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

		_tableCreated = true;
	}

	private async Task UpsertHealthRecordAsync(string candidateId, bool isHealthy, string metadataJson)
	{
		var qualifiedTableName = $"[{_healthOptions.SchemaName}].[{_healthOptions.TableName}]";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		var sql = $@"
MERGE {qualifiedTableName} AS target
USING (VALUES (@CandidateId, @IsHealthy, @HealthScore, SYSDATETIMEOFFSET(), @IsLeader, @MetadataJson))
    AS source (CandidateId, IsHealthy, HealthScore, LastUpdated, IsLeader, MetadataJson)
    ON target.CandidateId = source.CandidateId
WHEN MATCHED THEN
    UPDATE SET IsHealthy = source.IsHealthy, HealthScore = source.HealthScore, LastUpdated = source.LastUpdated, IsLeader = source.IsLeader, MetadataJson = source.MetadataJson
WHEN NOT MATCHED THEN
    INSERT (CandidateId, IsHealthy, HealthScore, LastUpdated, IsLeader, MetadataJson)
    VALUES (source.CandidateId, source.IsHealthy, source.HealthScore, source.LastUpdated, source.IsLeader, source.MetadataJson);";

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("@CandidateId", candidateId);
		_ = command.Parameters.AddWithValue("@IsHealthy", isHealthy);
		_ = command.Parameters.AddWithValue("@HealthScore", isHealthy ? 1.0 : 0.0);
		_ = command.Parameters.AddWithValue("@IsLeader", _inner.IsLeader);
		_ = command.Parameters.AddWithValue("@MetadataJson", metadataJson);

		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task RemoveHealthRecordAsync(string candidateId)
	{
		var qualifiedTableName = $"[{_healthOptions.SchemaName}].[{_healthOptions.TableName}]";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(
			$"DELETE FROM {qualifiedTableName} WHERE CandidateId = @CandidateId",
			connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("@CandidateId", candidateId);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private static void ValidateIdentifier(string value, string parameterName)
	{
		if (!SafeIdentifierRegex().IsMatch(value))
		{
			throw new ArgumentException($"Invalid SQL identifier: '{value}'. Only alphanumeric characters and underscores are allowed.", parameterName);
		}
	}

	[LoggerMessage(LeaderElectionEventId.SqlServerStarting + 100, LogLevel.Information,
		"Starting health-based leader election for candidate {CandidateId}")]
	private partial void LogStarted(string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerStopping + 100, LogLevel.Information,
		"Stopping health-based leader election for candidate {CandidateId}")]
	private partial void LogStopped(string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerBecameLeader + 100, LogLevel.Debug,
		"Updated health for candidate {CandidateId}: {IsHealthy}")]
	private partial void LogHealthUpdated(string candidateId, bool isHealthy);

	[LoggerMessage(LeaderElectionEventId.SqlServerLostLeadership + 100, LogLevel.Warning,
		"Leader {CandidateId} stepping down due to unhealthy status")]
	private partial void LogSteppingDown(string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockReleaseError + 100, LogLevel.Warning,
		"Error removing health record for candidate {CandidateId}")]
	private partial void LogHealthRemovalError(Exception ex, string candidateId);
}
