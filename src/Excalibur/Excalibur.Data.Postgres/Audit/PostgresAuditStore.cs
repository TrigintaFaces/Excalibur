// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Compliance;
using Excalibur.Data.Postgres.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.Audit;

/// <summary>
/// Postgres implementation of <see cref="IAuditStore"/> using JSONB for audit event data.
/// </summary>
/// <remarks>
/// <para>
/// Stores audit events with hash chain linking for tamper detection.
/// Uses Postgres's JSONB column type for efficient storage and querying of audit event metadata.
/// </para>
/// <para>
/// The hash chain is computed using SHA-256, linking each event to its predecessor
/// to provide a tamper-evident audit log as required by SOC2 compliance.
/// </para>
/// </remarks>
public sealed partial class PostgresAuditStore : IAuditStore, IDurableAuditStore, IAsyncDisposable
{
	private readonly PostgresAuditOptions _options;
	private readonly ITenantContext? _tenantContext;
	private readonly ILogger<PostgresAuditStore> _logger;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresAuditStore"/> class.
	/// </summary>
	/// <param name="options">The Postgres audit options.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Reads are
	/// scoped to the tenant this resolves — never to a tenant named by the caller's query.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	public PostgresAuditStore(
		IOptions<PostgresAuditOptions> options,
		ITenantContext? tenantContext,
		ILogger<PostgresAuditStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		_tenantContext = tenantContext;

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Adds the tenant scope to a query being built. This is the <strong>only</strong> place the tenant
	/// predicate is constructed, so a second read path cannot acquire a subtly different one.
	/// </summary>
	/// <param name="conditions">The clause list being built.</param>
	/// <param name="parameters">The parameter set being built.</param>
	/// <remarks>
	/// <c>COALESCE</c> is load-bearing: a bare <c>tenant_id = @TenantId</c> never matches a NULL-tenant row,
	/// so an untenanted caller would read nothing at all rather than its own rows. Every spelling of "no
	/// tenant" must name the same partition, and this is where they are reconciled.
	/// </remarks>
	private void AddTenantScope(List<string> conditions, DynamicParameters parameters)
	{
		conditions.Add("COALESCE(tenant_id, @UntenantedSentinel) = @TenantId");
		parameters.Add("UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);
		parameters.Add("TenantId", ResolveTenantTerm());
	}

	/// <summary>
	/// Resolves the tenant term every read is confined to — a <em>scope</em> from ambient context, never a
	/// filter supplied by the caller.
	/// </summary>
	/// <returns>The ambient tenant identifier, or the reserved untenanted sentinel.</returns>
	/// <exception cref="TenantRequiredException">Multi-tenancy is registered but resolves no tenant.</exception>
	private string ResolveTenantTerm()
	{
		var scope = TenantScope.FromContext(_tenantContext);

		return scope.IsScoped ? scope.TenantId! : KeyedTenantPartition.Untenanted.TenantId;
	}

	/// <inheritdoc/>
	public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(auditEvent);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Get the last event hash for chain linking
		var lastEventHash = await GetLastEventHashAsync(connection, auditEvent.TenantId, cancellationToken)
			.ConfigureAwait(false);

		// Compute the hash for this event
		var eventHash = ComputeEventHash(auditEvent, lastEventHash);

		var sql = $"""
			INSERT INTO {_options.SchemaName}.{_options.TableName}
			(event_id, event_type, action, outcome, timestamp, actor_id, actor_type,
			 resource_id, resource_type, tenant_id, correlation_id, session_id,
			 ip_address, user_agent, reason, metadata, previous_event_hash, event_hash)
			VALUES
			(@EventId, @EventType, @Action, @Outcome, @Timestamp, @ActorId, @ActorType,
			 @ResourceId, @ResourceType, @TenantId, @CorrelationId, @SessionId,
			 @IpAddress, @UserAgent, @Reason, @Metadata::jsonb, @PreviousEventHash, @EventHash)
			RETURNING sequence_number
			""";

#pragma warning disable IL2026, IL3050 // Serialization/reflection inherently not AOT-safe
		var metadataJson = auditEvent.Metadata != null ? JsonSerializer.Serialize(auditEvent.Metadata) : null;
#pragma warning restore IL2026, IL3050

		var sequenceNumber = await connection.ExecuteScalarAsync<long>(
			new CommandDefinition(sql, new
			{
				auditEvent.EventId,
				EventType = (int)auditEvent.EventType,
				auditEvent.Action,
				Outcome = (int)auditEvent.Outcome,
				auditEvent.Timestamp,
				auditEvent.ActorId,
				auditEvent.ActorType,
				auditEvent.ResourceId,
				auditEvent.ResourceType,
				// This store writes into the same audit.audit_events table the shipped audit schema creates,
				// where tenant_id is NOT NULL with the untenanted sentinel as its default. The raw nullable
				// therefore cannot be bound: an event captured with no ambient tenant carries a null by
				// design and would throw on insert. FromStoredValue is total over null, empty and whitespace,
				// so none of them can reach the column or persist a row no scoped predicate could match.
				TenantId = KeyedTenantPartition.FromStoredValue(auditEvent.TenantId).TenantId,
				auditEvent.CorrelationId,
				auditEvent.SessionId,
				auditEvent.IpAddress,
				auditEvent.UserAgent,
				auditEvent.Reason,
				Metadata = metadataJson,
				PreviousEventHash = lastEventHash,
				EventHash = eventHash
			}, cancellationToken: cancellationToken)).ConfigureAwait(false);

		LogAuditEventStored(auditEvent.EventId, auditEvent.Action);

		return new AuditEventId
		{
			EventId = auditEvent.EventId,
			EventHash = eventHash,
			SequenceNumber = sequenceNumber,
			RecordedAt = DateTimeOffset.UtcNow
		};
	}

