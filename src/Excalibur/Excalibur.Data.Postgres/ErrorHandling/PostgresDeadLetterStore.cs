// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Dapper;

using Excalibur.Data.Postgres.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.ErrorHandling;

/// <summary>
/// Postgres implementation of the dead letter store.
/// Uses IOptions pattern for configuration consistency with other Postgres stores.
/// </summary>
public sealed partial class PostgresDeadLetterStore : IDeadLetterStore, IDeadLetterStoreAdmin
{
	private readonly string _connectionString;
	private readonly ILogger<PostgresDeadLetterStore> _logger;
	private readonly string _schema;
	private readonly string _tableName;
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresDeadLetterStore" /> class.
	/// </summary>
	/// <param name="options"> The Postgres dead letter options. </param>
	/// <param name="tenantContext"> The ambient tenant, or <see langword="null" /> in a single-tenant host. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	public PostgresDeadLetterStore(
		IOptions<PostgresDeadLetterOptions> options,
		ITenantContext? tenantContext,
		ILogger<PostgresDeadLetterStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		var opts = options.Value;
		ArgumentException.ThrowIfNullOrWhiteSpace(opts.ConnectionString);

		_connectionString = opts.ConnectionString;
		_schema = opts.SchemaName;
		_tableName = opts.TableName;

		// Optional by construction: a single-tenant host registers no ITenantContext, which resolves to the
		// untenanted partition. That partition still binds a concrete term, so no statement is ever emitted
		// without a tenant predicate.
		_tenantContext = tenantContext;
		_logger = logger;
	}

	/// <summary>
	/// Gets the tenant term bound by every statement this store emits: the ambient tenant, or the reserved
	/// untenanted sentinel when no tenant is resolved.
	/// </summary>
	/// <remarks>
	/// A dead-letter row holds the failed message body, so an unscoped read discloses one tenant's message
	/// content to another. Routed through <see cref="KeyedTenantPartition" />, which has no empty
	/// inhabitant, so the term is always concrete and a tenant-blind statement cannot arise by omission.
	/// </remarks>
	private string CurrentTenantTerm =>
		KeyedTenantPartition.FromScope(TenantScope.FromContext(_tenantContext)).TenantId;

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "JSON serialization used for properties storage; Dictionary<string, string> is well-defined and preserved")]
	[RequiresDynamicCode(
		"JSON serialization of message properties dictionary requires dynamic code generation for type-specific serialization logic.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	public async Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var sql = string.Format(
			CultureInfo.InvariantCulture,
			"""
			INSERT INTO "{0}"."{1}" (
			id, tenant_id, message_id, message_type, message_body, message_metadata,
			reason, exception_details, processing_attempts, moved_to_dead_letter_at,
			first_attempt_at, last_attempt_at, is_replayed, replayed_at,
			source_system, correlation_id, properties
			) VALUES (
			@Id, @TenantId, @MessageId, @MessageType, @MessageBody, @MessageMetadata,
			@Reason, @ExceptionDetails, @ProcessingAttempts, @MovedToDeadLetterAt,
			@FirstAttemptAt, @LastAttemptAt, @IsReplayed, @ReplayedAt,
			@SourceSystem, @CorrelationId, @Properties::jsonb
			)
			""",
			_schema,
			_tableName);

		using var connection = CreateConnection();
		_ = await connection.ExecuteAsync(
			sql,
			new
			{
				// Stamped from AMBIENT CONTEXT, never from the DTO: a caller-supplied tenant could name
				// someone else's, so the value that scopes the row is not under the caller's control.
				TenantId = CurrentTenantTerm,
				message.Id,
				message.MessageId,
				message.MessageType,
				message.MessageBody,
				message.MessageMetadata,
				message.Reason,
				message.ExceptionDetails,
				message.ProcessingAttempts,
				message.MovedToDeadLetterAt,
				message.FirstAttemptAt,
				message.LastAttemptAt,
				message.IsReplayed,
				message.ReplayedAt,
				message.SourceSystem,
				message.CorrelationId,
				Properties = JsonSerializer.Serialize(message.Properties),
			}).ConfigureAwait(false);

		LogStoredDeadLetterMessage(message.MessageId, message.MessageType, message.Reason);
	}
	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]

	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public async Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var sql = string.Format(
			CultureInfo.InvariantCulture,
			"""
			SELECT * FROM "{0}"."{1}"
			WHERE message_id = @MessageId AND tenant_id = @TenantId
			LIMIT 1
			""",
			_schema,
			_tableName);

		using var connection = CreateConnection();
		var result = await connection.QueryFirstOrDefaultAsync<DeadLetterMessageDto>(
			sql,
			new { MessageId = messageId, TenantId = CurrentTenantTerm }).ConfigureAwait(false);

		return result?.ToDeadLetterMessage();
	}

	/// <inheritdoc />
	public async Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
		DeadLetterFilter filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);

		// The tenant term is seeded as a MANDATORY clause rather than appended per-filter, so a filter that
		// specifies nothing still returns only the caller's own rows instead of the whole estate.
		var sql = $"""
			SELECT * FROM "{_schema}"."{_tableName}"
			WHERE tenant_id = @TenantId
			""";

		var parameters = new DynamicParameters();
		parameters.Add("TenantId", CurrentTenantTerm);

		if (!string.IsNullOrWhiteSpace(filter.MessageType))
		{
			sql += " AND message_type = @MessageType";
			parameters.Add("MessageType", filter.MessageType);
		}

		if (!string.IsNullOrWhiteSpace(filter.Reason))
		{
			sql += " AND reason ILIKE @Reason";
			parameters.Add("Reason", $"%{filter.Reason}%");
		}

		if (filter.FromDate.HasValue)
		{
			sql += " AND moved_to_dead_letter_at >= @FromDate";
			parameters.Add("FromDate", filter.FromDate.Value);
		}

		if (filter.ToDate.HasValue)
		{
			sql += " AND moved_to_dead_letter_at <= @ToDate";
			parameters.Add("ToDate", filter.ToDate.Value);
		}

		if (filter.IsReplayed.HasValue)
		{
			sql += " AND is_replayed = @IsReplayed";
			parameters.Add("IsReplayed", filter.IsReplayed.Value);
		}

		if (!string.IsNullOrWhiteSpace(filter.SourceSystem))
		{
			sql += " AND source_system = @SourceSystem";
			parameters.Add("SourceSystem", filter.SourceSystem);
		}

		if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
		{
			sql += " AND correlation_id = @CorrelationId";
			parameters.Add("CorrelationId", filter.CorrelationId);
		}

		sql += """
			ORDER BY moved_to_dead_letter_at DESC
			LIMIT @MaxResults
			OFFSET @Skip
			""";

		parameters.Add("Skip", filter.Skip);
		parameters.Add("MaxResults", filter.MaxResults);

		using var connection = CreateConnection();

