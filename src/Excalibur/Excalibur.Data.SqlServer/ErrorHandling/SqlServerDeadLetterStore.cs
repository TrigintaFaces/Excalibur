// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Dapper;

using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.ErrorHandling;

/// <summary>
/// SQL Server implementation of the dead letter store.
/// Uses IOptions pattern for configuration consistency with other SqlServer stores.
/// </summary>
public sealed partial class SqlServerDeadLetterStore : IDeadLetterStore
{
	private readonly string _connectionString;
	private readonly ILogger<SqlServerDeadLetterStore> _logger;
	private readonly string _schema;
	private readonly string _tableName;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDeadLetterStore" /> class.
	/// </summary>
	/// <param name="options"> The SQL Server dead letter options. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	public SqlServerDeadLetterStore(
		IOptions<SqlServerDeadLetterOptions> options,
		ILogger<SqlServerDeadLetterStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		var opts = options.Value;
		ArgumentException.ThrowIfNullOrWhiteSpace(opts.ConnectionString);

		_connectionString = opts.ConnectionString;
		_schema = opts.SchemaName;
		_tableName = opts.TableName;
		_logger = logger;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "JSON serialization used for properties storage; Dictionary<string, string> is well-defined and preserved")]
	[RequiresDynamicCode(
		"JSON serialization of message properties dictionary requires dynamic code generation for type-specific serialization logic.")]
	public async Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		const string sql = """
		                    INSERT INTO [{0}].[{1}] (
		                    Id, MessageId, MessageType, MessageBody, MessageMetadata,
		                    Reason, ExceptionDetails, ProcessingAttempts, MovedToDeadLetterAt,
		                    FirstAttemptAt, LastAttemptAt, IsReplayed, ReplayedAt,
		                    SourceSystem, CorrelationId, Properties
		                    ) VALUES (
		                    @Id, @MessageId, @MessageType, @MessageBody, @MessageMetadata,
		                    @Reason, @ExceptionDetails, @ProcessingAttempts, @MovedToDeadLetterAt,
		                    @FirstAttemptAt, @LastAttemptAt, @IsReplayed, @ReplayedAt,
		                    @SourceSystem, @CorrelationId, @Properties
		                    )
		""";

		using var connection = CreateConnection();
		_ = await connection.ExecuteAsync(
			string.Format(CultureInfo.InvariantCulture, sql, _schema, _tableName),
			new
			{
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
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public async Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		const string sql = """
		                    SELECT TOP 1 * FROM [{0}].[{1}]
		                    WHERE MessageId = @MessageId
		""";

		using var connection = CreateConnection();
		var result = await connection.QueryFirstOrDefaultAsync<DeadLetterMessageDto>(
			string.Format(CultureInfo.InvariantCulture, sql, _schema, _tableName),
			new { MessageId = messageId }).ConfigureAwait(false);

		return result?.ToDeadLetterMessage();
	}

	/// <inheritdoc />
	public async Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
		DeadLetterFilter filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);

		var sql = $"""
		SELECT * FROM [{_schema}].[{_tableName}]
		            WHERE 1=1
		""";

		var parameters = new DynamicParameters();

		if (!string.IsNullOrWhiteSpace(filter.MessageType))
		{
			sql += " AND MessageType = @MessageType";
			parameters.Add("MessageType", filter.MessageType);
		}

		if (!string.IsNullOrWhiteSpace(filter.Reason))
		{
			sql += " AND Reason LIKE @Reason";
			parameters.Add("Reason", $"%{filter.Reason}%");
		}

		if (filter.FromDate.HasValue)
		{
			sql += " AND MovedToDeadLetterAt >= @FromDate";
			parameters.Add("FromDate", filter.FromDate.Value);
		}

		if (filter.ToDate.HasValue)
		{
			sql += " AND MovedToDeadLetterAt <= @ToDate";
			parameters.Add("ToDate", filter.ToDate.Value);
		}

		if (filter.IsReplayed.HasValue)
		{
			sql += " AND IsReplayed = @IsReplayed";
			parameters.Add("IsReplayed", filter.IsReplayed.Value);
		}

		if (!string.IsNullOrWhiteSpace(filter.SourceSystem))
		{
			sql += " AND SourceSystem = @SourceSystem";
			parameters.Add("SourceSystem", filter.SourceSystem);
		}

		if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
		{
			sql += " AND CorrelationId = @CorrelationId";
			parameters.Add("CorrelationId", filter.CorrelationId);
		}

		sql += """
		        ORDER BY MovedToDeadLetterAt DESC
		        OFFSET @Skip ROWS
		        FETCH NEXT @MaxResults ROWS ONLY
		""";

		parameters.Add("Skip", filter.Skip);
		parameters.Add("MaxResults", filter.MaxResults);

		using var connection = CreateConnection();
		var results = await connection.QueryAsync<DeadLetterMessageDto>(sql, parameters).ConfigureAwait(false);

		return results.Select(static dto => dto.ToDeadLetterMessage());
	}

	/// <inheritdoc />
	public async Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		const string sql = """
		                    UPDATE [{0}].[{1}]
		                    SET IsReplayed = 1, ReplayedAt = @ReplayedAt
		                    WHERE MessageId = @MessageId
		""";

		using var connection = CreateConnection();
		var rowsAffected = await connection.ExecuteAsync(
			string.Format(CultureInfo.InvariantCulture, sql, _schema, _tableName),
			new { MessageId = messageId, ReplayedAt = DateTimeOffset.UtcNow }).ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			LogMarkedDeadLetterMessageAsReplayed(messageId);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		const string sql = """
		                    DELETE FROM [{0}].[{1}]
		                    WHERE MessageId = @MessageId
		""";

		using var connection = CreateConnection();
		var rowsAffected = await connection.ExecuteAsync(
			string.Format(CultureInfo.InvariantCulture, sql, _schema, _tableName),
			new { MessageId = messageId }).ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			LogDeletedDeadLetterMessage(messageId);
		}

		return rowsAffected > 0;
	}

	/// <inheritdoc />
	public async Task<long> GetCountAsync(CancellationToken cancellationToken)
	{
		const string sql = "SELECT COUNT(*) FROM [{0}].[{1}]";

		using var connection = CreateConnection();
		return await connection.ExecuteScalarAsync<long>(
			string.Format(CultureInfo.InvariantCulture, sql, _schema, _tableName)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> CleanupOldMessagesAsync(int retentionDays, CancellationToken cancellationToken)
	{
		const string sql = """
		                    DELETE FROM [{0}].[{1}]
		                    WHERE MovedToDeadLetterAt < @CutoffDate
		""";

		using var connection = CreateConnection();
		var rowsAffected = await connection.ExecuteAsync(
			string.Format(CultureInfo.InvariantCulture, sql, _schema, _tableName),
			new { CutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays) }).ConfigureAwait(false);

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
		var connection = new SqlConnection(_connectionString);
		connection.Open();
		return connection;
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.DeadLetterMessageStored, LogLevel.Information,
		"Stored dead letter message {MessageId} of type {MessageType} with reason {Reason}")]
	private partial void LogStoredDeadLetterMessage(string messageId, string messageType, string reason);

	[LoggerMessage(DataSqlServerEventId.DeadLetterStoreError, LogLevel.Information,
		"Marked dead letter message {MessageId} as replayed")]
	private partial void LogMarkedDeadLetterMessageAsReplayed(string messageId);

	[LoggerMessage(DataSqlServerEventId.DeadLetterRetrievalError, LogLevel.Information,
		"Deleted dead letter message {MessageId}")]
	private partial void LogDeletedDeadLetterMessage(string messageId);

	[LoggerMessage(DataSqlServerEventId.DeadLetterCleanupCompleted, LogLevel.Information,
		"Cleaned up {RowsAffected} old dead letter messages with retention of {RetentionDays} days")]
	private partial void LogCleanedUpOldDeadLetterMessages(int rowsAffected, int retentionDays);

	/// <summary>
	/// DTO for mapping database results.
	/// </summary>
	private sealed class DeadLetterMessageDto
	{
		public string Id { get; set; } = string.Empty;

		public string MessageId { get; set; } = string.Empty;

		public string MessageType { get; set; } = string.Empty;

		public string MessageBody { get; set; } = string.Empty;

		public string MessageMetadata { get; set; } = string.Empty;

		public string Reason { get; set; } = string.Empty;

		public string? ExceptionDetails { get; set; }

		public int ProcessingAttempts { get; set; }

		public DateTimeOffset MovedToDeadLetterAt { get; set; }

		public DateTimeOffset? FirstAttemptAt { get; set; }

		public DateTimeOffset? LastAttemptAt { get; set; }

		public bool IsReplayed { get; set; }

		public DateTimeOffset? ReplayedAt { get; set; }

		public string? SourceSystem { get; set; }

		public string? CorrelationId { get; set; }

		public string? Properties { get; set; }

		[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "JSON deserialization used for properties retrieval; Dictionary<string, string> is well-defined and preserved")]
		[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
		public DeadLetterMessage ToDeadLetterMessage() =>
			new()
			{
				Id = Id,
				MessageId = MessageId,
				MessageType = MessageType,
				MessageBody = MessageBody,
				MessageMetadata = MessageMetadata,
				Reason = Reason,
				ExceptionDetails = ExceptionDetails,
				ProcessingAttempts = ProcessingAttempts,
				MovedToDeadLetterAt = MovedToDeadLetterAt,
				FirstAttemptAt = FirstAttemptAt,
				LastAttemptAt = LastAttemptAt,
				IsReplayed = IsReplayed,
				ReplayedAt = ReplayedAt,
				SourceSystem = SourceSystem,
				CorrelationId = CorrelationId,
				Properties = string.IsNullOrWhiteSpace(Properties)
					? []
					: JsonSerializer.Deserialize<Dictionary<string, string>>(Properties) ?? [],
			};
	}
}
