// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres.Requests;

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
public sealed partial class PostgresProjectionStore<TProjection> : ITransactionalProjectionStore<TProjection>, IProjectionStore<TProjection>, IProjectionGenerationReaderFactory<TProjection>, IAotProjectionGenerationReaderFactory<TProjection>,
	IPositionedProjectionStore<TProjection>
	where TProjection : class
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly string _tableName;
	private readonly string _qualifiedTable;
	private readonly string _destinationIdentity;
	private readonly ILogger<PostgresProjectionStore<TProjection>> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ITenantContext _tenantContext;

	// Row-level tenant discriminator predicate, single-sourced so the Postgres column name and
	// parameter (tenant_id = @TenantId) are not repeated across the read/delete/query builders.
	private const string TenantColumnPredicate = "tenant_id = @TenantId";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tableName">Optional table name. Defaults to projection type name in snake_case.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	/// <param name="schemaName">
	/// The schema holding the table. Defaults to <c>public</c>. Honoured by every statement this store
	/// runs: a schema setting the data access does not use reads as supported and does nothing. Both
	/// identifiers are quoted, so their case is preserved exactly as configured.
	/// </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// </remarks>
	public PostgresProjectionStore(
		string connectionString,
		ILogger<PostgresProjectionStore<TProjection>> logger,
		ITenantContext tenantContext,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null,
		string schemaName = "public")
		: this(CreateDataSource(connectionString), logger, tenantContext, tableName, jsonOptions, schemaName)
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
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="schemaName">
	/// The schema holding the table. Defaults to <c>public</c>. Honoured by every statement this store
	/// runs: a schema setting the data access does not use reads as supported and does nothing. Both
	/// identifiers are quoted, so their case is preserved exactly as configured.
	/// </param>
	/// <remarks>
	/// This is the advanced constructor for scenarios requiring custom connection management.
	/// </remarks>
	public PostgresProjectionStore(
		NpgsqlDataSource dataSource,
		ILogger<PostgresProjectionStore<TProjection>> logger,
		ITenantContext tenantContext,
		string? tableName = null,
		JsonSerializerOptions? jsonOptions = null,
		string schemaName = "public")
	{
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_tableName = tableName ?? ToSnakeCase(typeof(TProjection).Name);
		if (!ValidIdentifierRegex().IsMatch(_tableName))
		{
			throw new ArgumentException($"Table name '{_tableName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.", nameof(tableName));
		}

		if (schemaName is null || !ValidIdentifierRegex().IsMatch(schemaName))
		{
			throw new ArgumentException($"Schema name '{schemaName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.", nameof(schemaName));
		}

		// THE DECLARED GENERATION PARTICIPATES IN THE TABLE, so a change to the read model's shape or
		// fold addresses a new table rather than mutating the one already serving reads: the new
		// generation folds from zero while the previous one keeps its cursor until the consumer cuts
		// over. Applied AFTER the identifier check, so a rejection still names the table the consumer
		// configured rather than one with a suffix they never wrote; the suffix this adds is composed
		// from digits and the underscore separator and so is always a legal identifier.
		//
		// AN UNDERSCORE, NOT THE HYPHEN A SEARCH INDEX USES. A hyphen is not legal in an unquoted SQL
		// identifier and would be rejected by the check above, so cross-provider name parity is not
		// available here and is not the contract -- each provider places the segment where it genuinely
		// addresses a distinct destination.
		_tableName = ProjectionVersionConvention.Qualify(_tableName, typeof(TProjection), '_');

		// QUALIFIED ONCE, and used by every statement. Addressing the table unqualified made it resolve
		// through the connection's search_path -- which this store neither sets nor reads -- so two
		// deployments with the same table name under different search paths addressed different tables
		// and were indistinguishable to the framework.
		//
		// CASE IS PRESERVED, NOT FOLDED. Postgres folds an UNQUOTED identifier to lower case but keeps
		// the case inside double quotes, and both identifiers are quoted here. "MyTable" and "mytable"
		// are therefore two different tables, so normalising the case would address a table the
		// consumer did not configure.
		// Composed by the package's single identifier-quoting helper, the same one the snapshot, outbox and
		// materialized-view statements use, so a change to how identifiers are quoted cannot apply to some
		// statements and not others.
		_qualifiedTable = PgTableName.Format(schemaName, _tableName);

		// The LOGICAL target, from the same resolved schema and table every statement addresses, and
		// never re-derived by taking the composed name apart. The RESOLVED default goes in, not an empty
		// segment, so a consumer who later writes "public" explicitly keeps the same identity. No host,
		// database or credential: a consumer's environments address one logical destination.
		_destinationIdentity = $"postgres/{schemaName}/{_tableName}";
		// Read-model serialization — intentionally NOT the event canonical contract (a view is not an event;
		// consumer-injectable). The projection read-model contract preserves numeric enums: this JSON is a
		// queryable, consumer-facing surface (SQL/search filters) where enum-as-string would break
		// range/equality queries. Sourced from the shared ProjectionSerializationDefaults factory so every
		// projection store persists a consistent document shape.
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
		var sql = $"SELECT data FROM {_qualifiedTable} WHERE id = @Id{tenantPredicate} AND (last_applied_position IS NULL OR last_applied_position <> @Retired)";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Retired", ProjectionPosition.RetiredSentinel);
		parameters.Add("@TenantId", tenantId);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		await using var command = CreateReadCommand(connection, sql, parameters);
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

		// Tenant scoping rides the upsert atomically: the tenant discriminator is stamped on INSERT and is
		// THE POSITION IS INVALIDATED, NOT PRESERVED, AND THAT IS THE WHOLE POINT OF THE -1.
		//
		// An unconditional write replaces the state with something this store cannot place in the event
		// stream: it was not folded from any known prefix. Leaving the old position behind would leave
		// the row asserting "everything up to P is folded into me" about a state that no longer contains
		// it, and the next position-conditional write would then FILTER OUT events as already-applied
		// that are not applied at all. Those events are lost from the projection permanently, and
		// nothing downstream can detect it.
		//
		// Resetting to the sentinel says the honest thing: nobody knows what prefix this state
		// represents. The next positioned write is then REFUSED rather than applied -- it has no number to
		// advance from -- and the projection stops advancing until it is rebuilt. That is the deliberate
		// trade: a loud refusal an operator can act on, in place of a silent re-fold that over-counted an
		// accumulating projection every time an unconditional write landed on the row. What invalidation
		// buys is that the row becomes self-describing as "prefix unknown"; preserving the stale position
		// destroys the only evidence that anything is wrong. The document stores reach the same end state by omitting the
		// field from a whole-document replacement; this makes the relational stores agree rather than
		// differ silently.
		//
		// part of the ON CONFLICT target, so a tenant-A upsert can never conflict with (and overwrite) a
		// tenant-B row that shares the same id — they are distinct (id, tenant_id) rows. Tenant-facing write —
		// fails closed: a null/blank ambient tenant is rejected up front. (Requires a composite UNIQUE
		// (id, tenant_id) in the tenant-scoped table DDL.)
		var insertTenantColumn = ", tenant_id";
		var insertTenantValue = ", @TenantId";
		var conflictTarget = "(id, tenant_id)";

		// The UPDATE arm records WHY this row has no position. `-1` alone cannot say: the column default
		// also means `-1`, and that means "never positioned", where the stored state IS a complete fold.
		// This arm means "a position was destroyed", where the state is a fold over no known prefix at all.
		// Neither can be advanced from, so the distinction does not decide the next write -- it decides
		// what the row can honestly be said to hold while it waits to be rebuilt, and what an operator
		// reading it should conclude.
		//
		// BOTH arms write it, and that is the simplification. This method cannot know whether the state
		// it was handed is a fold over any prefix, so it records that it does not know -- identically
		// whether the row is created or replaced. Discriminating create from update would only be needed
		// in order to GUESS, and four of the document providers cannot do it atomically at all. A caller
		// that does hold a complete fold says so through UpsertUnnumberedAsync instead.
		var sql = $"""
		           INSERT INTO {_qualifiedTable} AS target (id, data, created_at, updated_at, last_applied_position{insertTenantColumn})
		           VALUES (@Id, @Data::jsonb, @UpdatedAt, @UpdatedAt, @Unplaceable{insertTenantValue})
		           ON CONFLICT {conflictTarget}
		           DO UPDATE SET data = EXCLUDED.data, updated_at = EXCLUDED.updated_at,
		                         last_applied_position = EXCLUDED.last_applied_position
		           WHERE target.last_applied_position IS DISTINCT FROM @Retired
		           """;

		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Retired", ProjectionPosition.RetiredSentinel);
		parameters.Add("@Data", json);
		parameters.Add("@UpdatedAt", now);
		parameters.Add("@Unplaceable", ProjectionPosition.Unplaceable.ToStored());
		parameters.Add("@TenantId", tenantId);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

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
            INSERT INTO {_qualifiedTable} (id, data, created_at, updated_at, tenant_id, last_applied_position)
            VALUES (@Id, @Data::jsonb, @UpdatedAt, @UpdatedAt, @TenantId, @Retired)
            ON CONFLICT (id, tenant_id) DO UPDATE
            SET data = EXCLUDED.data, updated_at = EXCLUDED.updated_at,
                last_applied_position = EXCLUDED.last_applied_position;
            """;

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Data", "{}");
		parameters.Add("@Retired", ProjectionPosition.RetiredSentinel);
		parameters.Add("@UpdatedAt", DateTimeOffset.UtcNow);
		parameters.Add("@TenantId", tenantId);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
				new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

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
		           SELECT data FROM {_qualifiedTable}
		           {whereClause}
		           {orderByClause}
		           {paginationClause}
		           """;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		await using var command = CreateReadCommand(connection, sql, parameters);
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
		           SELECT COUNT(*) FROM {_qualifiedTable}
		           {whereClause}
		           """;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		await using var command = CreateReadCommand(connection, sql, parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
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

	/// <summary>
	/// Builds the filter WHERE clause and appends the row-level tenant predicate
	/// (<c>tenant_id = @TenantId</c>) so every read is scoped to the current tenant in the same statement.
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
		whereClause += " AND (last_applied_position IS NULL OR last_applied_position <> @Retired)";

		return (whereClause, parameters);
	}

	// Schema and table names are interpolated into the SQL, so they are restricted rather than
	// quoted-and-hoped: a quote or a dot inside an identifier would escape the quoting and make the
	// destination ambiguous. Matches the restriction the shipped CREATE TABLE generator applies.
	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex ValidIdentifierRegex();

	[GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]*$")]
	private static partial Regex ValidPropertyNameRegex();

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

			// Validate the property name to prevent SQL injection via the JSON path (parity with the
			// SqlServer sibling): the name is interpolated into the statement, so a non-identifier name
			// could break out of the data->>'…' literal. Covers the In operator too — BuildInCondition
			// receives the jsonPath derived from this validated name.
			if (!ValidPropertyNameRegex().IsMatch(parsed.PropertyName))
			{
				throw new ArgumentException(
					$"Filter property name '{parsed.PropertyName}' contains invalid characters. " +
					"Only alphanumeric characters and underscores are allowed, starting with a letter.",
					nameof(filters));
			}

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

		// Validate the OrderBy property name to prevent SQL injection via the JSON path (parity with the
		// SqlServer sibling): the name is interpolated into the ORDER BY clause.
		if (!ValidPropertyNameRegex().IsMatch(options.OrderBy))
		{
			throw new ArgumentException(
				$"OrderBy property name '{options.OrderBy}' contains invalid characters. " +
				"Only alphanumeric characters and underscores are allowed, starting with a letter.",
				nameof(options));
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

	/// <inheritdoc />
	/// <remarks>
	/// Delegated so the conditional SQL lives in one place: the statement carries a concurrency
	/// contract, and a second copy of it is a second chance to get it wrong.
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
			// The schema and table exactly as every statement in this store emits them, composed once in
			// the constructor from the same resolved values. Both this type and
			// PostgresPositionedProjectionStore double-quote both identifiers, so the server preserves
			// their case and a case-only configuration change really is a different table -- the identity
			// therefore preserves case rather than folding it.
			return new ConfiguredDestinationIdentity(_destinationIdentity);
		}

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	private sealed class ConfiguredDestinationIdentity(string identity) : IProjectionDestinationIdentity
	{
		public string Identity { get; } = identity;
	}

	private PostgresPositionedProjectionStore<TProjection> Positioned() =>
		new(_dataSource.CreateConnection, _qualifiedTable);

	/// <summary>
	/// The tenant term every statement in this store is partitioned by, resolved exactly as the
	/// unconditional paths resolve it so a conditional write cannot read one tenant and write another.
	/// </summary>
	private string RequireTenant() => TenantScope.Scoped(_tenantContext.TenantId).TenantId;
}
