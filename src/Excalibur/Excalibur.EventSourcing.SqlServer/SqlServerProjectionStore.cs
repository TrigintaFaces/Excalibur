// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides projection storage using SQL Server with JSON serialization for projection data.
/// Uses MERGE statement for thread-safe upsert operations and dynamic SQL generation
/// for dictionary-based filter queries.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed class SqlServerProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _tableName;
	private readonly ILogger<SqlServerProjectionStore<TProjection>> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tableName">Optional table name. Defaults to projection type name.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// </remarks>
	public SqlServerProjectionStore(
		string connectionString,
		ILogger<SqlServerProjectionStore<TProjection>> logger,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null)
		: this(CreateConnectionFactory(connectionString), logger, tableName, jsonOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerProjectionStore{TProjection}"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tableName">Optional table name. Defaults to projection type name.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	/// <remarks>
	/// This is the advanced constructor for scenarios requiring custom connection management.
	/// </remarks>
	public SqlServerProjectionStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerProjectionStore<TProjection>> logger,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tableName = tableName ?? typeof(TProjection).Name;
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

		const string sqlTemplate = """
		                           SELECT Data FROM [{0}] WHERE Id = @Id
		                           """;

		var sql = string.Format(sqlTemplate, _tableName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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

		const string sqlTemplate = """
		                           MERGE [{0}] AS target
		                           USING (SELECT @Id AS Id, @Data AS Data, @UpdatedAt AS UpdatedAt) AS source
		                           ON target.Id = source.Id
		                           WHEN MATCHED THEN
		                           	UPDATE SET Data = source.Data, UpdatedAt = source.UpdatedAt
		                           WHEN NOT MATCHED THEN
		                           	INSERT (Id, Data, CreatedAt, UpdatedAt)
		                           	VALUES (source.Id, source.Data, source.UpdatedAt, source.UpdatedAt);
		                           """;

		var sql = string.Format(sqlTemplate, _tableName);
		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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

		const string sqlTemplate = """
		                           DELETE FROM [{0}] WHERE Id = @Id
		                           """;

		var sql = string.Format(sqlTemplate, _tableName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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
		           SELECT Data FROM [{_tableName}]
		           {whereClause}
		           {orderByClause}
		           {paginationClause}
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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
		           SELECT COUNT(*) FROM [{_tableName}]
		           {whereClause}
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ExecuteScalarAsync<long>(
				new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return () => new SqlConnection(connectionString);
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
			var paramName = $"@p{paramIndex++}";

			// For JSON data, we need to use JSON_VALUE to extract the property
			var jsonPath = $"$.{char.ToLowerInvariant(parsed.PropertyName[0])}{parsed.PropertyName[1..]}";

			var condition = parsed.Operator switch
			{
				FilterOperator.Equals => $"JSON_VALUE(Data, '{jsonPath}') = {paramName}",
				FilterOperator.NotEquals => $"JSON_VALUE(Data, '{jsonPath}') <> {paramName}",
				FilterOperator.GreaterThan => $"CAST(JSON_VALUE(Data, '{jsonPath}') AS FLOAT) > {paramName}",
				FilterOperator.GreaterThanOrEqual => $"CAST(JSON_VALUE(Data, '{jsonPath}') AS FLOAT) >= {paramName}",
				FilterOperator.LessThan => $"CAST(JSON_VALUE(Data, '{jsonPath}') AS FLOAT) < {paramName}",
				FilterOperator.LessThanOrEqual => $"CAST(JSON_VALUE(Data, '{jsonPath}') AS FLOAT) <= {paramName}",
				FilterOperator.Contains => $"JSON_VALUE(Data, '{jsonPath}') LIKE {paramName}",
				FilterOperator.In => BuildInCondition(jsonPath, value, paramName, parameters, ref paramIndex),
				_ => $"JSON_VALUE(Data, '{jsonPath}') = {paramName}"
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
			return $"JSON_VALUE(Data, '{jsonPath}') = {paramName}";
		}

		var values = new List<string>();
		foreach (var item in enumerable)
		{
			var itemParamName = $"@p{paramIndex++}";
			parameters.Add(itemParamName, item);
			values.Add(itemParamName);
		}

		if (values.Count == 0)
		{
			return "1 = 0"; // Empty IN clause, always false
		}

		return $"JSON_VALUE(Data, '{jsonPath}') IN ({string.Join(", ", values)})";
	}

	private static string BuildOrderByClause(QueryOptions? options)
	{
		if (options?.OrderBy is null)
		{
			return "ORDER BY Id"; // Default ordering for consistent pagination
		}

		var jsonPath = $"$.{char.ToLowerInvariant(options.OrderBy[0])}{options.OrderBy[1..]}";
		var direction = options.Descending ? "DESC" : "ASC";

		return $"ORDER BY JSON_VALUE(Data, '{jsonPath}') {direction}";
	}

	private static string BuildPaginationClause(QueryOptions? options, DynamicParameters parameters)
	{
		if (options?.Skip is null && options?.Take is null)
		{
			return string.Empty;
		}

		var skip = options?.Skip ?? 0;
		var take = options?.Take ?? int.MaxValue;

		parameters.Add("@Skip", skip);
		parameters.Add("@Take", take);

		return "OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
	}
}
