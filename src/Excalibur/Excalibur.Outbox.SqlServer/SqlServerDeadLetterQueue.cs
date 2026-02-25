// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IDeadLetterQueue"/> for production scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores dead letter entries in SQL Server with full support for
/// filtering, replay, and purge operations. Uses Dapper for data access.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Options-based for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class SqlServerDeadLetterQueue : IDeadLetterQueue
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly SqlServerDeadLetterQueueOptions _options;
	private readonly ILogger<SqlServerDeadLetterQueue> _logger;
	private readonly Func<object, Task>? _replayHandler;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDeadLetterQueue"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="replayHandler">Optional handler for replaying messages.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerDeadLetterQueue(Func{SqlConnection}, SqlServerDeadLetterQueueOptions, ILogger{SqlServerDeadLetterQueue}, Func{object, Task}?)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerDeadLetterQueue(
		IOptions<SqlServerDeadLetterQueueOptions> options,
		ILogger<SqlServerDeadLetterQueue> logger,
		Func<object, Task>? replayHandler = null)
		: this(CreateConnectionFactory(options?.Value), options?.Value, logger, replayHandler)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDeadLetterQueue"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="options">The configuration options (used for table names, timeouts, etc.).</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="replayHandler">Optional handler for replaying messages.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Multi-database setups with marker interfaces (e.g., IDomainDb, IDlqDb)</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with <see cref="IDb"/> abstraction</description></item>
	/// </list>
	/// </remarks>
	public SqlServerDeadLetterQueue(
		Func<SqlConnection> connectionFactory,
		SqlServerDeadLetterQueueOptions options,
		ILogger<SqlServerDeadLetterQueue> logger,
		Func<object, Task>? replayHandler = null)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_options = options;
		_logger = logger;
		_replayHandler = replayHandler;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc />
	public async Task<Guid> EnqueueAsync<T>(
		T message,
		DeadLetterReason reason,
		CancellationToken cancellationToken,
		Exception? exception = null,
		IDictionary<string, string>? metadata = null)
	{
		ArgumentNullException.ThrowIfNull(message);

		var id = Guid.NewGuid();
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
		var metadataJson = metadata is { Count: > 0 }
			? JsonSerializer.Serialize(metadata, _jsonOptions)
			: null;

		var sql = $"""
		           INSERT INTO {_options.QualifiedTableName}
		           	(Id, MessageType, Payload, Reason, ExceptionMessage, ExceptionStackTrace,
		           	 EnqueuedAt, OriginalAttempts, Metadata, CorrelationId, CausationId,
		           	 SourceQueue, IsReplayed, ReplayedAt)
		           VALUES
		           	(@Id, @MessageType, @Payload, @Reason, @ExceptionMessage, @ExceptionStackTrace,
		           	 @EnqueuedAt, @OriginalAttempts, @Metadata, @CorrelationId, @CausationId,
		           	 @SourceQueue, @IsReplayed, @ReplayedAt)
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new
			{
				Id = id,
				MessageType = typeof(T).FullName ?? typeof(T).Name,
				Payload = payload,
				Reason = (int)reason,
				ExceptionMessage = exception?.Message,
				ExceptionStackTrace = exception?.StackTrace,
				EnqueuedAt = DateTimeOffset.UtcNow,
				OriginalAttempts = 1,
				Metadata = metadataJson,
				CorrelationId = metadata is not null && metadata.TryGetValue("CorrelationId", out var corrId) ? corrId : null,
				CausationId = metadata is not null && metadata.TryGetValue("CausationId", out var causId) ? causId : null,
				SourceQueue = metadata is not null && metadata.TryGetValue("SourceQueue", out var srcQueue) ? srcQueue : null,
				IsReplayed = false,
				ReplayedAt = (DateTimeOffset?)null
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		_logger.LogInformation(
			"Dead lettered message {MessageType} with ID {EntryId} for reason {Reason}",
			typeof(T).FullName, id, reason);

		return id;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
		CancellationToken cancellationToken,
		DeadLetterQueryFilter? filter = null,
		int limit = 100)
	{
		var (whereClause, parameters) = BuildWhereClause(filter);
		var offset = filter?.Skip ?? 0;

		var sql = $"""
		           SELECT Id, MessageType, Payload, Reason, ExceptionMessage, ExceptionStackTrace,
		           	   EnqueuedAt, OriginalAttempts, Metadata, CorrelationId, CausationId,
		           	   SourceQueue, IsReplayed, ReplayedAt
		           FROM {_options.QualifiedTableName}
		           {whereClause}
		           ORDER BY EnqueuedAt DESC
		           OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY
		           """;

		parameters.Add("@Offset", offset);
		parameters.Add("@Limit", limit);

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			parameters,
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<DeadLetterRow>(command).ConfigureAwait(false);

		return rows.Select(MapRowToEntry).ToList();
	}

	/// <inheritdoc />
	public async Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken)
	{
		var sql = $"""
		           SELECT Id, MessageType, Payload, Reason, ExceptionMessage, ExceptionStackTrace,
		           	   EnqueuedAt, OriginalAttempts, Metadata, CorrelationId, CausationId,
		           	   SourceQueue, IsReplayed, ReplayedAt
		           FROM {_options.QualifiedTableName}
		           WHERE Id = @Id
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Id = entryId },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var row = await connection.QuerySingleOrDefaultAsync<DeadLetterRow>(command).ConfigureAwait(false);

		return row is null ? null : MapRowToEntry(row);
	}

	/// <inheritdoc />
	public async Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken)
	{
		var entry = await GetEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
		if (entry is null)
		{
			return false;
		}

		if (_replayHandler is not null)
		{
			try
			{
				var message = JsonSerializer.Deserialize<object>(entry.Payload, _jsonOptions);
				if (message is not null)
				{
					await _replayHandler(message).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to replay dead letter entry {EntryId}", entryId);
				throw;
			}
		}

		// Mark as replayed
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET IsReplayed = 1, ReplayedAt = @ReplayedAt
		           WHERE Id = @Id
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Id = entryId, ReplayedAt = DateTimeOffset.UtcNow },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		_logger.LogInformation("Replayed dead letter entry {EntryId}", entryId);
		return true;
	}

	/// <inheritdoc />
	public async Task<int> ReplayBatchAsync(DeadLetterQueryFilter filter, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);

		var entries = await GetEntriesAsync(cancellationToken, filter, limit: 1000).ConfigureAwait(false);
		var replayedCount = 0;

		foreach (var entry in entries)
		{
			if (await ReplayAsync(entry.Id, cancellationToken).ConfigureAwait(false))
			{
				replayedCount++;
			}
		}

		return replayedCount;
	}

	/// <inheritdoc />
	public async Task<bool> PurgeAsync(Guid entryId, CancellationToken cancellationToken)
	{
		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE Id = @Id
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Id = entryId },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var deleted = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (deleted > 0)
		{
			_logger.LogInformation("Purged dead letter entry {EntryId}", entryId);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public async Task<int> PurgeOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken)
	{
		var cutoff = DateTimeOffset.UtcNow - olderThan;

		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE EnqueuedAt < @Cutoff
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Cutoff = cutoff },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var purgedCount = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (purgedCount > 0)
		{
			_logger.LogInformation("Purged {Count} dead letter entries older than {Age}", purgedCount, olderThan);
		}

		return purgedCount;
	}

	/// <inheritdoc />
	public async Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null)
	{
		var (whereClause, parameters) = BuildWhereClause(filter);

		var sql = $"""
		           SELECT COUNT(*)
		           FROM {_options.QualifiedTableName}
		           {whereClause}
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			parameters,
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(false);
	}

	private static Func<SqlConnection> CreateConnectionFactory(SqlServerDeadLetterQueueOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Apply ApplicationName for connection pool isolation
		var connectionString = options.ConnectionString;
		if (!string.IsNullOrWhiteSpace(options.ApplicationName))
		{
			var builder = new SqlConnectionStringBuilder(connectionString)
			{
				ApplicationName = options.ApplicationName,
			};
			connectionString = builder.ConnectionString;
		}

		return () => new SqlConnection(connectionString);
	}

	#region Private Methods

	private static (string whereClause, DynamicParameters parameters) BuildWhereClause(DeadLetterQueryFilter? filter)
	{
		var parameters = new DynamicParameters();

		if (filter is null)
		{
			return (string.Empty, parameters);
		}

		var conditions = new List<string>();

		if (!string.IsNullOrWhiteSpace(filter.MessageType))
		{
			conditions.Add("MessageType LIKE @MessageType");
			parameters.Add("@MessageType", $"%{filter.MessageType}%");
		}

		if (filter.Reason.HasValue)
		{
			conditions.Add("Reason = @Reason");
			parameters.Add("@Reason", (int)filter.Reason.Value);
		}

		if (filter.FromDate.HasValue)
		{
			conditions.Add("EnqueuedAt >= @FromDate");
			parameters.Add("@FromDate", filter.FromDate.Value);
		}

		if (filter.ToDate.HasValue)
		{
			conditions.Add("EnqueuedAt <= @ToDate");
			parameters.Add("@ToDate", filter.ToDate.Value);
		}

		if (filter.IsReplayed.HasValue)
		{
			conditions.Add("IsReplayed = @IsReplayed");
			parameters.Add("@IsReplayed", filter.IsReplayed.Value);
		}

		if (!string.IsNullOrWhiteSpace(filter.SourceQueue))
		{
			conditions.Add("SourceQueue = @SourceQueue");
			parameters.Add("@SourceQueue", filter.SourceQueue);
		}

		if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
		{
			conditions.Add("CorrelationId = @CorrelationId");
			parameters.Add("@CorrelationId", filter.CorrelationId);
		}

		if (filter.MinAttempts.HasValue)
		{
			conditions.Add("OriginalAttempts >= @MinAttempts");
			parameters.Add("@MinAttempts", filter.MinAttempts.Value);
		}

		var whereClause = conditions.Count > 0
			? "WHERE " + string.Join(" AND ", conditions)
			: string.Empty;

		return (whereClause, parameters);
	}

	private DeadLetterEntry MapRowToEntry(DeadLetterRow row)
	{
		IDictionary<string, string>? metadata = null;
		if (!string.IsNullOrEmpty(row.Metadata))
		{
			metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Metadata, _jsonOptions);
		}

		return new DeadLetterEntry
		{
			Id = row.Id,
			MessageType = row.MessageType,
			Payload = row.Payload,
			Reason = (DeadLetterReason)row.Reason,
			ExceptionMessage = row.ExceptionMessage,
			ExceptionStackTrace = row.ExceptionStackTrace,
			EnqueuedAt = row.EnqueuedAt,
			OriginalAttempts = row.OriginalAttempts,
			Metadata = metadata,
			CorrelationId = row.CorrelationId,
			CausationId = row.CausationId,
			SourceQueue = row.SourceQueue,
			IsReplayed = row.IsReplayed,
			ReplayedAt = row.ReplayedAt
		};
	}

	#endregion Private Methods

	#region Row Type

	private sealed class DeadLetterRow
	{
		public Guid Id { get; set; }
		public string MessageType { get; set; } = string.Empty;
		public byte[] Payload { get; set; } = [];
		public int Reason { get; set; }
		public string? ExceptionMessage { get; set; }
		public string? ExceptionStackTrace { get; set; }
		public DateTimeOffset EnqueuedAt { get; set; }
		public int OriginalAttempts { get; set; }
		public string? Metadata { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public string? SourceQueue { get; set; }
		public bool IsReplayed { get; set; }
		public DateTimeOffset? ReplayedAt { get; set; }
	}

	#endregion Row Type
}
