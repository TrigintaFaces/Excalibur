// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch;

using Dapper;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores materialized views as JSONB documents in Postgres with the following schema:
/// <list type="bullet">
/// <item><c>materialized_views</c> table for view data</item>
/// <item><c>materialized_view_positions</c> table for position tracking</item>
/// </list>
/// </para>
/// <para>
/// Both tables are partitioned by tenant. The tenant term is part of each table's key rather than a
/// filter applied over it, so two tenants projecting the same named view hold distinct rows and
/// distinct checkpoints:
/// <code>
/// CREATE TABLE materialized_views (
///     tenant_id  VARCHAR(64) NOT NULL,
///     view_name  VARCHAR(255) NOT NULL,
///     view_id    VARCHAR(255) NOT NULL,
///     data       JSONB        NOT NULL,
///     created_at TIMESTAMPTZ  NOT NULL,
///     updated_at TIMESTAMPTZ  NOT NULL,
///     CONSTRAINT pk_materialized_views PRIMARY KEY (tenant_id, view_name, view_id)
/// );
///
/// CREATE TABLE materialized_view_positions (
///     tenant_id  VARCHAR(64) NOT NULL,
///     view_name  VARCHAR(255) NOT NULL,
///     position   BIGINT       NOT NULL,
///     created_at TIMESTAMPTZ  NOT NULL,
///     updated_at TIMESTAMPTZ  NOT NULL,
///     CONSTRAINT pk_materialized_view_positions PRIMARY KEY (tenant_id, view_name)
/// );
/// </code>
/// </para>
/// <para>
/// Uses INSERT ... ON CONFLICT for thread-safe upsert operations and JSONB
/// for efficient JSON storage and querying.
/// </para>
/// <para>
/// <b>Performance Note:</b> Methods return <see cref="ValueTask{TResult}"/> to avoid heap allocations
/// for common patterns where the operation completes synchronously or is already cached.
/// </para>
/// </remarks>
public sealed partial class PostgresMaterializedViewStore : IAtomicMaterializedViewStore
{
	private const string DefaultViewTableName = "materialized_views";
	private const string DefaultPositionTableName = "materialized_view_positions";

	private readonly NpgsqlDataSource _dataSource;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private KeyedTenantPartition CurrentTenantPartition =>
		KeyedTenantPartition.FromContext(_tenantContext);