	/// <inheritdoc/>
	public async Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// A lookup by primary key is still a tenant-scoped read. The identifier alone does not authorise the
		// row: without this predicate a caller holding an event id obtained from anywhere — a log line, an
		// export, a correlation trail — reads another tenant's audit record verbatim. Composed from the same
		// AddTenantScope every other read path uses, so the tenant term has exactly one definition per store.
		var conditions = new List<string> { "event_id = @EventId" };
		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);
		AddTenantScope(conditions, parameters);

		var sql = $"""
			SELECT * FROM {_options.SchemaName}.{_options.TableName}
			WHERE {string.Join(" AND ", conditions)}
			""";

		var row = await connection.QuerySingleOrDefaultAsync<AuditRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row?.ToAuditEvent();
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(query);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClause, parameters) = BuildWhereClause(query);

		var orderBy = query.OrderByDescending ? "ORDER BY timestamp DESC" : "ORDER BY timestamp ASC";

		var sql = $"""
			SELECT * FROM {_options.SchemaName}.{_options.TableName}
			{whereClause}
			{orderBy}
			OFFSET @Skip LIMIT @MaxResults
			""";

		parameters.Add("Skip", query.Skip);
		parameters.Add("MaxResults", query.MaxResults);

		var rows = await connection.QueryAsync<AuditRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogAuditQueryExecuted(query.MaxResults);

		return rows.Select(r => r.ToAuditEvent()).ToList().AsReadOnly();
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(query);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClause, parameters) = BuildWhereClause(query);

		var sql = $"""
			SELECT COUNT(*) FROM {_options.SchemaName}.{_options.TableName}
			{whereClause}
			""";

		return await connection.ExecuteScalarAsync<long>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $"""
			SELECT * FROM {_options.SchemaName}.{_options.TableName}
			WHERE timestamp >= @StartDate AND timestamp <= @EndDate
			ORDER BY sequence_number ASC
			""";

		var rows = await connection.QueryAsync<AuditRow>(
			new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var rowList = rows.ToList();
		long eventsVerified = 0;

		for (var i = 0; i < rowList.Count; i++)
		{
			eventsVerified++;
			var row = rowList[i];

			if (i > 0)
			{
				var previousRow = rowList[i - 1];
				if (row.previous_event_hash != previousRow.event_hash)
				{
					return AuditIntegrityResult.Invalid(
						eventsVerified, startDate, endDate,
						row.event_id,
						$"Hash chain broken at event {row.event_id}: expected previous hash '{previousRow.event_hash}' but found '{row.previous_event_hash}'");
				}
			}
		}

		LogAuditIntegrityVerified(eventsVerified);

		return AuditIntegrityResult.Valid(eventsVerified, startDate, endDate);
	}

	/// <inheritdoc/>
	public async Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = tenantId != null
			? $"""
				SELECT * FROM {_options.SchemaName}.{_options.TableName}
				WHERE tenant_id = @TenantId
				ORDER BY sequence_number DESC LIMIT 1
				"""
			: $"""
				SELECT * FROM {_options.SchemaName}.{_options.TableName}
				ORDER BY sequence_number DESC LIMIT 1
				""";

		var row = await connection.QuerySingleOrDefaultAsync<AuditRow>(
			new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row?.ToAuditEvent();
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		return ValueTask.CompletedTask;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_options.AutoCreateTable)
		{
			await CreateSchemaAndTableAsync(cancellationToken).ConfigureAwait(false);
		}

		_initialized = true;
		LogAuditStoreInitialized();
	}

	private async Task CreateSchemaAndTableAsync(CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $"""
			CREATE SCHEMA IF NOT EXISTS {_options.SchemaName};

			CREATE TABLE IF NOT EXISTS {_options.SchemaName}.{_options.TableName} (
				sequence_number BIGSERIAL PRIMARY KEY,
				event_id TEXT NOT NULL UNIQUE,
				event_type INTEGER NOT NULL,
				action TEXT NOT NULL,
				outcome INTEGER NOT NULL,
				timestamp TIMESTAMPTZ NOT NULL,
				actor_id TEXT NOT NULL,
				actor_type TEXT,
				resource_id TEXT,
				resource_type TEXT,
				tenant_id TEXT,
				correlation_id TEXT,
				session_id TEXT,
				ip_address TEXT,
				user_agent TEXT,
				reason TEXT,
				metadata JSONB,
				previous_event_hash TEXT,
				event_hash TEXT NOT NULL
			);

			CREATE INDEX IF NOT EXISTS idx_{_options.TableName}_timestamp
				ON {_options.SchemaName}.{_options.TableName} (timestamp);
			CREATE INDEX IF NOT EXISTS idx_{_options.TableName}_actor_id
				ON {_options.SchemaName}.{_options.TableName} (actor_id);
			CREATE INDEX IF NOT EXISTS idx_{_options.TableName}_tenant_id
				ON {_options.SchemaName}.{_options.TableName} (tenant_id);
			CREATE INDEX IF NOT EXISTS idx_{_options.TableName}_correlation_id
				ON {_options.SchemaName}.{_options.TableName} (correlation_id);
			""";

		_ = await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	private async Task<string?> GetLastEventHashAsync(NpgsqlConnection connection, string? tenantId, CancellationToken cancellationToken)
	{
		var sql = tenantId != null
			? $"SELECT event_hash FROM {_options.SchemaName}.{_options.TableName} WHERE tenant_id = @TenantId ORDER BY sequence_number DESC LIMIT 1"
			: $"SELECT event_hash FROM {_options.SchemaName}.{_options.TableName} ORDER BY sequence_number DESC LIMIT 1";

		return await connection.ExecuteScalarAsync<string?>(
			new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	private static string ComputeEventHash(AuditEvent auditEvent, string? previousHash)
	{
		var data = $"{auditEvent.EventId}|{auditEvent.Action}|{auditEvent.ActorId}|{auditEvent.Timestamp:O}|{previousHash ?? ""}";
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
		return Convert.ToHexString(hashBytes).ToUpperInvariant();
	}

	private (string whereClause, DynamicParameters parameters) BuildWhereClause(AuditQuery query)
	{
		var conditions = new List<string>();
		var parameters = new DynamicParameters();

		if (query.StartDate.HasValue)
		{
			conditions.Add("timestamp >= @StartDate");
			parameters.Add("StartDate", query.StartDate.Value);
		}

		if (query.EndDate.HasValue)
		{
			conditions.Add("timestamp <= @EndDate");
			parameters.Add("EndDate", query.EndDate.Value);
		}

		if (!string.IsNullOrWhiteSpace(query.ActorId))
		{
			conditions.Add("actor_id = @ActorId");
			parameters.Add("ActorId", query.ActorId);
		}

		if (!string.IsNullOrWhiteSpace(query.ResourceId))
		{
			conditions.Add("resource_id = @ResourceId");
			parameters.Add("ResourceId", query.ResourceId);
		}

		if (!string.IsNullOrWhiteSpace(query.ResourceType))
		{
			conditions.Add("resource_type = @ResourceType");
			parameters.Add("ResourceType", query.ResourceType);
		}

		// SECURITY: the tenant term is a SCOPE from ambient context, added unconditionally. query.TenantId
		// is deliberately not consulted — omitting it returned every tenant's audit events, and naming
		// another tenant returned theirs. This spelling used IsNullOrWhiteSpace, which is why a sweep for
		// IsNullOrEmpty reported this file clean.
		AddTenantScope(conditions, parameters);

		if (!string.IsNullOrWhiteSpace(query.CorrelationId))
		{
			conditions.Add("correlation_id = @CorrelationId");
			parameters.Add("CorrelationId", query.CorrelationId);
		}

		if (!string.IsNullOrWhiteSpace(query.Action))
		{
			conditions.Add("action = @Action");
			parameters.Add("Action", query.Action);
		}

		if (!string.IsNullOrWhiteSpace(query.IpAddress))
		{
			conditions.Add("ip_address = @IpAddress");
			parameters.Add("IpAddress", query.IpAddress);
		}

		var whereClause = conditions.Count > 0
			? "WHERE " + string.Join(" AND ", conditions)
			: string.Empty;

		return (whereClause, parameters);
	}

	[LoggerMessage(DataPostgresEventId.AuditEventStored, LogLevel.Debug,
		"Stored audit event {EventId} with action '{Action}'")]
	private partial void LogAuditEventStored(string eventId, string action);

	[LoggerMessage(DataPostgresEventId.AuditQueryExecuted, LogLevel.Debug,
		"Executed audit query with max results {MaxResults}")]
	private partial void LogAuditQueryExecuted(int maxResults);

	[LoggerMessage(DataPostgresEventId.AuditIntegrityVerified, LogLevel.Information,
		"Verified audit chain integrity for {EventsVerified} events")]
	private partial void LogAuditIntegrityVerified(long eventsVerified);

	[LoggerMessage(DataPostgresEventId.AuditStoreInitialized, LogLevel.Information,
		"Postgres audit store initialized")]
	private partial void LogAuditStoreInitialized();

	/// <summary>
	/// Internal row type for Dapper mapping.
	/// </summary>
#pragma warning disable IDE1006 // Naming Styles - Dapper column mapping requires lowercase
	internal sealed class AuditRow
	{
		public long sequence_number { get; set; }
		public string event_id { get; set; } = string.Empty;
		public int event_type { get; set; }
		public string action { get; set; } = string.Empty;
		public int outcome { get; set; }
		public DateTimeOffset timestamp { get; set; }
		public string actor_id { get; set; } = string.Empty;
		public string? actor_type { get; set; }
		public string? resource_id { get; set; }
		public string? resource_type { get; set; }
		public string? tenant_id { get; set; }
		public string? correlation_id { get; set; }
		public string? session_id { get; set; }
		public string? ip_address { get; set; }
		public string? user_agent { get; set; }
		public string? reason { get; set; }
		public string? metadata { get; set; }
		public string? previous_event_hash { get; set; }
		public string event_hash { get; set; } = string.Empty;

		public AuditEvent ToAuditEvent()
		{
			IReadOnlyDictionary<string, string>? metadataDict = null;
			if (!string.IsNullOrEmpty(metadata))
			{
#pragma warning disable IL2026, IL3050 // Serialization/reflection inherently not AOT-safe
				metadataDict = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
#pragma warning restore IL2026, IL3050
			}

			return new AuditEvent
			{
				EventId = event_id,
				EventType = (AuditEventType)event_type,
				Action = action,
				Outcome = (AuditOutcome)outcome,
				Timestamp = timestamp,
				ActorId = actor_id,
				ActorType = actor_type,
				ResourceId = resource_id,
				ResourceType = resource_type,
				TenantId = tenant_id,
				CorrelationId = correlation_id,
				SessionId = session_id,
				IpAddress = ip_address,
				UserAgent = user_agent,
				Reason = reason,
				Metadata = metadataDict,
				PreviousEventHash = previous_event_hash,
				EventHash = event_hash
			};
		}
	}
#pragma warning restore IDE1006
}
