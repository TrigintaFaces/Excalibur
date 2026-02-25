// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides projection storage using Postgres with JSONB for efficient JSON querying.
/// Uses INSERT ... ON CONFLICT for thread-safe upsert operations and Postgres's
/// native JSONB operators for efficient filter queries.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: NpgsqlDataSource for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed class PostgresProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly string _tableName;
	private readonly ILogger<PostgresProjectionStore<TProjection>> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tableName">Optional table name. Defaults to projection type name in snake_case.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// </remarks>
	public PostgresProjectionStore(
		string connectionString,
		ILogger<PostgresProjectionStore<TProjection>> logger,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null)
		: this(CreateDataSource(connectionString), logger, tableName, jsonOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresProjectionStore{TProjection}"/> class with an NpgsqlDataSource.
	/// </summary>
	/// <param name="dataSource">
	/// An <see cref="NpgsqlDataSource"/> that manages connection pooling.
	/// Using NpgsqlDataSource is the recommended pattern per Npgsql documentation.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tableName">Optional table name. Defaults to projection type name in snake_case.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	/// <remarks>
	/// This is the advanced constructor for scenarios requiring custom connection management.
	/// </remarks>
	public PostgresProjectionStore(
		NpgsqlDataSource dataSource,
		ILogger<PostgresProjectionStore<TProjection>> logger,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tableName = tableName ?? ToSnakeCase(typeof(TProjection).Name);
		_jsonOptions = jsonOptions ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public async Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		var sql = $"""
		           SELECT data FROM "{_tableName}" WHERE id = @Id
		           """;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var json = await connection.QuerySingleOrDefaultAsync<string>(
				new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (json is null)
		{
			return null;
		}

		return JsonSerializer.Deserialize<TProjection>(json, _jsonOptions);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public async Task UpsertAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		var sql = $"""
		           INSERT INTO "{_tableName}" (id, data, created_at, updated_at)
		           VALUES (@Id, @Data::jsonb, @UpdatedAt, @UpdatedAt)
		           ON CONFLICT (id)
		           DO UPDATE SET data = EXCLUDED.data, updated_at = EXCLUDED.updated_at
		           """;

		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
				new CommandDefinition(
					sql,
					new { Id = id, Data = json, UpdatedAt = now },
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		_logger.LogDebug("Upserted projection {ProjectionType}/{Id}", typeof(TProjection).Name, id);
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		var sql = $"""
		           DELETE FROM "{_tableName}" WHERE id = @Id
		           """;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
				new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		_logger.LogDebug("Deleted projection {ProjectionType}/{Id}", typeof(TProjection).Name, id);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = BuildWhereClause(filters);
		var orderByClause = BuildOrderByClause(options);
		var paginationClause = BuildPaginationClause(options, parameters);

		var sql = $"""
		           SELECT data FROM "{_tableName}"
		           {whereClause}
		           {orderByClause}
		           {paginationClause}
		           """;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var results = await connection.QueryAsync<string>(
				new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return results
			.Select(json => JsonSerializer.Deserialize<TProjection>(json, _jsonOptions)!)
			.ToList();
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = BuildWhereClause(filters);

		var sql = $"""
		           SELECT COUNT(*) FROM "{_tableName}"
		           {whereClause}
		           """;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ExecuteScalarAsync<long>(
				new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	private static NpgsqlDataSource CreateDataSource(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return NpgsqlDataSource.Create(connectionString);
	}

	private static string ToSnakeCase(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return name;
		}

		var result = new System.Text.StringBuilder();
		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];
			if (char.IsUpper(c) && i > 0)
			{
				_ = result.Append('_');
			}

			_ = result.Append(char.ToLowerInvariant(c));
		}

		return result.ToString();
	}

	private static (string WhereClause, DynamicParameters Parameters) BuildWhereClause(
		IDictionary<string, object>? filters)
	{
		var parameters = new DynamicParameters();

		if (filters is null || filters.Count == 0)
		{
			return (string.Empty, parameters);
		}

		var conditions = new List<string>();
		var paramIndex = 0;

		foreach (var (key, value) in filters)
		{
			var parsed = FilterParser.Parse(key);
			var paramName = $"p{paramIndex++}";

			// For Postgres JSONB, use ->> for text extraction
			var jsonPath = char.ToLowerInvariant(parsed.PropertyName[0]) + parsed.PropertyName[1..];

			var condition = parsed.Operator switch
			{
				FilterOperator.Equals => $"data->>'{jsonPath}' = @{paramName}",
				FilterOperator.NotEquals => $"data->>'{jsonPath}' <> @{paramName}",
				FilterOperator.GreaterThan => $"(data->>'{jsonPath}')::numeric > @{paramName}",
				FilterOperator.GreaterThanOrEqual => $"(data->>'{jsonPath}')::numeric >= @{paramName}",
				FilterOperator.LessThan => $"(data->>'{jsonPath}')::numeric < @{paramName}",
				FilterOperator.LessThanOrEqual => $"(data->>'{jsonPath}')::numeric <= @{paramName}",
				FilterOperator.Contains => $"data->>'{jsonPath}' ILIKE @{paramName}",
				FilterOperator.In => BuildInCondition(jsonPath, value, paramName, parameters, ref paramIndex),
				_ => $"data->>'{jsonPath}' = @{paramName}"
			};

			if (parsed.Operator != FilterOperator.In)
			{
				if (parsed.Operator == FilterOperator.Contains)
				{
					parameters.Add(paramName, $"%{value}%");
				}
				else
				{
					parameters.Add(paramName, value);
				}
			}

			conditions.Add(condition);
		}

		return ($"WHERE {string.Join(" AND ", conditions)}", parameters);
	}

	private static string BuildInCondition(
		string jsonPath,
		object value,
		string paramName,
		DynamicParameters parameters,
		ref int paramIndex)
	{
		if (value is not IEnumerable enumerable || value is string)
		{
			parameters.Add(paramName, value);
			return $"data->>'{jsonPath}' = @{paramName}";
		}

		var values = new List<string>();
		foreach (var item in enumerable)
		{
			var itemParamName = $"p{paramIndex++}";
			parameters.Add(itemParamName, item);
			values.Add($"@{itemParamName}");
		}

		if (values.Count == 0)
		{
			return "false"; // Empty IN clause, always false
		}

		return $"data->>'{jsonPath}' IN ({string.Join(", ", values)})";
	}

	private static string BuildOrderByClause(QueryOptions? options)
	{
		if (options?.OrderBy is null)
		{
			return "ORDER BY id"; // Default ordering for consistent pagination
		}

		var jsonPath = char.ToLowerInvariant(options.OrderBy[0]) + options.OrderBy[1..];
		var direction = options.Descending ? "DESC" : "ASC";

		return $"ORDER BY data->>'{jsonPath}' {direction}";
	}

	private static string BuildPaginationClause(QueryOptions? options, DynamicParameters parameters)
	{
		if (options?.Skip is null && options?.Take is null)
		{
			return string.Empty;
		}

		var skip = options?.Skip ?? 0;
		var take = options?.Take ?? int.MaxValue;

		parameters.Add("Skip", skip);
		parameters.Add("Take", take);

		return "LIMIT @Take OFFSET @Skip";
	}
}
