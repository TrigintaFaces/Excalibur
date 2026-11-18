// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Transactions;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
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

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "HealthElectionLifetime owns the inner DisposeAsync delegate and joins its memoized result before completing wrapper disposal.")]
	private readonly ILeaderElection _inner;
    private readonly IHealthElectionStore? _store;
	private readonly PostgresLeaderElectionOptions _pgOptions;
	private readonly PostgresHealthBasedLeaderElectionOptions _healthOptions;
	private readonly ILogger<PostgresHealthBasedLeaderElection> _logger;
	private readonly Lock _lifetimeGate = new();
    private HealthElectionLifetime? _lifetime;
    private int _metadataWriteAttempted;
    private HealthElectionLifetime Lifetime
    {
        get
        {
            lock (_lifetimeGate)
            {
                return _lifetime ??= new HealthElectionLifetime(StartCoreAsync, _inner.StopAsync,
                    _inner.DisposeAsync, RemoveOwnedHealthAsync, _ddlGate.Dispose,
                    beforeStart: () => LeaderElectionNotifications.ThrowIfNotificationReentrant(_inner.NotificationsCompleted));
            }
        }
    }

    private async Task RemoveOwnedHealthAsync(CancellationToken token)
    {
        if (Volatile.Read(ref _metadataWriteAttempted) == 0) { return; }
        await RemoveHealthRecordAsync(CandidateId, token).ConfigureAwait(false);
        Volatile.Write(ref _metadataWriteAttempted, 0);
    }

    private async Task StartCoreAsync(CancellationToken token)
    {
        if (_healthOptions.AutoCreateTable)
        { await Lifetime.NativeAsync(EnsureTableCreatedAsync, token).ConfigureAwait(false); }
        token.ThrowIfCancellationRequested();
        await _inner.StartAsync(token).ConfigureAwait(false);
    }
	private bool _tableCreated;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "HealthElectionLifetime invokes the gate Dispose delegate after all admitted DDL and health work has drained, including failure paths.")]
    private readonly SemaphoreSlim _ddlGate = new(1, 1);

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresHealthBasedLeaderElection"/> class.
	/// </summary>
	/// <param name="pgOptions">The Postgres leader election options.</param>
	/// <param name="electionOptions">The leader election options.</param>
	/// <param name="healthOptions">The health-based leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="innerLogger">The logger for the inner leader election.</param>
	/// <param name="fencingTokenProvider">
	/// An optional <see cref="IFencingTokenIssuer"/> forwarded to the inner
	/// <see cref="PostgresLeaderElection"/> for fail-closed fencing-token issuance on acquisition. Defaults to
	/// <see langword="null"/> (no fencing).
	/// </param>
	public PostgresHealthBasedLeaderElection(
		IOptions<PostgresLeaderElectionOptions> pgOptions,
		IOptions<LeaderElectionOptions> electionOptions,
		IOptions<PostgresHealthBasedLeaderElectionOptions> healthOptions,
		ILogger<PostgresHealthBasedLeaderElection> logger,
		ILogger<PostgresLeaderElection> innerLogger,
        IFencingTokenIssuer? fencingTokenProvider = null)
        : this(pgOptions, electionOptions, healthOptions, logger, innerLogger, fencingTokenProvider, null) { }

    /// <summary>Initializes health-aware election with a shared classifier for the inner owner.</summary>
    /// <param name="pgOptions">Postgres session options.</param>
    /// <param name="electionOptions">Local timing options.</param>
    /// <param name="healthOptions">Health metadata options.</param>
    /// <param name="logger">The wrapper logger.</param>
    /// <param name="innerLogger">The inner election logger.</param>
    /// <param name="fencingTokenProvider">The optional fencing provider.</param>
    /// <param name="context">The optional failure-classification context.</param>
    public PostgresHealthBasedLeaderElection(IOptions<PostgresLeaderElectionOptions> pgOptions,
        IOptions<LeaderElectionOptions> electionOptions, IOptions<PostgresHealthBasedLeaderElectionOptions> healthOptions,
        ILogger<PostgresHealthBasedLeaderElection> logger, ILogger<PostgresLeaderElection> innerLogger,
        IFencingTokenIssuer? fencingTokenProvider, PostgresLeaderElectionContext? context)
    {
		ArgumentNullException.ThrowIfNull(pgOptions);
		ArgumentNullException.ThrowIfNull(electionOptions);
		ArgumentNullException.ThrowIfNull(healthOptions);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(innerLogger);

		_pgOptions = new PostgresLeaderElectionOptions
        {
            ConnectionString = pgOptions.Value.ConnectionString, LockKey = pgOptions.Value.LockKey,
            CommandTimeoutSeconds = pgOptions.Value.CommandTimeoutSeconds
        };
		_healthOptions = Snapshot(healthOptions.Value);
		_logger = logger;

		ValidateIdentifier(_healthOptions.SchemaName, nameof(_healthOptions.SchemaName));
		ValidateIdentifier(_healthOptions.TableName, nameof(_healthOptions.TableName));

		_inner = new PostgresLeaderElection(Options.Create(_pgOptions), electionOptions, innerLogger, fencingTokenProvider, null, context);
	}

    internal PostgresHealthBasedLeaderElection(IOptions<PostgresLeaderElectionOptions> pgOptions,
        IOptions<PostgresHealthBasedLeaderElectionOptions> healthOptions,
        ILogger<PostgresHealthBasedLeaderElection> logger, PostgresLeaderElection inner)
    {
        _pgOptions = new PostgresLeaderElectionOptions
        {
            ConnectionString = pgOptions.Value.ConnectionString, LockKey = pgOptions.Value.LockKey,
            CommandTimeoutSeconds = pgOptions.Value.CommandTimeoutSeconds
        };
        _healthOptions = Snapshot(healthOptions.Value);
        _logger = logger;
        ValidateIdentifier(_healthOptions.SchemaName, nameof(_healthOptions.SchemaName));
        ValidateIdentifier(_healthOptions.TableName, nameof(_healthOptions.TableName));
        _inner = inner;
    }

    internal PostgresHealthBasedLeaderElection(IOptions<PostgresLeaderElectionOptions> pgOptions,
        IOptions<PostgresHealthBasedLeaderElectionOptions> healthOptions,
        ILogger<PostgresHealthBasedLeaderElection> logger, ILeaderElection inner, IHealthElectionStore store)
    {
        _pgOptions = new PostgresLeaderElectionOptions { ConnectionString = pgOptions.Value.ConnectionString,
            LockKey = pgOptions.Value.LockKey, CommandTimeoutSeconds = pgOptions.Value.CommandTimeoutSeconds };
        _healthOptions = Snapshot(healthOptions.Value);
        _logger = logger; _inner = inner; _store = store;
        ValidateIdentifier(_healthOptions.SchemaName, nameof(_healthOptions.SchemaName));
        ValidateIdentifier(_healthOptions.TableName, nameof(_healthOptions.TableName));
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
	public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed
	{
		add => _inner.AcquisitionFailed += value;
		remove => _inner.AcquisitionFailed -= value;
	}

	/// <inheritdoc/>
	public string CandidateId => _inner.CandidateId;

	/// <inheritdoc/>
	public Task NotificationsCompleted => _inner.NotificationsCompleted;

	/// <inheritdoc/>
	public bool IsLeader => _inner.IsLeader;

	/// <inheritdoc/>
	public Leadership? CurrentLeadership => _inner.CurrentLeadership;

	/// <inheritdoc/>
	public string? CurrentLeaderId => _inner.CurrentLeaderId;

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Lifetime.StartAsync(cancellationToken).ConfigureAwait(false);
        LogStarted(CandidateId);
    }

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Lifetime.StopAsync(cancellationToken).ConfigureAwait(false);
        LogStopped(CandidateId);
    }

	/// <inheritdoc/>
	public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var copy = metadata is null ? null : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        var metadataJson = copy is null ? "{}" : JsonSerializer.Serialize(copy, JsonContext.IDictionaryStringString);
        var steppedDown = false;
        await Lifetime.MutateAsync(ct =>
        {
            Interlocked.Exchange(ref _metadataWriteAttempted, 1);
            return UpsertHealthRecordAsync(CandidateId, isHealthy, metadataJson, ct);
        }, async () =>
        {
            // Remains owned until this authority effect settles: an old update cannot stop a new run.
            if (!isHealthy && _healthOptions.StepDownWhenUnhealthy && _inner.IsLeader)
            {
                try { await _inner.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (ObjectDisposedException) when (Lifetime.DisposalRequested) { }
                steppedDown = true;
            }
        }, cancellationToken).ConfigureAwait(false);
        LogHealthUpdated(CandidateId, isHealthy);
        if (steppedDown) { LogSteppingDown(CandidateId); }
    }

	/// <inheritdoc/>
	public Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken) =>
        Lifetime.ReadAsync(GetCandidateHealthCoreAsync, cancellationToken);

    private async Task<IEnumerable<CandidateHealth>> GetCandidateHealthCoreAsync(CancellationToken cancellationToken)
	{
        if (_store is not null) { return await _store.ReadAsync(cancellationToken).ConfigureAwait(false); }

		var results = new List<CandidateHealth>();
		var qualifiedTableName = $"\"{_healthOptions.SchemaName}\".\"{_healthOptions.TableName}\"";
		// The expiry threshold is computed BY THE DATABASE, from the same clock that writes last_updated
		// (NOW(), and the column DEFAULT). Computing it here from the application clock compared a
		// DB-written timestamp against an app-computed one: any skew between the two hosts read a live
		// candidate as expired or a dead one as healthy. One clock makes that unexpressible rather than
		// merely tested against. This is why no TimeProvider appears here - the app clock is not the clock
		// this comparison is about.

		await using var connection = new NpgsqlConnection(_pgOptions.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Schema and table names are validated by SafeIdentifierRegex at construction time
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(
			$"SELECT candidate_id, is_healthy, health_score, last_updated, is_leader, metadata_json FROM {qualifiedTableName} WHERE last_updated >= NOW() - make_interval(secs => @ExpirationSeconds)",
			connection)
		{
			CommandTimeout = _healthOptions.CommandTimeoutSeconds
		};
#pragma warning restore CA2100

		command.Parameters.AddWithValue("ExpirationSeconds", (double)_healthOptions.HealthExpirationSeconds);

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
    public ValueTask DisposeAsync() => Lifetime.DisposeAsync();

    private async Task EnsureTableCreatedAsync(CancellationToken cancellationToken)
    {
        await _ddlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tableCreated) { return; }
            using var infrastructure = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
            await CreateTableCoreAsync(cancellationToken).ConfigureAwait(false);
            // Publication follows command and connection disposal, in the autonomous scope.
            _tableCreated = true;
        }
        finally { _ddlGate.Release(); }
    }

    private async Task CreateTableCoreAsync(CancellationToken cancellationToken)
    {
        if (_store is not null) { await _store.CreateAsync(cancellationToken).ConfigureAwait(false); return; }
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

    }

	private async Task UpsertHealthRecordAsync(string candidateId, bool isHealthy, string metadataJson, CancellationToken cancellationToken)
	{
        if (_store is not null) { await _store.UpsertAsync(candidateId, isHealthy, metadataJson, _inner.IsLeader, cancellationToken).ConfigureAwait(false); return; }
		var qualifiedTableName = $"\"{_healthOptions.SchemaName}\".\"{_healthOptions.TableName}\"";

		await using var connection = new NpgsqlConnection(_pgOptions.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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

		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task RemoveHealthRecordAsync(string candidateId, CancellationToken cancellationToken)
	{
        if (_store is not null) { await _store.RemoveAsync(candidateId, cancellationToken).ConfigureAwait(false); return; }
		var qualifiedTableName = $"\"{_healthOptions.SchemaName}\".\"{_healthOptions.TableName}\"";

		await using var connection = new NpgsqlConnection(_pgOptions.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

    private static PostgresHealthBasedLeaderElectionOptions Snapshot(PostgresHealthBasedLeaderElectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var snapshot = new PostgresHealthBasedLeaderElectionOptions
        {
            SchemaName = options.SchemaName, TableName = options.TableName,
            AutoCreateTable = options.AutoCreateTable, StepDownWhenUnhealthy = options.StepDownWhenUnhealthy,
            HealthExpirationSeconds = options.HealthExpirationSeconds, CommandTimeoutSeconds = options.CommandTimeoutSeconds
        };
        var validation = new PostgresHealthBasedLeaderElectionOptionsValidator().Validate(null, snapshot);
        if (validation.Failed) { throw new OptionsValidationException(Options.DefaultName, typeof(PostgresHealthBasedLeaderElectionOptions), validation.Failures); }
        return snapshot;
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