#pragma warning disable IL2026, IL3050 // Dapper QueryAsync and ToDeadLetterMessage use reflection
		var results = await connection.QueryAsync<DeadLetterMessageDto>(sql, parameters).ConfigureAwait(false);

		return results.Select(static dto => dto.ToDeadLetterMessage());
#pragma warning restore IL2026, IL3050
	}

	/// <inheritdoc />
	public async Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var sql = string.Format(
			CultureInfo.InvariantCulture,
			"""
			UPDATE "{0}"."{1}"
			SET is_replayed = true, replayed_at = @ReplayedAt
			WHERE message_id = @MessageId AND tenant_id = @TenantId
			""",
			_schema,
			_tableName);

		using var connection = CreateConnection();
		var rowsAffected = await connection.ExecuteAsync(
			sql,
			new { MessageId = messageId, ReplayedAt = DateTimeOffset.UtcNow, TenantId = CurrentTenantTerm })
			.ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			LogMarkedDeadLetterMessageAsReplayed(messageId);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var sql = string.Format(
			CultureInfo.InvariantCulture,
			"""
			DELETE FROM "{0}"."{1}"
			WHERE message_id = @MessageId AND tenant_id = @TenantId
			""",
			_schema,
			_tableName);

		using var connection = CreateConnection();
		var rowsAffected = await connection.ExecuteAsync(
			sql,
			new { MessageId = messageId, TenantId = CurrentTenantTerm }).ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			LogDeletedDeadLetterMessage(messageId);
		}

		return rowsAffected > 0;
	}

	/// <inheritdoc />
	public async Task<long> GetCountAsync(CancellationToken cancellationToken)
	{
		var sql = string.Format(
			CultureInfo.InvariantCulture,
			// An admin count is still tenant-scoped: an estate-wide total tells one tenant how many failures
			// every other tenant has, which is an inference channel even though no message body is returned.
			"""SELECT COUNT(*) FROM "{0}"."{1}" WHERE tenant_id = @TenantId""",
			_schema,
			_tableName);

		using var connection = CreateConnection();
		return await connection.ExecuteScalarAsync<long>(sql, new { TenantId = CurrentTenantTerm })
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> CleanupOldMessagesAsync(int retentionDays, CancellationToken cancellationToken)
	{
		var sql = string.Format(
			CultureInfo.InvariantCulture,
			"""
			DELETE FROM "{0}"."{1}"
			WHERE moved_to_dead_letter_at < @CutoffDate AND tenant_id = @TenantId
			""",
			_schema,
			_tableName);

		using var connection = CreateConnection();
		var rowsAffected = await connection.ExecuteAsync(
			sql,
			new { CutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays), TenantId = CurrentTenantTerm })
			.ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			LogCleanedUpOldDeadLetterMessages(rowsAffected, retentionDays);
		}

		return rowsAffected;
	}

	/// <summary>
	/// Creates a new database connection.
	/// </summary>
	/// <returns>An open database connection.</returns>
	private IDbConnection CreateConnection()
	{
		var connection = new NpgsqlConnection(_connectionString);
		connection.Open();
		return connection;
	}

	// Source-generated logging methods
	[LoggerMessage(DataPostgresEventId.StoredDeadLetterMessage, LogLevel.Information,
		"Stored dead letter message {MessageId} of type {MessageType} with reason {Reason}")]
	private partial void LogStoredDeadLetterMessage(string messageId, string messageType, string reason);

	[LoggerMessage(DataPostgresEventId.MarkedDeadLetterMessageAsReplayed, LogLevel.Information,
		"Marked dead letter message {MessageId} as replayed")]
	private partial void LogMarkedDeadLetterMessageAsReplayed(string messageId);

	[LoggerMessage(DataPostgresEventId.DeletedDeadLetterMessage, LogLevel.Information,
		"Deleted dead letter message {MessageId}")]
	private partial void LogDeletedDeadLetterMessage(string messageId);

	[LoggerMessage(DataPostgresEventId.CleanedUpOldDeadLetterMessages, LogLevel.Information,
		"Cleaned up {RowsAffected} old dead letter messages with retention of {RetentionDays} days")]
	private partial void LogCleanedUpOldDeadLetterMessages(int rowsAffected, int retentionDays);

	/// <summary>
	/// DTO for mapping database results. Uses Postgres snake_case column naming convention.
	/// </summary>
	private sealed class DeadLetterMessageDto
	{
		// ReSharper disable InconsistentNaming - Postgres snake_case naming
		public string id { get; set; } = string.Empty;

		public string message_id { get; set; } = string.Empty;

		public string message_type { get; set; } = string.Empty;

		public string message_body { get; set; } = string.Empty;

		public string message_metadata { get; set; } = string.Empty;

		public string reason { get; set; } = string.Empty;

		public string? exception_details { get; set; }

		public int processing_attempts { get; set; }

		public DateTimeOffset moved_to_dead_letter_at { get; set; }

		public DateTimeOffset? first_attempt_at { get; set; }

		public DateTimeOffset? last_attempt_at { get; set; }

		public bool is_replayed { get; set; }

		public DateTimeOffset? replayed_at { get; set; }

		public string? source_system { get; set; }

		public string? correlation_id { get; set; }

		public string? properties { get; set; }
		// ReSharper restore InconsistentNaming

		[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "JSON deserialization used for properties retrieval; Dictionary<string, string> is well-defined and preserved")]
		[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
		public DeadLetterMessage ToDeadLetterMessage() =>
			new()
			{
				Id = id,
				MessageId = message_id,
				MessageType = message_type,
				MessageBody = message_body,
				MessageMetadata = message_metadata,
				Reason = reason,
				ExceptionDetails = exception_details,
				ProcessingAttempts = processing_attempts,
				MovedToDeadLetterAt = moved_to_dead_letter_at,
				FirstAttemptAt = first_attempt_at,
				LastAttemptAt = last_attempt_at,
				IsReplayed = is_replayed,
				ReplayedAt = replayed_at,
				SourceSystem = source_system,
				CorrelationId = correlation_id,
				Properties = string.IsNullOrWhiteSpace(properties)
					? []
					: JsonSerializer.Deserialize<Dictionary<string, string>>(properties) ?? [],
			};
	}
}
