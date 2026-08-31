// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

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
public sealed partial class SqlServerProjectionStore<TProjection> : IProjectionStore<TProjection>,
	IPageableProjectionStore<TProjection>
	where TProjection : class
{
	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex ValidTableNameRegex();

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _tableName;
	private readonly ILogger<SqlServerProjectionStore<TProjection>> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ITenantContext _tenantContext;

	// Row-level tenant discriminator predicate, single-sourced so the SQL Server column name and
	// parameter (TenantId = @TenantId) are not repeated across the read/delete/query builders.
	private const string TenantColumnPredicate = "TenantId = @TenantId";

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tableName">Optional table name. Defaults to projection type name.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// </remarks>
	public SqlServerProjectionStore(
		string connectionString,
		ILogger<SqlServerProjectionStore<TProjection>> logger,
		ITenantContext tenantContext,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null)
		: this(CreateConnectionFactory(connectionString), logger, tenantContext, tableName, jsonOptions)
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
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <remarks>
	/// This is the advanced constructor for scenarios requiring custom connection management.
	/// </remarks>
	public SqlServerProjectionStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerProjectionStore<TProjection>> logger,
		ITenantContext tenantContext,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		var resolvedName = tableName ?? typeof(TProjection).Name;
		if (!ValidTableNameRegex().IsMatch(resolvedName))
		{
			throw new ArgumentException($"Table name '{resolvedName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.", nameof(tableName));
		}

		_tableName = resolvedName;

		// Converged on the single ProjectionSerializationDefaults read-model factory (camelCase +
		// null-omission) so every projection store's wire shape stays byte-compatible on the read path.
		_jsonOptions = jsonOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions();
	}
	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public async Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		// Canonical fail-closed seam for an always-tenant-scoped store: Scoped() throws
		// TenantRequiredException when the ambient tenant is null/blank, so an unscoped (false-isolation)
		// query is inexpressible. This store has no legitimate unscoped mode — its writes always stamp the
		// TenantId discriminator — so it requires a tenant rather than degrading to default(TenantScope).
		var tenantId = TenantScope.Scoped(_tenantContext.TenantId).TenantId;
		var tenantPredicate = " AND " + TenantColumnPredicate;
		var sql = $"SELECT Data FROM [{_tableName}] WHERE Id = @Id{tenantPredicate}";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@TenantId", tenantId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var json = await connection.QuerySingleOrDefaultAsync<string>(
				new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
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

		// Canonical fail-closed seam for an always-tenant-scoped store: Scoped() throws
		// TenantRequiredException when the ambient tenant is null/blank, so an unscoped (false-isolation)
		// query is inexpressible. This store has no legitimate unscoped mode — its writes always stamp the
		// TenantId discriminator — so it requires a tenant rather than degrading to default(TenantScope).
		var tenantId = TenantScope.Scoped(_tenantContext.TenantId).TenantId;

		// Tenant scoping rides the MERGE atomically: the tenant discriminator is part of the match key so a
		// tenant-A upsert can NEVER match (and overwrite) a tenant-B row with the same Id, and the INSERT
		// stamps the tenant. Tenant-facing write — fails closed: a null/blank ambient tenant is rejected up
		// front. This must never degrade to a client-side check — the isolation is in the match key itself.
		var sourceTenantSelect = ", @TenantId AS TenantId";
		var onTenantPredicate = " AND target.TenantId = source.TenantId";
		var insertTenantColumn = ", TenantId";
		var insertTenantValue = ", source.TenantId";

		var sql = $"""
			MERGE [{_tableName}] WITH (UPDLOCK, HOLDLOCK) AS target
			USING (SELECT @Id AS Id, @Data AS Data, @UpdatedAt AS UpdatedAt{sourceTenantSelect}) AS source
			ON target.Id = source.Id{onTenantPredicate}
			WHEN MATCHED THEN
				UPDATE SET Data = source.Data, UpdatedAt = source.UpdatedAt
			WHEN NOT MATCHED THEN
				INSERT (Id, Data, CreatedAt, UpdatedAt{insertTenantColumn})
				VALUES (source.Id, source.Data, source.UpdatedAt, source.UpdatedAt{insertTenantValue});
			""";

		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Data", json);
		parameters.Add("@UpdatedAt", now);
		parameters.Add("@TenantId", tenantId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
				new CommandDefinition(
					sql,
					parameters,
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

		// Canonical fail-closed seam for an always-tenant-scoped store: Scoped() throws
		// TenantRequiredException when the ambient tenant is null/blank, so an unscoped (false-isolation)
		// query is inexpressible. This store has no legitimate unscoped mode — its writes always stamp the
		// TenantId discriminator — so it requires a tenant rather than degrading to default(TenantScope).
		var tenantId = TenantScope.Scoped(_tenantContext.TenantId).TenantId;
		var tenantPredicate = " AND " + TenantColumnPredicate;
		var sql = $"DELETE FROM [{_tableName}] WHERE Id = @Id{tenantPredicate}";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@TenantId", tenantId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
				new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		_logger.LogDebug("Deleted projection {ProjectionType}/{Id}", typeof(TProjection).Name, id);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = BuildTenantScopedWhereClause(filters);
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
		var (whereClause, parameters) = BuildTenantScopedWhereClause(filters);

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

	/// <inheritdoc/>
	/// <remarks>
	/// Uses a single-roundtrip SQL query with <c>COUNT(*) OVER()</c> window function
	/// to return both the page data and total count in one database call.
	/// Falls back to <c>OFFSET/FETCH</c> for pagination.
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public async Task<PagedResult<TProjection>> QueryPagedAsync(
		IDictionary<string, object>? filters,
		int pageNumber,
		int pageSize,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		var (whereClause, parameters) = BuildTenantScopedWhereClause(filters);
		var orderByClause = BuildOrderByClause(options);

		var offset = (pageNumber - 1) * pageSize;
		parameters.Add("@PageOffset", offset);
		parameters.Add("@PageSize", pageSize);

		// Single-roundtrip: COUNT(*) OVER() returns total count alongside each row.
		// When no rows match, we still need the count, so use a CTE approach.
		var sql = $"""
			SELECT Data, COUNT(*) OVER() AS TotalCount
			FROM [{_tableName}]
			{whereClause}
			{orderByClause}
			OFFSET @PageOffset ROWS FETCH NEXT @PageSize ROWS ONLY
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<PagedRow>(
				new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var rowList = rows.AsList();

		if (rowList.Count == 0)
		{
			// No rows on this page — get the total count separately to report it
			var countSql = $"SELECT COUNT(*) FROM [{_tableName}] {whereClause}";
			var totalCount = await connection.ExecuteScalarAsync<long>(
					new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			return new PagedResult<TProjection>([], pageNumber, pageSize, totalCount);
		}

		var total = rowList[0].TotalCount;

		var items = rowList
			.Select(r => JsonSerializer.Deserialize<TProjection>(r.Data, _jsonOptions)!)
			.ToList();

		return new PagedResult<TProjection>(items, pageNumber, pageSize, total);
	}

	/// <summary>
	/// Row type for paged query results including the total count from <c>COUNT(*) OVER()</c>.
	/// </summary>
	private sealed record PagedRow(string Data, long TotalCount);

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return () => new SqlConnection(connectionString);
	}

	[GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]*$")]
	private static partial Regex ValidPropertyNameRegex();

	/// <summary>
	/// Builds the filter WHERE clause and appends the row-level tenant predicate
	/// (<c>TenantId = @TenantId</c>) so every read is scoped to the current tenant in the same statement.
	/// Fails closed: a null or whitespace ambient tenant throws <see cref="System.ArgumentException"/>
	/// rather than returning every tenant's rows.
	/// </summary>
	private (string WhereClause, DynamicParameters Parameters) BuildTenantScopedWhereClause(
		IDictionary<string, object>? filters)
	{
		var (whereClause, parameters) = BuildWhereClause(filters);

		// Fail closed: a projection query is a tenant-facing consumer read, so a null ambient tenant under
		// multi-tenancy must throw rather than silently return every tenant's rows (symmetric with the
		// keyed ops and the event-store fix). The single-tenant default context is non-null.
		// Canonical fail-closed seam for an always-tenant-scoped store: Scoped() throws
		// TenantRequiredException when the ambient tenant is null/blank, so an unscoped (false-isolation)
		// query is inexpressible. This store has no legitimate unscoped mode — its writes always stamp the
		// TenantId discriminator — so it requires a tenant rather than degrading to default(TenantScope).
		var tenantId = TenantScope.Scoped(_tenantContext.TenantId).TenantId;

		parameters.Add("@TenantId", tenantId);
		whereClause = string.IsNullOrEmpty(whereClause)
			? "WHERE " + TenantColumnPredicate
			: whereClause + " AND " + TenantColumnPredicate;

		return (whereClause, parameters);
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

			// Validate property name to prevent SQL injection via JSON path
			if (!ValidPropertyNameRegex().IsMatch(parsed.PropertyName))
			{
				throw new ArgumentException(
					$"Filter property name '{parsed.PropertyName}' contains invalid characters. " +
					"Only alphanumeric characters and underscores are allowed, starting with a letter.",
					nameof(filters));
			}

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

		// Validate OrderBy property name to prevent SQL injection via JSON path
		if (!ValidPropertyNameRegex().IsMatch(options.OrderBy))
		{
			throw new ArgumentException(
				$"OrderBy property name '{options.OrderBy}' contains invalid characters. " +
				"Only alphanumeric characters and underscores are allowed, starting with a letter.",
				nameof(options));
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
