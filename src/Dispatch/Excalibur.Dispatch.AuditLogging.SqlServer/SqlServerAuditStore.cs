// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Dispatch.Compliance;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IAuditStore"/> using Dapper.
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
public sealed partial class SqlServerAuditStore : IAuditStore, IDisposable
{
	private readonly SqlServerAuditOptions _options;
	private readonly ILogger<SqlServerAuditStore> _logger;
	private readonly SemaphoreSlim _hashChainLock = new(1, 1);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerAuditStore"/> class.
	/// </summary>
	public SqlServerAuditStore(
		IOptions<SqlServerAuditOptions> options,
		ILogger<SqlServerAuditStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrEmpty(_options.ConnectionString))
		{
			throw new ArgumentException(Resources.SqlServerAuditStore_ConnectionStringRequired, nameof(options));
		}

		ValidateSqlIdentifier(_options.SchemaName, nameof(SqlServerAuditOptions.SchemaName));
		ValidateSqlIdentifier(_options.TableName, nameof(SqlServerAuditOptions.TableName));
	}

	/// <inheritdoc />
	public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		string? previousHash = null;
		string eventHash;

		if (_options.EnableHashChain)
		{
			await _hashChainLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				// Get the previous event's hash for chain linking
				var lastEvent = await GetLastEventAsync(auditEvent.TenantId, cancellationToken).ConfigureAwait(false);
				previousHash = lastEvent?.EventHash;

				// Compute hash for this event
				eventHash = ComputeEventHash(auditEvent, previousHash);
			}
			finally
			{
				_ = _hashChainLock.Release();
			}
		}
		else
		{
			eventHash = ComputeEventHash(auditEvent, null);
		}

		var parameters = new DynamicParameters();
		parameters.Add("@EventId", auditEvent.EventId);
		parameters.Add("@EventType", (int)auditEvent.EventType);
		parameters.Add("@Action", auditEvent.Action);
		parameters.Add("@Outcome", (int)auditEvent.Outcome);
		parameters.Add("@Timestamp", auditEvent.Timestamp);
		parameters.Add("@ActorId", auditEvent.ActorId);
		parameters.Add("@ActorType", auditEvent.ActorType);
		parameters.Add("@ResourceId", auditEvent.ResourceId);
		parameters.Add("@ResourceType", auditEvent.ResourceType);
		parameters.Add("@ResourceClassification",
			auditEvent.ResourceClassification.HasValue ? (int)auditEvent.ResourceClassification.Value : null);
		parameters.Add("@TenantId", auditEvent.TenantId);
		parameters.Add("@CorrelationId", auditEvent.CorrelationId);
		parameters.Add("@SessionId", auditEvent.SessionId);
		parameters.Add("@IpAddress", auditEvent.IpAddress);
		parameters.Add("@UserAgent", auditEvent.UserAgent);
		parameters.Add("@Reason", auditEvent.Reason);
		parameters.Add("@Metadata", auditEvent.Metadata is not null
			? JsonSerializer.Serialize(
				auditEvent.Metadata,
				SqlServerAuditJsonContext.Default.IReadOnlyDictionaryStringString)
			: null);
		parameters.Add("@PreviousEventHash", previousHash);
		parameters.Add("@EventHash", eventHash);
		parameters.Add("@SequenceNumber", dbType: DbType.Int64, direction: ParameterDirection.Output);

		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
			(EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
			 ResourceId, ResourceType, ResourceClassification, TenantId, CorrelationId,
			 SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash)
			OUTPUT INSERTED.SequenceNumber
			VALUES
			(@EventId, @EventType, @Action, @Outcome, @Timestamp, @ActorId, @ActorType,
			 @ResourceId, @ResourceType, @ResourceClassification, @TenantId, @CorrelationId,
			 @SessionId, @IpAddress, @UserAgent, @Reason, @Metadata, @PreviousEventHash, @EventHash)";

		var sequenceNumber = await connection.ExecuteScalarAsync<long>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogStoredAuditEvent(auditEvent.EventId, sequenceNumber);

		return new AuditEventId
		{
			EventId = auditEvent.EventId,
			SequenceNumber = sequenceNumber,
			EventHash = eventHash,
			RecordedAt = auditEvent.Timestamp
		};
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
				   ResourceId, ResourceType, ResourceClassification, TenantId, CorrelationId,
				   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
			FROM {_options.FullyQualifiedTableName}
			WHERE EventId = @EventId";

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

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClauses, parameters) = BuildQueryClauses(query);
		var orderBy = query.OrderByDescending
			? "ORDER BY [Timestamp] DESC, SequenceNumber DESC"
			: "ORDER BY [Timestamp] ASC, SequenceNumber ASC";

		var sql = $@"
			SELECT EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
				   ResourceId, ResourceType, ResourceClassification, TenantId, CorrelationId,
				   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
			FROM {_options.FullyQualifiedTableName}
			{(whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "")}
			{orderBy}
			OFFSET @Skip ROWS FETCH NEXT @MaxResults ROWS ONLY";

		parameters.Add("@Skip", query.Skip);
		parameters.Add("@MaxResults", query.MaxResults);

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

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
				   ResourceId, ResourceType, ResourceClassification, TenantId, CorrelationId,
				   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
			FROM {_options.FullyQualifiedTableName}
			WHERE [Timestamp] >= @StartDate AND [Timestamp] <= @EndDate
			ORDER BY SequenceNumber ASC";

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
			var expectedHash = ComputeEventHash(auditEvent, row.PreviousEventHash);

			if (row.EventHash != expectedHash)
			{
				violationCount++;
				firstViolationEventId ??= row.EventId;
				violationDescription ??= $"Hash mismatch for event {row.EventId}: expected {expectedHash}, found {row.EventHash}";

				LogIntegrityHashMismatch(row.EventId);
			}

			// Verify chain link (except for first event)
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
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = tenantId is null
			? $@"
				SELECT TOP 1 EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
					   ResourceId, ResourceType, ResourceClassification, TenantId, CorrelationId,
					   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
				FROM {_options.FullyQualifiedTableName}
				ORDER BY SequenceNumber DESC"
			: $@"
				SELECT TOP 1 EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
					   ResourceId, ResourceType, ResourceClassification, TenantId, CorrelationId,
					   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
				FROM {_options.FullyQualifiedTableName}
				WHERE TenantId = @TenantId
				ORDER BY SequenceNumber DESC";

		var row = await connection.QuerySingleOrDefaultAsync<AuditEventRow>(
				new CommandDefinition(sql, new { TenantId = tenantId }, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row is null ? null : MapToAuditEvent(row);
	}

	/// <summary>
	/// Stores multiple audit events in a batch.
	/// </summary>
	/// <param name="auditEvents">The audit events to store.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The IDs of the stored events.</returns>
	public async Task<IReadOnlyList<AuditEventId>> StoreBatchAsync(
		IEnumerable<AuditEvent> auditEvents,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvents);

		var results = new List<AuditEventId>();
		foreach (var auditEvent in auditEvents)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var id = await StoreAsync(auditEvent, cancellationToken).ConfigureAwait(false);
			results.Add(id);
		}

		return results;
	}

	/// <summary>
	/// Deletes audit events older than the retention period.
	/// </summary>
	/// <param name="cutoffDate">Events older than this date will be deleted.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of events deleted.</returns>
	public async Task<int> EnforceRetentionAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			DELETE TOP (@BatchSize) FROM {_options.FullyQualifiedTableName}
			WHERE [Timestamp] < @CutoffDate";

		var totalDeleted = 0;
		int deleted;

		do
		{
			deleted = await connection.ExecuteAsync(
					new CommandDefinition(
						sql,
						new { BatchSize = _options.RetentionCleanupBatchSize, CutoffDate = cutoffDate },
						commandTimeout: _options.CommandTimeoutSeconds,
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			totalDeleted += deleted;

			if (deleted > 0)
			{
				LogDeletedExpiredEvents(deleted, cutoffDate);
			}
		} while (deleted == _options.RetentionCleanupBatchSize);

		return totalDeleted;
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

	private static string ComputeEventHash(AuditEvent auditEvent, string? previousHash) =>
		AuditHasher.ComputeHash(auditEvent, previousHash);

	private static (List<string> WhereClauses, DynamicParameters Parameters) BuildQueryClauses(AuditQuery query)
	{
		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		if (query.StartDate.HasValue)
		{
			whereClauses.Add("[Timestamp] >= @StartDate");
			parameters.Add("@StartDate", query.StartDate.Value);
		}

		if (query.EndDate.HasValue)
		{
			whereClauses.Add("[Timestamp] <= @EndDate");
			parameters.Add("@EndDate", query.EndDate.Value);
		}

		if (query.EventTypes is { Count: > 0 })
		{
			whereClauses.Add("EventType IN @EventTypes");
			parameters.Add("@EventTypes", query.EventTypes.Select(e => (int)e).ToArray());
		}

		if (query.Outcomes is { Count: > 0 })
		{
			whereClauses.Add("Outcome IN @Outcomes");
			parameters.Add("@Outcomes", query.Outcomes.Select(o => (int)o).ToArray());
		}

		if (!string.IsNullOrEmpty(query.ActorId))
		{
			whereClauses.Add("ActorId = @ActorId");
			parameters.Add("@ActorId", query.ActorId);
		}

		if (!string.IsNullOrEmpty(query.ResourceId))
		{
			whereClauses.Add("ResourceId = @ResourceId");
			parameters.Add("@ResourceId", query.ResourceId);
		}

		if (!string.IsNullOrEmpty(query.ResourceType))
		{
			whereClauses.Add("ResourceType = @ResourceType");
			parameters.Add("@ResourceType", query.ResourceType);
		}

		if (query.MinimumClassification.HasValue)
		{
			whereClauses.Add("ResourceClassification >= @MinClassification");
			parameters.Add("@MinClassification", (int)query.MinimumClassification.Value);
		}

		if (!string.IsNullOrEmpty(query.TenantId))
		{
			whereClauses.Add("TenantId = @TenantId");
			parameters.Add("@TenantId", query.TenantId);
		}

		if (!string.IsNullOrEmpty(query.CorrelationId))
		{
			whereClauses.Add("CorrelationId = @CorrelationId");
			parameters.Add("@CorrelationId", query.CorrelationId);
		}

		if (!string.IsNullOrEmpty(query.Action))
		{
			whereClauses.Add("[Action] = @Action");
			parameters.Add("@Action", query.Action);
		}

		if (!string.IsNullOrEmpty(query.IpAddress))
		{
			whereClauses.Add("IpAddress = @IpAddress");
			parameters.Add("@IpAddress", query.IpAddress);
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
					SqlServerAuditJsonContext.Default.DictionaryStringString),
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

	[LoggerMessage(LogLevel.Information, "Deleted {Count} audit events older than {CutoffDate}")]
	private partial void LogDeletedExpiredEvents(int count, DateTimeOffset cutoffDate);

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
