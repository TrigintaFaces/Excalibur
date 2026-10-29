// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer.Requests;

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
public sealed partial class SqlServerProjectionStore<TProjection> : IProjectionStore<TProjection>, IProjectionGenerationReaderFactory<TProjection>, IAotProjectionGenerationReaderFactory<TProjection>,
	IPageableProjectionStore<TProjection>,
	IPositionedProjectionStore<TProjection>
	where TProjection : class
{
	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex ValidTableNameRegex();

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _qualifiedTable;
	private readonly string _destinationIdentity;
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
	/// <param name="schemaName">
	/// The schema holding the table. Defaults to <c>dbo</c>. Honoured by every statement this store
	/// runs: a schema setting the data access does not use reads as supported and does nothing.
	/// </param>
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
		JsonSerializerOptions? jsonOptions = null,
		string schemaName = "dbo")
		: this(CreateConnectionFactory(connectionString), logger, tenantContext, tableName, jsonOptions, schemaName)
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
	/// <param name="schemaName">
	/// The schema holding the table. Defaults to <c>dbo</c>. Honoured by every statement this store
	/// runs: a schema setting the data access does not use reads as supported and does nothing.
	/// </param>
	/// <remarks>
	/// This is the advanced constructor for scenarios requiring custom connection management.
	/// </remarks>
	public SqlServerProjectionStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerProjectionStore<TProjection>> logger,
		ITenantContext tenantContext,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null,
		string schemaName = "dbo")
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

		if (!ValidTableNameRegex().IsMatch(schemaName))
		{
			throw new ArgumentException($"Schema name '{schemaName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.", nameof(schemaName));
		}

		// THE DECLARED GENERATION PARTICIPATES IN THE TABLE, so a change to the read model's shape or
		// fold addresses a new table rather than mutating the one already serving reads: the new
		// generation folds from zero while the previous one keeps its cursor until the consumer cuts
		// over. Applied AFTER the identifier check, so a rejection still names the table the consumer
		// configured rather than one with a suffix they never wrote.
		//
		// AN UNDERSCORE, NOT THE HYPHEN A SEARCH INDEX USES: the identifier check above permits only
		// alphanumerics and the underscore, so a hyphen would be rejected. Cross-provider name parity
		// is therefore not available here and is not the contract -- each provider places the segment
		// where it genuinely addresses a distinct destination.
		//
		// REASSIGNED ONCE, not applied twice: the qualified table and the destination identity below
		// both read this one local, so they cannot derive the generation differently. The schema is
		// left alone -- a generation moves the table, never the schema holding it.
		resolvedName = ProjectionVersionConvention.Qualify(resolvedName, typeof(TProjection), '_');

		// QUALIFIED ONCE, and used by every statement. Addressing the table unqualified made it resolve
		// through the connection's default schema, so a consumer who set a schema got a table the store
		// could not find -- the setting read as supported and did nothing.
		// Composed by the package's single identifier-quoting helper, the same one the event, snapshot and
		// materialized-view statements use, so a change to how identifiers are quoted cannot apply to some
		// statements and not others.
		_qualifiedTable = SqlTableName.Format(schemaName, resolvedName);

		// The LOGICAL target, from the same resolved schema and table every statement addresses. The schema
		// is part of it because SQL Server resolves an unqualified name through the caller's default schema,
		// so the same table name in two schemas is two destinations. The RESOLVED default goes in, not an
		// empty segment, so making the default explicit does not change the identity. No server, database or
		// credential: a consumer's environments address one logical destination.
		_destinationIdentity = $"sqlserver/{schemaName}/{resolvedName}";

		// Converged on the single ProjectionSerializationDefaults read-model factory (camelCase +
		// null-omission) so every projection store's wire shape stays byte-compatible on the read path.
		_jsonOptions = jsonOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions();
	}
	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken) =>
		GetByIdCoreAsync(id, json => JsonSerializer.Deserialize<TProjection>(json, _jsonOptions), cancellationToken);

	private async Task<TProjection?> GetByIdCoreAsync(string id, Func<string, TProjection?> deserialize, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		// Canonical fail-closed seam for an always-tenant-scoped store: Scoped() throws
		// TenantRequiredException when the ambient tenant is null/blank, so an unscoped (false-isolation)
		// query is inexpressible. This store has no legitimate unscoped mode — its writes always stamp the
		// TenantId discriminator — so it requires a tenant rather than degrading to default(TenantScope).
		var tenantId = TenantScope.Scoped(_tenantContext.TenantId).TenantId;
		var tenantPredicate = " AND " + TenantColumnPredicate;
		var sql = $"SELECT Data FROM {_qualifiedTable} WHERE Id = @Id{tenantPredicate} AND (LastAppliedPosition IS NULL OR LastAppliedPosition <> @Retired)";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Retired", ProjectionPosition.RetiredSentinel);
		parameters.Add("@TenantId", tenantId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = CreateStoreCommand(connection, sql, parameters);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var json = scalar is null or DBNull ? null : (string)scalar;

		if (json is null)
		{
			return null;
		}

		return deserialize(json);
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

		// THE POSITION IS INVALIDATED, NOT PRESERVED, AND THAT IS THE WHOLE POINT OF THE -1.
		//
		// An unconditional write replaces the state with something this store cannot place in the event
		// stream: it was not folded from any known prefix. Leaving the old position behind would leave
		// the row asserting "everything up to P is folded into me" about a state that no longer contains
		// it, and the next position-conditional write would then FILTER OUT events as already-applied
		// that are not applied at all. Those events are lost from the projection permanently, and
		// nothing downstream can detect it -- the row looks consistent and every individual value was
		// written correctly.
		//
		// Resetting to the sentinel says the honest thing instead: nobody knows what prefix this state
		// represents. The next positioned write is then REFUSED rather than applied -- it has no number to
		// advance from -- and the projection stops advancing until it is rebuilt. That is the deliberate
		// trade: a loud refusal an operator can act on, in place of a silent re-fold that over-counted an
		// accumulating projection every time an unconditional write landed on the row. What invalidation
		// buys is that the row becomes SELF-DESCRIBING as "prefix unknown". Preserving the stale
		// position destroys the only evidence that anything is wrong, and that evidence is what any
		// correct recovery has to start from.
		// The document stores reach the same end state by omitting the field from a whole-document
		// replacement; this makes the relational stores agree with them rather than differ silently.
		//
		// Tenant scoping rides the MERGE atomically: the tenant discriminator is part of the match key so a
		// tenant-A upsert can NEVER match (and overwrite) a tenant-B row with the same Id, and the INSERT
		// stamps the tenant. Tenant-facing write — fails closed: a null/blank ambient tenant is rejected up
		// front. This must never degrade to a client-side check — the isolation is in the match key itself.
		var sourceTenantSelect = ", @TenantId AS TenantId";
		var onTenantPredicate = " AND target.TenantId = source.TenantId";
		var insertTenantColumn = ", TenantId";
		var insertTenantValue = ", source.TenantId";

		var sql = $"""
			MERGE {_qualifiedTable} WITH (UPDLOCK, HOLDLOCK) AS target
			USING (SELECT @Id AS Id, @Data AS Data, @UpdatedAt AS UpdatedAt{sourceTenantSelect}) AS source
			ON target.Id = source.Id{onTenantPredicate}
			WHEN MATCHED AND (target.LastAppliedPosition IS NULL OR target.LastAppliedPosition <> @Retired) THEN
				UPDATE SET Data = source.Data, UpdatedAt = source.UpdatedAt,
						   LastAppliedPosition = @UnplaceableSentinel
			WHEN NOT MATCHED THEN
				INSERT (Id, Data, CreatedAt, UpdatedAt, LastAppliedPosition{insertTenantColumn})
				VALUES (source.Id, source.Data, source.UpdatedAt, source.UpdatedAt,
						@UnplaceableSentinel{insertTenantValue});
			""";

		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Retired", ProjectionPosition.RetiredSentinel);
		parameters.Add("@Data", json);
		parameters.Add("@UpdatedAt", now);
		parameters.Add("@TenantId", tenantId);
		// BOTH arms of the MERGE bind this, and that is the simplification. This method cannot know
		// whether the state it was handed is a fold over any prefix -- so it records that it does not
		// know, identically whether the row is created or replaced. Discriminating create from update
		// would be needed only in order to GUESS, and four of the document providers cannot do it
		// atomically at all. A caller that DOES hold a complete fold says so through
		// UpsertUnnumberedAsync instead.
		parameters.Add("@UnplaceableSentinel", ProjectionPosition.Unplaceable.ToStored());

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(
				new CommandDefinition(
					sql,
					parameters,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (affected == 0)
		{
			throw new InvalidOperationException("This projection key is retired. Use a new projection generation to rebuild it.");
		}

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
		var sql = $"""
            MERGE {_qualifiedTable} WITH (UPDLOCK, HOLDLOCK) AS target
            USING (SELECT @Id AS Id, @TenantId AS TenantId) AS source
            ON target.Id = source.Id AND target.TenantId = source.TenantId
            WHEN MATCHED THEN
                UPDATE SET Data = @Data, UpdatedAt = @UpdatedAt, LastAppliedPosition = @Retired
            WHEN NOT MATCHED THEN
                INSERT (Id, Data, CreatedAt, UpdatedAt, TenantId, LastAppliedPosition)
                VALUES (@Id, @Data, @UpdatedAt, @UpdatedAt, @TenantId, @Retired);
            """;

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Data", "{}");
		parameters.Add("@Retired", ProjectionPosition.RetiredSentinel);
		parameters.Add("@UpdatedAt", DateTimeOffset.UtcNow);
		parameters.Add("@TenantId", tenantId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = CreateStoreCommand(connection, sql, parameters);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Deleted projection {ProjectionType}/{Id}", typeof(TProjection).Name, id);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken) =>
		QueryCoreAsync(filters, options, json => JsonSerializer.Deserialize<TProjection>(json, _jsonOptions), cancellationToken);

	private async Task<IReadOnlyList<TProjection>> QueryCoreAsync(IDictionary<string, object>? filters, QueryOptions? options, Func<string, TProjection?> deserialize, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = BuildTenantScopedWhereClause(filters);
		var orderByClause = BuildOrderByClause(options);
		var paginationClause = BuildPaginationClause(options, parameters);

		var sql = $"""
		           SELECT Data FROM {_qualifiedTable}
		           {whereClause}
		           {orderByClause}
		           {paginationClause}
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = CreateStoreCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<TProjection>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { results.Add(deserialize(reader.GetString(0))!); }
        return results;
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = BuildTenantScopedWhereClause(filters);

		var sql = $"""
		           SELECT COUNT_BIG(*) FROM {_qualifiedTable}
		           {whereClause}
		           """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = CreateStoreCommand(connection, sql, parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Uses a single-roundtrip SQL query with <c>COUNT_BIG(*) OVER()</c> window function
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
        var offset = checked((pageNumber - 1) * pageSize);

		var (whereClause, parameters) = BuildTenantScopedWhereClause(filters);
		var orderByClause = BuildOrderByClause(options);

		parameters.Add("@PageOffset", offset);
		parameters.Add("@PageSize", pageSize);

		// Single-roundtrip: COUNT_BIG(*) OVER() returns total count alongside each row.
		// When no rows match, we still need the count, so use a CTE approach.
		var sql = $"""
			SELECT Data, COUNT_BIG(*) OVER() AS TotalCount
			FROM {_qualifiedTable}
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
			var countSql = $"SELECT COUNT_BIG(*) FROM {_qualifiedTable} {whereClause}";
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
	/// Row type for paged query results including the total count from <c>COUNT_BIG(*) OVER()</c>.
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

		parameters.Add("@Retired", ProjectionPosition.RetiredSentinel);
		whereClause += " AND (LastAppliedPosition IS NULL OR LastAppliedPosition <> @Retired)";

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

	/// <inheritdoc />
	/// <remarks>
	/// Delegated so the conditional SQL lives in one place rather than being inlined here: the
	/// statement carries a concurrency contract, and a second copy of it is a second chance to get it
	/// wrong.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		CancellationToken cancellationToken)
	{
		// Materialized HERE, through the same canonical read-model options every other read on this
		// store uses. The positioned helper owns the conditional SQL and nothing about the wire shape.
		var (data, position) = await Positioned()
			.GetWithPositionAsync(id, RequireTenant(), cancellationToken)
			.ConfigureAwait(false);

		return (data is null ? null : JsonSerializer.Deserialize<TProjection>(data, _jsonOptions), position);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task UpsertUnnumberedAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken) =>
		Positioned().UpsertUnnumberedAsync(
			id, JsonSerializer.Serialize(projection, _jsonOptions), RequireTenant(), cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
		string id,
		TProjection projection,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken) =>
		Positioned().UpsertAtPositionAsync(
			id,
			JsonSerializer.Serialize(projection, _jsonOptions),
			expectedPosition,
			newPosition,
			RequireTenant(),
			cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
		string id,
		TProjection projection,
		long atPosition,
		CancellationToken cancellationToken) =>
		Positioned().RefoldAtPositionAsync(
			id,
			JsonSerializer.Serialize(projection, _jsonOptions),
			atPosition,
			RequireTenant(),
			cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<ProjectionRebuildResult> RebuildAtPositionAsync(
		string id,
		TProjection projection,
		long newPosition,
		CancellationToken cancellationToken) =>
		Positioned().RebuildAtPositionAsync(
			id,
			JsonSerializer.Serialize(projection, _jsonOptions),
			newPosition,
			RequireTenant(),
			cancellationToken);

	private SqlServerPositionedProjectionStore<TProjection> Positioned() =>
		new(_connectionFactory, _qualifiedTable);

	/// <summary>
	/// The tenant term every statement in this store is partitioned by.
	/// </summary>
	/// <remarks>
	/// Resolved the same way the unconditional paths resolve it, so the conditional write cannot end up
	/// reading one tenant's row and writing another's.
	/// </remarks>
	private string RequireTenant() => TenantScope.Scoped(_tenantContext.TenantId).TenantId;

	/// <inheritdoc cref="IServiceProvider.GetService" />
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IProjectionDestinationScope))
        {
            var scope = ReadDestinationScope();
            return scope is null ? null : new ConfiguredDestinationScope(scope);
        }

		if (serviceType == typeof(IProjectionTenantBoundary)) { return ProjectionTenantBoundary.Partition(_tenantContext.TenantId!); }
		if (serviceType == typeof(IProjectionDestinationIdentity))
		{
			return new ConfiguredDestinationIdentity(_destinationIdentity);
		}

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	private sealed class ConfiguredDestinationIdentity(string identity) : IProjectionDestinationIdentity
	{
		public string Identity { get; } = identity;
	}
}
