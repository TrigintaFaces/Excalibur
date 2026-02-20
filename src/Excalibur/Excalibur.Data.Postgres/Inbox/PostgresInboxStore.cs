// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Dapper;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.Inbox;

/// <summary>
/// Postgres implementation of <see cref="IInboxStore"/> for idempotent message processing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides reliable message deduplication and processing tracking using Postgres.
/// Messages are keyed by a composite of (MessageId, HandlerType), allowing the same message to be
/// processed independently by multiple handlers.
/// </para>
/// <para>
/// The <see cref="TryMarkAsProcessedAsync"/> method provides atomic "first writer wins" semantics
/// using Postgres's INSERT ... ON CONFLICT DO NOTHING for proper isolation.
/// </para>
/// </remarks>
public sealed class PostgresInboxStore : IInboxStore
{
	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly PostgresInboxOptions _options;
	private readonly ILogger<PostgresInboxStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresInboxStore(
		IOptions<PostgresInboxOptions> options,
		ILogger<PostgresInboxStore> logger)
		: this(CreateConnectionFactory(options?.Value), options?.Value, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresInboxStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="NpgsqlConnection"/> instances.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresInboxStore(
		Func<NpgsqlConnection> connectionFactory,
		PostgresInboxOptions options,
		ILogger<PostgresInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_options = options;
		_logger = logger;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);

		var sql = $"""
		           INSERT INTO {_options.QualifiedTableName}
		           	(message_id, handler_type, message_type, payload, metadata, received_at, status, retry_count, correlation_id, tenant_id, source)
		           VALUES
		           	(@MessageId, @HandlerType, @MessageType, @Payload, @Metadata::jsonb, @ReceivedAt, @Status, @RetryCount, @CorrelationId, @TenantId, @Source)
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				entry.MessageId,
				entry.HandlerType,
				entry.MessageType,
				entry.Payload,
				Metadata = SerializeMetadata(entry.Metadata),
				entry.ReceivedAt,
				Status = (int)entry.Status,
				entry.RetryCount,
				entry.CorrelationId,
				entry.TenantId,
				entry.Source
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
			_logger.LogDebug("Created inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return entry;
		}
		catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
		{
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.", ex);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = @ProcessedStatus, processed_at = @ProcessedAt, last_attempt_at = @ProcessedAt, last_error = NULL
		           WHERE message_id = @MessageId AND handler_type = @HandlerType AND status != @ProcessedStatus
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessedStatus = (int)InboxStatus.Processed,
				ProcessedAt = DateTimeOffset.UtcNow
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (affected == 0)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found or already processed for message '{messageId}' and handler '{handlerType}'.");
		}

		_logger.LogDebug("Marked inbox entry as processed for message {MessageId} and handler {HandlerType}", messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// Atomic "first writer wins" using INSERT ... ON CONFLICT DO NOTHING
		// Returns true if row was inserted (first processor), false if conflict (duplicate)
		var sql = $$"""
		            INSERT INTO {{_options.QualifiedTableName}}
		            	(message_id, handler_type, message_type, payload, metadata, received_at, processed_at, status, retry_count)
		            VALUES
		            	(@MessageId, @HandlerType, '', ''::bytea, '{}'::jsonb, @Now, @Now, @ProcessedStatus, 0)
		            ON CONFLICT (message_id, handler_type) DO NOTHING
		            """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				Now = DateTimeOffset.UtcNow,
				ProcessedStatus = (int)InboxStatus.Processed
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
		var isFirstProcessor = rowsAffected > 0;

		if (isFirstProcessor)
		{
			_logger.LogDebug("First processor for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Duplicate detected for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return isFirstProcessor;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var sql = $"""
		           SELECT EXISTS (
		           	SELECT 1 FROM {_options.QualifiedTableName}
		           	WHERE message_id = @MessageId AND handler_type = @HandlerType AND status = @ProcessedStatus
		           )
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await connection.QuerySingleAsync<bool>(command).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var sql = $"""
		           SELECT message_id, handler_type, message_type, payload, metadata, received_at, processed_at,
		           	   status, last_error, retry_count, last_attempt_at, correlation_id, tenant_id, source
		           FROM {_options.QualifiedTableName}
		           WHERE message_id = @MessageId AND handler_type = @HandlerType
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var row = await connection.QuerySingleOrDefaultAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return row != null ? MapRowToEntry(row) : null;
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = @FailedStatus, last_error = @LastError, retry_count = retry_count + 1, last_attempt_at = @LastAttemptAt
		           WHERE message_id = @MessageId AND handler_type = @HandlerType
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				FailedStatus = (int)InboxStatus.Failed,
				LastError = errorMessage,
				LastAttemptAt = DateTimeOffset.UtcNow
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		           SELECT message_id, handler_type, message_type, payload, metadata, received_at, processed_at,
		           	   status, last_error, retry_count, last_attempt_at, correlation_id, tenant_id, source
		           FROM {_options.QualifiedTableName}
		           WHERE status = @FailedStatus
		           	AND retry_count < @MaxRetries
		           	AND (@OlderThan IS NULL OR last_attempt_at < @OlderThan)
		           ORDER BY retry_count ASC, last_attempt_at ASC
		           LIMIT @BatchSize
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { BatchSize = batchSize, FailedStatus = (int)InboxStatus.Failed, MaxRetries = maxRetries, OlderThan = olderThan },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return rows.Select(MapRowToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		var sql = $"""
		           SELECT message_id, handler_type, message_type, payload, metadata, received_at, processed_at,
		           	   status, last_error, retry_count, last_attempt_at, correlation_id, tenant_id, source
		           FROM {_options.QualifiedTableName}
		           ORDER BY received_at DESC
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return rows.Select(MapRowToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var sql = $"""
		           SELECT
		           	COUNT(*) AS "TotalEntries",
		           	SUM(CASE WHEN status = @ProcessedStatus THEN 1 ELSE 0 END) AS "ProcessedEntries",
		           	SUM(CASE WHEN status = @FailedStatus THEN 1 ELSE 0 END) AS "FailedEntries",
		           	SUM(CASE WHEN status = @ReceivedStatus OR status = @ProcessingStatus THEN 1 ELSE 0 END) AS "PendingEntries"
		           FROM {_options.QualifiedTableName}
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				ProcessedStatus = (int)InboxStatus.Processed,
				FailedStatus = (int)InboxStatus.Failed,
				ReceivedStatus = (int)InboxStatus.Received,
				ProcessingStatus = (int)InboxStatus.Processing
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await connection.QuerySingleAsync<InboxStatistics>(command).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		var cutoffDate = DateTimeOffset.UtcNow - retentionPeriod;

		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE status = @ProcessedStatus AND processed_at < @CutoffDate
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { ProcessedStatus = (int)InboxStatus.Processed, CutoffDate = cutoffDate },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var deleted = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogInformation("Cleaned up {Count} processed inbox entries older than {CutoffDate}", deleted, cutoffDate);

		return deleted;
	}

	private static Func<NpgsqlConnection> CreateConnectionFactory(PostgresInboxOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return () => new NpgsqlConnection(options.ConnectionString);
	}

	#region Private Methods

	private string SerializeMetadata(IDictionary<string, object> metadata)
	{
		return JsonSerializer.Serialize(metadata, _jsonOptions);
	}

	private InboxEntry MapRowToEntry(InboxEntryRow row)
	{
		return new InboxEntry
		{
			MessageId = row.MessageId,
			HandlerType = row.HandlerType,
			MessageType = row.MessageType,
			Payload = row.Payload,
			ReceivedAt = row.ReceivedAt,
			ProcessedAt = row.ProcessedAt,
			Status = (InboxStatus)row.Status,
			LastError = row.LastError,
			RetryCount = row.RetryCount,
			LastAttemptAt = row.LastAttemptAt,
			CorrelationId = row.CorrelationId,
			TenantId = row.TenantId,
			Source = row.Source
		};
	}

	#endregion Private Methods

	#region Row Types

	private sealed class InboxEntryRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public string message_id { get; set; } = string.Empty;

		public string handler_type { get; set; } = string.Empty;
		public string message_type { get; set; } = string.Empty;
		public byte[] payload { get; set; } = [];
		public string? metadata { get; set; }
		public DateTimeOffset received_at { get; set; }
		public DateTimeOffset? processed_at { get; set; }
		public int status { get; set; }
		public string? last_error { get; set; }
		public int retry_count { get; set; }
		public DateTimeOffset? last_attempt_at { get; set; }
		public string? correlation_id { get; set; }
		public string? tenant_id { get; set; }
		public string? source { get; set; }
		// ReSharper restore InconsistentNaming

		// Map snake_case columns to PascalCase properties for use in code
		public string MessageId => message_id;

		public string HandlerType => handler_type;
		public string MessageType => message_type;
		public byte[] Payload => payload;
		public string? Metadata => metadata;
		public DateTimeOffset ReceivedAt => received_at;
		public DateTimeOffset? ProcessedAt => processed_at;
		public int Status => status;
		public string? LastError => last_error;
		public int RetryCount => retry_count;
		public DateTimeOffset? LastAttemptAt => last_attempt_at;
		public string? CorrelationId => correlation_id;
		public string? TenantId => tenant_id;
		public string? Source => source;
	}

	#endregion Row Types
}
