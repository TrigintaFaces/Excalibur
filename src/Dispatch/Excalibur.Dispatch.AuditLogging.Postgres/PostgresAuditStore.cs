// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Dispatch.AuditLogging.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IAuditStore"/> using Dapper and Npgsql.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides:
/// - Hash-chain integrity for tamper detection
/// - Retention policy enforcement
/// - Batch insert support for high-throughput scenarios
/// - Optimized indexes for compliance queries
/// </para>
/// </remarks>
public sealed partial class PostgresAuditStore : IAuditStore, IDisposable
{
	private readonly PostgresAuditOptions _options;
	private readonly ILogger<PostgresAuditStore> _logger;
	private readonly SemaphoreSlim _hashChainLock = new(1, 1);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresAuditStore"/> class.
	/// </summary>
	public PostgresAuditStore(
		IOptions<PostgresAuditOptions> options,
		ILogger<PostgresAuditStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrEmpty(_options.ConnectionString))
		{
			throw new ArgumentException("ConnectionString is required.", nameof(options));
		}

		ValidateSqlIdentifier(_options.SchemaName, nameof(PostgresAuditOptions.SchemaName));
		ValidateSqlIdentifier(_options.TableName, nameof(PostgresAuditOptions.TableName));
	}

	/// <inheritdoc />
	public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		string? previousHash = null;
		string eventHash;

		if (_options.EnableHashChain)
		{
			await _hashChainLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				var lastEvent = await GetLastEventAsync(auditEvent.TenantId, cancellationToken).ConfigureAwait(false);
				previousHash = lastEvent?.EventHash;
				eventHash = AuditHasher.ComputeHash(auditEvent, previousHash);
			}
			finally
			{
				_ = _hashChainLock.Release();
			}
		}
		else
		{
			eventHash = AuditHasher.ComputeHash(auditEvent, null);
		}

		var parameters = new DynamicParameters();
		parameters.Add("EventId", auditEvent.EventId);
		parameters.Add("EventType", (int)auditEvent.EventType);
		parameters.Add("Action", auditEvent.Action);
		parameters.Add("Outcome", (int)auditEvent.Outcome);
		parameters.Add("Timestamp", auditEvent.Timestamp);
		parameters.Add("ActorId", auditEvent.ActorId);
		parameters.Add("ActorType", auditEvent.ActorType);
		parameters.Add("ResourceId", auditEvent.ResourceId);
		parameters.Add("ResourceType", auditEvent.ResourceType);
		parameters.Add("ResourceClassification",
			auditEvent.ResourceClassification.HasValue ? (int)auditEvent.ResourceClassification.Value : null);
		parameters.Add("TenantId", auditEvent.TenantId);
		parameters.Add("CorrelationId", auditEvent.CorrelationId);
		parameters.Add("SessionId", auditEvent.SessionId);
		parameters.Add("IpAddress", auditEvent.IpAddress);
		parameters.Add("UserAgent", auditEvent.UserAgent);
		parameters.Add("Reason", auditEvent.Reason);
		parameters.Add("Metadata", auditEvent.Metadata is not null
			? JsonSerializer.Serialize(
				auditEvent.Metadata,
				PostgresAuditJsonContext.Default.IReadOnlyDictionaryStringString)
			: null);
		parameters.Add("PreviousEventHash", previousHash);
		parameters.Add("EventHash", eventHash);

		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
			(event_id, event_type, action, outcome, timestamp, actor_id, actor_type,
			 resource_id, resource_type, resource_classification, tenant_id, correlation_id,
			 session_id, ip_address, user_agent, reason, metadata, previous_event_hash, event_hash)
			VALUES
			(@EventId, @EventType, @Action, @Outcome, @Timestamp, @ActorId, @ActorType,
			 @ResourceId, @ResourceType, @ResourceClassification, @TenantId, @CorrelationId,
			 @SessionId, @IpAddress, @UserAgent, @Reason, @Metadata::jsonb, @PreviousEventHash, @EventHash)
			RETURNING sequence_number";

		var sequenceNumber = await connection.ExecuteScalarAsync<long>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogStoredAuditEvent(auditEvent.EventId, sequenceNumber);

		return new AuditEventId
		{
			EventId = auditEvent.EventId, SequenceNumber = sequenceNumber, EventHash = eventHash, RecordedAt = auditEvent.Timestamp
		};
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
				   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
				   resource_id AS ResourceId, resource_type AS ResourceType,
				   resource_classification AS ResourceClassification, tenant_id AS TenantId,
				   correlation_id AS CorrelationId, session_id AS SessionId,
				   ip_address AS IpAddress, user_agent AS UserAgent, reason AS Reason,
				   metadata AS Metadata, previous_event_hash AS PreviousEventHash, event_hash AS EventHash
			FROM {_options.FullyQualifiedTableName}
			WHERE event_id = @EventId";

		var row = await connection.QuerySingleOrDefaultAsync<AuditEventRow>(
				new CommandDefinition(sql, new { EventId = eventId }, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row is null ? null : MapToAuditEvent(row);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClauses, parameters) = BuildQueryClauses(query);
		var orderBy = query.OrderByDescending
			? "ORDER BY timestamp DESC, sequence_number DESC"
			: "ORDER BY timestamp ASC, sequence_number ASC";

		var sql = $@"
			SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
				   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
				   resource_id AS ResourceId, resource_type AS ResourceType,
				   resource_classification AS ResourceClassification, tenant_id AS TenantId,
				   correlation_id AS CorrelationId, session_id AS SessionId,
				   ip_address AS IpAddress, user_agent AS UserAgent, reason AS Reason,
				   metadata AS Metadata, previous_event_hash AS PreviousEventHash, event_hash AS EventHash
			FROM {_options.FullyQualifiedTableName}
			{(whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "")}
			{orderBy}
			LIMIT @MaxResults OFFSET @Skip";

		parameters.Add("Skip", query.Skip);
		parameters.Add("MaxResults", query.MaxResults);

		var rows = await connection.QueryAsync<AuditEventRow>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return rows.Select(MapToAuditEvent).ToList();
	}

	/// <inheritdoc />
	public async Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClauses, parameters) = BuildQueryClauses(query);

		var sql = $@"
			SELECT COUNT(*)
			FROM {_options.FullyQualifiedTableName}
			{(whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "")}";

		return await connection.ExecuteScalarAsync<long>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
				   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
				   resource_id AS ResourceId, resource_type AS ResourceType,
				   resource_classification AS ResourceClassification, tenant_id AS TenantId,
				   correlation_id AS CorrelationId, session_id AS SessionId,
				   ip_address AS IpAddress, user_agent AS UserAgent, reason AS Reason,
				   metadata AS Metadata, previous_event_hash AS PreviousEventHash, event_hash AS EventHash
			FROM {_options.FullyQualifiedTableName}
			WHERE timestamp >= @StartDate AND timestamp <= @EndDate
			ORDER BY sequence_number ASC";

		var rows = await connection.QueryAsync<AuditEventRow>(
				new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate }, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var events = rows.ToList();
		if (events.Count == 0)
		{
			return AuditIntegrityResult.Valid(0, startDate, endDate);
		}

		var violationCount = 0;
		string? firstViolationEventId = null;
		string? violationDescription = null;

		for (var i = 0; i < events.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var row = events[i];
			var auditEvent = MapToAuditEvent(row);
			var expectedHash = AuditHasher.ComputeHash(auditEvent, row.PreviousEventHash);

			if (row.EventHash != expectedHash)
			{
				violationCount++;
				firstViolationEventId ??= row.EventId;
				violationDescription ??= $"Hash mismatch for event {row.EventId}: expected {expectedHash}, found {row.EventHash}";

				LogIntegrityHashMismatch(row.EventId);
			}

			if (i > 0 && row.PreviousEventHash != events[i - 1].EventHash)
			{
				violationCount++;
				firstViolationEventId ??= row.EventId;
				violationDescription ??= $"Chain link broken at event {row.EventId}: previous hash mismatch";

				LogIntegrityChainBroken(row.EventId);
			}
		}

		if (violationCount > 0)
		{
			return AuditIntegrityResult.Invalid(
				events.Count,
				startDate,
				endDate,
				firstViolationEventId,
				violationDescription,
				violationCount);
		}

		LogIntegrityVerificationPassed(events.Count, startDate, endDate);

		return AuditIntegrityResult.Valid(events.Count, startDate, endDate);
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = tenantId is null
			? $@"
				SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
					   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
					   resource_id AS ResourceId, resource_type AS ResourceType,
					   resource_classification AS ResourceClassification, tenant_id AS TenantId,
					   correlation_id AS CorrelationId, session_id AS SessionId,
					   ip_address AS IpAddress, user_agent AS UserAgent, reason AS Reason,
					   metadata AS Metadata, previous_event_hash AS PreviousEventHash, event_hash AS EventHash
				FROM {_options.FullyQualifiedTableName}
				ORDER BY sequence_number DESC
				LIMIT 1"
			: $@"
				SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
					   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
					   resource_id AS ResourceId, resource_type AS ResourceType,
					   resource_classification AS ResourceClassification, tenant_id AS TenantId,
					   correlation_id AS CorrelationId, session_id AS SessionId,
					   ip_address AS IpAddress, user_agent AS UserAgent, reason AS Reason,
					   metadata AS Metadata, previous_event_hash AS PreviousEventHash, event_hash AS EventHash
				FROM {_options.FullyQualifiedTableName}
				WHERE tenant_id = @TenantId
				ORDER BY sequence_number DESC
				LIMIT 1";

		var row = await connection.QuerySingleOrDefaultAsync<AuditEventRow>(
				new CommandDefinition(sql, new { TenantId = tenantId }, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row is null ? null : MapToAuditEvent(row);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_hashChainLock.Dispose();
			_disposed = true;
		}
	}

	private static (List<string> WhereClauses, DynamicParameters Parameters) BuildQueryClauses(AuditQuery query)
	{
		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		if (query.StartDate.HasValue)
		{
			whereClauses.Add("timestamp >= @StartDate");
			parameters.Add("StartDate", query.StartDate.Value);
		}

		if (query.EndDate.HasValue)
		{
			whereClauses.Add("timestamp <= @EndDate");
			parameters.Add("EndDate", query.EndDate.Value);
		}

		if (query.EventTypes is { Count: > 0 })
		{
			whereClauses.Add("event_type = ANY(@EventTypes)");
			parameters.Add("EventTypes", query.EventTypes.Select(e => (int)e).ToArray());
		}

		if (query.Outcomes is { Count: > 0 })
		{
			whereClauses.Add("outcome = ANY(@Outcomes)");
			parameters.Add("Outcomes", query.Outcomes.Select(o => (int)o).ToArray());
		}

		if (!string.IsNullOrEmpty(query.ActorId))
		{
			whereClauses.Add("actor_id = @ActorId");
			parameters.Add("ActorId", query.ActorId);
		}

		if (!string.IsNullOrEmpty(query.ResourceId))
		{
			whereClauses.Add("resource_id = @ResourceId");
			parameters.Add("ResourceId", query.ResourceId);
		}

		if (!string.IsNullOrEmpty(query.ResourceType))
		{
			whereClauses.Add("resource_type = @ResourceType");
			parameters.Add("ResourceType", query.ResourceType);
		}

		if (query.MinimumClassification.HasValue)
		{
			whereClauses.Add("resource_classification >= @MinClassification");
			parameters.Add("MinClassification", (int)query.MinimumClassification.Value);
		}

		if (!string.IsNullOrEmpty(query.TenantId))
		{
			whereClauses.Add("tenant_id = @TenantId");
			parameters.Add("TenantId", query.TenantId);
		}

		if (!string.IsNullOrEmpty(query.CorrelationId))
		{
			whereClauses.Add("correlation_id = @CorrelationId");
			parameters.Add("CorrelationId", query.CorrelationId);
		}

		if (!string.IsNullOrEmpty(query.Action))
		{
			whereClauses.Add("action = @Action");
			parameters.Add("Action", query.Action);
		}

		if (!string.IsNullOrEmpty(query.IpAddress))
		{
			whereClauses.Add("ip_address = @IpAddress");
			parameters.Add("IpAddress", query.IpAddress);
		}

		return (whereClauses, parameters);
	}

	private static AuditEvent MapToAuditEvent(AuditEventRow row)
	{
		return new AuditEvent
		{
			EventId = row.EventId,
			EventType = (AuditEventType)row.EventType,
			Action = row.Action,
			Outcome = (AuditOutcome)row.Outcome,
			Timestamp = row.Timestamp,
			ActorId = row.ActorId,
			ActorType = row.ActorType,
			ResourceId = row.ResourceId,
			ResourceType = row.ResourceType,
			ResourceClassification = row.ResourceClassification.HasValue
				? (DataClassification)row.ResourceClassification.Value
				: null,
			TenantId = row.TenantId,
			CorrelationId = row.CorrelationId,
			SessionId = row.SessionId,
			IpAddress = row.IpAddress,
			UserAgent = row.UserAgent,
			Reason = row.Reason,
			Metadata = string.IsNullOrEmpty(row.Metadata)
				? null
				: JsonSerializer.Deserialize(
					row.Metadata,
					PostgresAuditJsonContext.Default.DictionaryStringString),
			PreviousEventHash = row.PreviousEventHash,
			EventHash = row.EventHash
		};
	}

	private static void ValidateSqlIdentifier(string identifier, string parameterName)
	{
		if (!SqlIdentifierRegex().IsMatch(identifier))
		{
			throw new ArgumentException(
				$"SQL identifier '{parameterName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.",
				parameterName);
		}
	}

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SqlIdentifierRegex();

	[LoggerMessage(LogLevel.Debug, "Stored audit event {EventId} with sequence {SequenceNumber}")]
	private partial void LogStoredAuditEvent(string eventId, long sequenceNumber);

	[LoggerMessage(LogLevel.Warning, "Integrity violation detected for event {EventId}: hash mismatch")]
	private partial void LogIntegrityHashMismatch(string eventId);

	[LoggerMessage(LogLevel.Warning, "Integrity violation detected for event {EventId}: chain link broken")]
	private partial void LogIntegrityChainBroken(string eventId);

	[LoggerMessage(LogLevel.Information,
		"Integrity verification passed for {EventCount} events from {StartDate} to {EndDate}")]
	private partial void LogIntegrityVerificationPassed(
		int eventCount,
		DateTimeOffset startDate,
		DateTimeOffset endDate);

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1812:Avoid uninstantiated internal classes",
		Justification = "Dapper materializes rows via reflection.")]
	private sealed class AuditEventRow
	{
		public string EventId { get; init; } = string.Empty;
		public int EventType { get; init; }
		public string Action { get; init; } = string.Empty;
		public int Outcome { get; init; }
		public DateTimeOffset Timestamp { get; init; }
		public string ActorId { get; init; } = string.Empty;
		public string? ActorType { get; init; }
		public string? ResourceId { get; init; }
		public string? ResourceType { get; init; }
		public int? ResourceClassification { get; init; }
		public string? TenantId { get; init; }
		public string? CorrelationId { get; init; }
		public string? SessionId { get; init; }
		public string? IpAddress { get; init; }
		public string? UserAgent { get; init; }
		public string? Reason { get; init; }
		public string? Metadata { get; init; }
		public string? PreviousEventHash { get; init; }
		public string? EventHash { get; init; }
	}
}