	/// <summary>
	/// Test-only fault hook invoked inside <see cref="SaveViewAndPositionAsync{TView}"/> AFTER the view
	/// upsert and BEFORE the position advance, within the same transaction. Default <see langword="null"/>
	/// (no-op) in production — zero behavior change. A test sets it to a throwing delegate to simulate a
	/// crash between the two writes and assert the transaction rolls BOTH back (exactly-once, no torn write).
	/// </summary>
	internal Func<CancellationToken, Task>? OnAfterViewBeforePositionAsync { get; set; }
	private readonly string _viewTableName;
	private readonly string _positionTableName;
	private readonly ILogger<PostgresMaterializedViewStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="viewTableName">Optional view table name. Defaults to "materialized_views".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "materialized_view_positions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public PostgresMaterializedViewStore(
		string connectionString,
		ILogger<PostgresMaterializedViewStore> logger,
		ITenantContext tenantContext,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
		: this(CreateDataSource(connectionString), logger, tenantContext, viewTableName, positionTableName, jsonOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresMaterializedViewStore"/> class with an NpgsqlDataSource.
	/// </summary>
	/// <param name="dataSource">
	/// An <see cref="NpgsqlDataSource"/> that manages connection pooling.
	/// Using NpgsqlDataSource is the recommended pattern per Npgsql documentation.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="viewTableName">Optional view table name. Defaults to "materialized_views".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "materialized_view_positions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public PostgresMaterializedViewStore(
		NpgsqlDataSource dataSource,
		ILogger<PostgresMaterializedViewStore> logger,
		ITenantContext tenantContext,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_viewTableName = viewTableName ?? DefaultViewTableName;
		_positionTableName = positionTableName ?? DefaultPositionTableName;
		// Read-model serialization — intentionally NOT the event canonical contract (a view is not an event;
		// consumer-injectable). The numeric-enum representation is preserved: this JSON is a queryable,
		// consumer-facing surface (SQL/search filters) where enum-as-string would break range/equality queries.
		_jsonOptions = jsonOptions ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};
	}
	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON deserialization might require runtime code generation.")]
	public async ValueTask<TView?> GetAsync<TView>(
		string viewName,
		string viewId,
		CancellationToken cancellationToken)
		where TView : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);

		var sql = $"""
			SELECT data FROM {_viewTableName}
			WHERE tenant_id = @tenant_id AND view_name = @view_name AND view_id = @view_id
			""";

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var json = await connection.QuerySingleOrDefaultAsync<string>(
			new CommandDefinition(
				sql,
				new { tenant_id = ResolveTenantKey(), view_name = viewName, view_id = viewId },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (json is null)
		{
			LogViewNotFound(viewName, viewId);
			return null;
		}

		LogViewLoaded(viewName, viewId);
		return JsonSerializer.Deserialize<TView>(json, _jsonOptions);
	}
	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization might require runtime code generation.")]
	public async ValueTask SaveAsync<TView>(
		string viewName,
		string viewId,
		TView view,
		CancellationToken cancellationToken)
		where TView : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
		ArgumentNullException.ThrowIfNull(view);

		var sql = $"""
			INSERT INTO {_viewTableName} (tenant_id, view_name, view_id, data, created_at, updated_at)
			VALUES (@tenant_id, @view_name, @view_id, @data::jsonb, @updated_at, @updated_at)
			ON CONFLICT (tenant_id, view_name, view_id)
			DO UPDATE SET data = EXCLUDED.data, updated_at = EXCLUDED.updated_at
			""";

		var json = JsonSerializer.Serialize(view, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { tenant_id = ResolveTenantKey(), view_name = viewName, view_id = viewId, data = json, updated_at = now },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogViewSaved(viewName, viewId);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteAsync(
		string viewName,
		string viewId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);

		var sql = $"""
			DELETE FROM {_viewTableName}
			WHERE tenant_id = @tenant_id AND view_name = @view_name AND view_id = @view_id
			""";

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				// The tenant term is not optional on a delete: without it this statement removes whichever tenant
				// happens to hold that (view_name, view_id), which is not necessarily the caller.
				new { tenant_id = ResolveTenantKey(), view_name = viewName, view_id = viewId },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			LogViewDeleted(viewName, viewId);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<long?> GetPositionAsync(
		string viewName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		var sql = $"""
			SELECT position FROM {_positionTableName}
			WHERE tenant_id = @tenant_id AND view_name = @view_name
			""";

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var position = await connection.QuerySingleOrDefaultAsync<long?>(
			new CommandDefinition(
				sql,
				new { tenant_id = ResolveTenantKey(), view_name = viewName },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (position.HasValue)
		{
			LogPositionLoaded(viewName, position.Value);
		}

		return position;
	}

	/// <inheritdoc/>
	public async ValueTask SavePositionAsync(
		string viewName,
		long position,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		// The WHERE clause makes the advance monotonic, matching the position upsert inside
		// SaveViewAndPositionAsync. Without it a delayed or retried write carrying an older position rewinds the
		// checkpoint, and the projection replays events it has already applied. Both methods write this same row,
		// so both must enforce the invariant: a guarantee held by only one of a state's writers is not held.
		var sql = $"""
			INSERT INTO {_positionTableName} (tenant_id, view_name, position, created_at, updated_at)
			VALUES (@tenant_id, @view_name, @position, @updated_at, @updated_at)
			ON CONFLICT (tenant_id, view_name)
			DO UPDATE SET position = EXCLUDED.position, updated_at = EXCLUDED.updated_at
			WHERE {_positionTableName}.position < EXCLUDED.position
			""";

		var now = DateTimeOffset.UtcNow;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { tenant_id = ResolveTenantKey(), view_name = viewName, position, updated_at = now },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogPositionSaved(viewName, position);
	}

	/// <inheritdoc/>
	/// <remarks>Always <see langword="true"/>: both writes run inside one SQL transaction, unconditionally.</remarks>
	public bool SupportsAtomicWrites => true;

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization might require runtime code generation.")]
	public async ValueTask SaveViewAndPositionAsync<TView>(
		string viewName,
		string viewId,
		TView view,
		long position,
		CancellationToken cancellationToken)
		where TView : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
		ArgumentNullException.ThrowIfNull(view);

		// Atomic: the view upsert and the checkpoint advance run in ONE transaction, so a crash
		// can never leave the view updated while the position lags (or vice versa) -- the exactly-once
		// contract. The position advance is monotonic: the DO UPDATE only fires when the new position is
		// strictly greater, so a lower position never overwrites a higher one.
		var viewSql = $"""
			INSERT INTO {_viewTableName} (tenant_id, view_name, view_id, data, created_at, updated_at)
			VALUES (@tenant_id, @view_name, @view_id, @data::jsonb, @updated_at, @updated_at)
			ON CONFLICT (tenant_id, view_name, view_id)
			DO UPDATE SET data = EXCLUDED.data, updated_at = EXCLUDED.updated_at
			""";

		var positionSql = $"""
			INSERT INTO {_positionTableName} (tenant_id, view_name, position, created_at, updated_at)
			VALUES (@tenant_id, @view_name, @position, @updated_at, @updated_at)
			ON CONFLICT (tenant_id, view_name)
			DO UPDATE SET position = EXCLUDED.position, updated_at = EXCLUDED.updated_at
			WHERE {_positionTableName}.position < EXCLUDED.position
			""";

		var json = JsonSerializer.Serialize(view, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		// Resolved ONCE, outside the transaction: the view row and the checkpoint recording how far that view
		// has been built must land in the SAME partition. Resolving per statement would let an ambient change
		// between them file the view under one tenant and its checkpoint under another.
		var tenantKey = ResolveTenantKey();

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					viewSql,
					new { tenant_id = tenantKey, view_name = viewName, view_id = viewId, data = json, updated_at = now },
					transaction,
					cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			if (OnAfterViewBeforePositionAsync is not null)
			{
				await OnAfterViewBeforePositionAsync(cancellationToken).ConfigureAwait(false);
			}

			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					positionSql,
					new { tenant_id = tenantKey, view_name = viewName, position, updated_at = now },
					transaction,
					cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}

		LogViewSaved(viewName, viewId);
		LogPositionSaved(viewName, position);
	}

	/// <summary>
	/// Resolves the partition every view row and every checkpoint is confined to.
	/// </summary>
	/// <remarks>
	/// Keyed on view name and view id alone, two tenants projecting the same named view shared one row: the
	/// later writer's data silently replaced the earlier one's, and a read returned whichever tenant wrote
	/// last. The position table was worse -- keyed on view name alone it held ONE checkpoint for every
	/// tenant, so one tenant's progress advanced another's and that tenant's projector skipped every event in
	/// between. The monotonic guard makes that permanent rather than transient: it exists to stop the
	/// checkpoint moving backwards, so the skipped range can never be re-read.
	/// </remarks>
	/// <returns>The reserved partition key for the ambient tenant.</returns>
	private string ResolveTenantKey() =>
		CurrentTenantPartition.TenantId;

	private static NpgsqlDataSource CreateDataSource(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return NpgsqlDataSource.Create(connectionString);
	}

	#region Logging

	[LoggerMessage(
		EventId = 3200,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} loaded")]
	private partial void LogViewLoaded(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3201,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} not found")]
	private partial void LogViewNotFound(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3202,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} saved")]
	private partial void LogViewSaved(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3203,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} deleted")]
	private partial void LogViewDeleted(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3204,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} loaded: {Position}")]
	private partial void LogPositionLoaded(string viewName, long position);

	[LoggerMessage(
		EventId = 3205,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} saved: {Position}")]
	private partial void LogPositionSaved(string viewName, long position);

	#endregion
}
