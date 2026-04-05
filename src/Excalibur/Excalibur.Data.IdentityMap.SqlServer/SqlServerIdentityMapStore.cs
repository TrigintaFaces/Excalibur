// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.IdentityMap.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IIdentityMapStore"/>.
/// </summary>
internal sealed partial class SqlServerIdentityMapStore : IIdentityMapStore
{
	private readonly SqlServerIdentityMapOptions _options;
	private readonly ILogger<SqlServerIdentityMapStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerIdentityMapStore"/> class.
	/// </summary>
	/// <param name="options">The SQL Server identity map options.</param>
	/// <param name="logger">The logger.</param>
	public SqlServerIdentityMapStore(
		IOptions<SqlServerIdentityMapOptions> options,
		ILogger<SqlServerIdentityMapStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async ValueTask<string?> ResolveAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			SELECT AggregateId
			FROM {_options.QualifiedTableName}
			WHERE ExternalSystem = @ExternalSystem
			  AND ExternalId = @ExternalId
			  AND AggregateType = @AggregateType;
			""";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var result = await connection.QuerySingleOrDefaultAsync<string?>(
			new CommandDefinition(
				sql,
				new { ExternalSystem = externalSystem, ExternalId = externalId, AggregateType = aggregateType },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return result;
	}

	/// <inheritdoc/>
	public async ValueTask BindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			MERGE {_options.QualifiedTableName} AS target
			USING (SELECT @ExternalSystem AS ExternalSystem, @ExternalId AS ExternalId, @AggregateType AS AggregateType) AS source
			ON target.ExternalSystem = source.ExternalSystem
			   AND target.ExternalId = source.ExternalId
			   AND target.AggregateType = source.AggregateType
			WHEN MATCHED THEN
				UPDATE SET AggregateId = @AggregateId, UpdatedAt = SYSUTCDATETIME()
			WHEN NOT MATCHED THEN
				INSERT (ExternalSystem, ExternalId, AggregateType, AggregateId)
				VALUES (@ExternalSystem, @ExternalId, @AggregateType, @AggregateId);
			""";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { ExternalSystem = externalSystem, ExternalId = externalId, AggregateType = aggregateType, AggregateId = aggregateId },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		Log.BindingStored(_logger, aggregateType, externalSystem, externalId, aggregateId);
	}

	/// <inheritdoc/>
	public async ValueTask<IdentityBindResult> TryBindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		// Use MERGE with OUTPUT to atomically insert-or-return in a single round trip.
		// The $action column indicates whether the row was INSERTed or matched (existing).
		var sql = $"""
			MERGE {_options.QualifiedTableName} AS target
			USING (SELECT @ExternalSystem AS ExternalSystem, @ExternalId AS ExternalId, @AggregateType AS AggregateType) AS source
			ON target.ExternalSystem = source.ExternalSystem
			   AND target.ExternalId = source.ExternalId
			   AND target.AggregateType = source.AggregateType
			WHEN NOT MATCHED THEN
				INSERT (ExternalSystem, ExternalId, AggregateType, AggregateId)
				VALUES (@ExternalSystem, @ExternalId, @AggregateType, @AggregateId)
			WHEN MATCHED THEN
				UPDATE SET UpdatedAt = UpdatedAt
			OUTPUT $action AS MergeAction, inserted.AggregateId;
			""";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var result = await connection.QuerySingleAsync<(string MergeAction, string AggregateId)>(
			new CommandDefinition(
				sql,
				new { ExternalSystem = externalSystem, ExternalId = externalId, AggregateType = aggregateType, AggregateId = aggregateId },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var wasCreated = string.Equals(result.MergeAction, "INSERT", StringComparison.OrdinalIgnoreCase);

		if (!wasCreated)
		{
			Log.BindingConflict(_logger, aggregateType, externalSystem, externalId, aggregateId, result.AggregateId);
		}

		return new IdentityBindResult(result.AggregateId, wasCreated);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> UnbindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			DELETE FROM {_options.QualifiedTableName}
			WHERE ExternalSystem = @ExternalSystem
			  AND ExternalId = @ExternalId
			  AND AggregateType = @AggregateType;
			""";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { ExternalSystem = externalSystem, ExternalId = externalId, AggregateType = aggregateType },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return rowsAffected > 0;
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyDictionary<string, string>> ResolveBatchAsync(
		string externalSystem,
		IReadOnlyList<string> externalIds,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		if (externalIds.Count == 0)
		{
			return new Dictionary<string, string>();
		}

		if (externalIds.Count == 1)
		{
			var single = await ResolveAsync(externalSystem, externalIds[0], aggregateType, cancellationToken)
				.ConfigureAwait(false);

			return single is not null
				? new Dictionary<string, string> { [externalIds[0]] = single }
				: new Dictionary<string, string>();
		}

		var result = new Dictionary<string, string>(externalIds.Count);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var chunk in externalIds.Chunk(_options.MaxBatchSize))
		{
			var parameters = new DynamicParameters();
			parameters.Add("@ExternalSystem", externalSystem);
			parameters.Add("@AggregateType", aggregateType);

			var sb = new StringBuilder();
			sb.Append("SELECT ExternalId, AggregateId FROM ");
			sb.Append(_options.QualifiedTableName);
			sb.Append(" WHERE ExternalSystem = @ExternalSystem AND AggregateType = @AggregateType AND ExternalId IN (");

			for (var i = 0; i < chunk.Length; i++)
			{
				if (i > 0)
				{
					sb.Append(", ");
				}

				var paramName = $"@p{i}";
				sb.Append(paramName);
				parameters.Add(paramName, chunk[i]);
			}

			sb.Append(");");

			var rows = await connection.QueryAsync<(string ExternalId, string AggregateId)>(
				new CommandDefinition(
					sb.ToString(),
					parameters,
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			foreach (var (extId, aggId) in rows)
			{
				result[extId] = aggId;
			}
		}

		return result;
	}

	private static partial class Log
	{
		[LoggerMessage(
			EventId = 3700,
			Level = LogLevel.Information,
			Message = "Stored identity binding for {AggregateType} from {ExternalSystem}:{ExternalId} -> {AggregateId}.")]
		public static partial void BindingStored(
			ILogger logger,
			string aggregateType,
			string externalSystem,
			string externalId,
			string aggregateId);

		[LoggerMessage(
			EventId = 3701,
			Level = LogLevel.Warning,
			Message = "Identity binding conflict for {AggregateType} from {ExternalSystem}:{ExternalId}. Requested {RequestedAggregateId}, existing {ExistingAggregateId}.")]
		public static partial void BindingConflict(
			ILogger logger,
			string aggregateType,
			string externalSystem,
			string externalId,
			string requestedAggregateId,
			string existingAggregateId);
	}
}
