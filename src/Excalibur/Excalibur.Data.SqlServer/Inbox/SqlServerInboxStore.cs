// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Dapper;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Inbox;

/// <summary>
/// SQL Server implementation of <see cref="IInboxStore"/> for idempotent message processing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides reliable message deduplication and processing tracking using SQL Server.
/// Messages are keyed by a composite of (MessageId, HandlerType), allowing the same message to be
/// processed independently by multiple handlers.
/// </para>
/// <para>
/// The <see cref="TryMarkAsProcessedAsync"/> method provides atomic "first writer wins" semantics
/// using SQL Server's MERGE statement with HOLDLOCK hint for proper isolation.
/// </para>
/// </remarks>
public sealed class SqlServerInboxStore : IInboxStore
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly SqlServerInboxOptions _options;
	private readonly ILogger<SqlServerInboxStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerInboxStore(
		IOptions<SqlServerInboxOptions> options,
		ILogger<SqlServerInboxStore> logger)
		: this(CreateConnectionFactory(options?.Value), options?.Value, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerInboxStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerInboxStore(
		Func<SqlConnection> connectionFactory,
		SqlServerInboxOptions options,
		ILogger<SqlServerInboxStore> logger)
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
		           	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, Status, RetryCount, CorrelationId, TenantId, Source)
		           VALUES
		           	(@MessageId, @HandlerType, @MessageType, @Payload, @Metadata, @ReceivedAt, @Status, @RetryCount, @CorrelationId, @TenantId, @Source)
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
		catch (SqlException ex) when (ex.Number is 2627 or 2601) // Unique constraint violation
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
		           SET Status = @ProcessedStatus, ProcessedAt = @ProcessedAt, LastAttemptAt = @ProcessedAt, LastError = NULL
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType AND Status != @ProcessedStatus
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

		// Atomic "first writer wins" using MERGE with HOLDLOCK
		// Returns 1 if this is a new insert (first processor), 0 if already exists
		var sql = $$"""
		            MERGE {{_options.QualifiedTableName}} WITH (HOLDLOCK) AS target
		            USING (SELECT @MessageId AS MessageId, @HandlerType AS HandlerType) AS source
		            ON target.MessageId = source.MessageId AND target.HandlerType = source.HandlerType
		            WHEN NOT MATCHED THEN
		            	INSERT (MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount)
		            	VALUES (@MessageId, @HandlerType, '', 0x, '{}', @Now, @Now, @ProcessedStatus, 0)
		            WHEN MATCHED AND target.Status = @ProcessedStatus THEN
		            	UPDATE SET MessageId = target.MessageId -- No-op update to satisfy MERGE syntax
		            OUTPUT $action AS Action;
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

		var action = await connection.QuerySingleOrDefaultAsync<string>(command).ConfigureAwait(false);
		var isFirstProcessor = action == "INSERT";

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
		           SELECT CASE WHEN EXISTS (
		           	SELECT 1 FROM {_options.QualifiedTableName}
		           	WHERE MessageId = @MessageId AND HandlerType = @HandlerType AND Status = @ProcessedStatus
		           ) THEN 1 ELSE 0 END
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
		           SELECT MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	   Status, LastError, RetryCount, LastAttemptAt, CorrelationId, TenantId, Source
		           FROM {_options.QualifiedTableName}
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType
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
		           SET Status = @FailedStatus, LastError = @LastError, RetryCount = RetryCount + 1, LastAttemptAt = @LastAttemptAt
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType
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
		           SELECT TOP (@BatchSize)
		           	MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	Status, LastError, RetryCount, LastAttemptAt, CorrelationId, TenantId, Source
		           FROM {_options.QualifiedTableName}
		           WHERE Status = @FailedStatus
		           	AND RetryCount < @MaxRetries
		           	AND (@OlderThan IS NULL OR LastAttemptAt < @OlderThan)
		           ORDER BY RetryCount ASC, LastAttemptAt ASC
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
		           SELECT MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	   Status, LastError, RetryCount, LastAttemptAt, CorrelationId, TenantId, Source
		           FROM {_options.QualifiedTableName}
		           ORDER BY ReceivedAt DESC
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
		           	COUNT(*) AS TotalEntries,
		           	SUM(CASE WHEN Status = @ProcessedStatus THEN 1 ELSE 0 END) AS ProcessedEntries,
		           	SUM(CASE WHEN Status = @FailedStatus THEN 1 ELSE 0 END) AS FailedEntries,
		           	SUM(CASE WHEN Status = @ReceivedStatus OR Status = @ProcessingStatus THEN 1 ELSE 0 END) AS PendingEntries
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
		           WHERE Status = @ProcessedStatus AND ProcessedAt < @CutoffDate
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

	private static Func<SqlConnection> CreateConnectionFactory(SqlServerInboxOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return () => new SqlConnection(options.ConnectionString);
	}

	#region Private Methods

	private string SerializeMetadata(IDictionary<string, object> metadata)
	{
		return JsonSerializer.Serialize(metadata, _jsonOptions);
	}

	private IDictionary<string, object> DeserializeMetadata(string? json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return new Dictionary<string, object>(StringComparer.Ordinal);
		}

		return JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions)
			   ?? new Dictionary<string, object>(StringComparer.Ordinal);
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
		public string MessageId { get; set; } = string.Empty;
		public string HandlerType { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public byte[] Payload { get; set; } = [];
		public string? Metadata { get; set; }
		public DateTimeOffset ReceivedAt { get; set; }
		public DateTimeOffset? ProcessedAt { get; set; }
		public int Status { get; set; }
		public string? LastError { get; set; }
		public int RetryCount { get; set; }
		public DateTimeOffset? LastAttemptAt { get; set; }
		public string? CorrelationId { get; set; }
		public string? TenantId { get; set; }
		public string? Source { get; set; }
	}

	#endregion Row Types
}
