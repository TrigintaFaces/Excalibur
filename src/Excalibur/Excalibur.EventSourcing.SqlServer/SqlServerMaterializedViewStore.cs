// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.Data.Validation;

using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores materialized views as JSON documents in SQL Server with the following schema:
/// <list type="bullet">
/// <item><c>MaterializedViews</c> table for view data</item>
/// <item><c>MaterializedViewPositions</c> table for position tracking</item>
/// </list>
/// </para>
/// <para>
/// Both tables are partitioned by tenant. The tenant term is part of each table's uniqueness
/// constraint rather than a filter applied over it, so two tenants projecting the same named view hold
/// distinct rows and distinct checkpoints. See <see cref="EnsureSchemaAsync"/> for the emitted DDL and
/// for why the natural key is a UNIQUE constraint rather than the clustered key.
/// </para>
/// <para>
/// <b>Performance Note:</b> Methods return <see cref="ValueTask{TResult}"/> to avoid heap allocations
/// for common patterns where the operation completes synchronously or is already cached.
/// </para>
/// </remarks>
public sealed partial class SqlServerMaterializedViewStore : IAtomicMaterializedViewStore
{
	private const string DefaultViewTableName = "MaterializedViews";
	private const string DefaultPositionTableName = "MaterializedViewPositions";

	private readonly Func<SqlConnection> _connectionFactory;
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
	private readonly ILogger<SqlServerMaterializedViewStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="viewTableName">Optional view table name. Defaults to "MaterializedViews".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "MaterializedViewPositions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public SqlServerMaterializedViewStore(
		string connectionString,
		ILogger<SqlServerMaterializedViewStore> logger,
		ITenantContext tenantContext,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
		: this(CreateConnectionFactory(connectionString), logger, tenantContext, viewTableName, positionTableName, jsonOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerMaterializedViewStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="viewTableName">Optional view table name. Defaults to "MaterializedViews".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "MaterializedViewPositions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public SqlServerMaterializedViewStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerMaterializedViewStore> logger,
		ITenantContext tenantContext,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_viewTableName = viewTableName ?? DefaultViewTableName;
		_positionTableName = positionTableName ?? DefaultPositionTableName;
		SqlIdentifierValidator.ThrowIfInvalid(_viewTableName, nameof(viewTableName));
		SqlIdentifierValidator.ThrowIfInvalid(_positionTableName, nameof(positionTableName));
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
			SELECT Data FROM [{_viewTableName}]
			WHERE TenantId = @TenantId AND ViewName = @ViewName AND ViewId = @ViewId
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var json = await connection.QuerySingleOrDefaultAsync<string>(
			new CommandDefinition(
				sql,
				new { TenantId = ResolveTenantKey(), ViewName = viewName, ViewId = viewId },
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
			MERGE [{_viewTableName}] WITH (UPDLOCK, HOLDLOCK) AS target
			USING (SELECT @TenantId AS TenantId, @ViewName AS ViewName, @ViewId AS ViewId, @Data AS Data, @UpdatedAt AS UpdatedAt) AS source
			ON target.TenantId = source.TenantId AND target.ViewName = source.ViewName AND target.ViewId = source.ViewId
			WHEN MATCHED THEN
				UPDATE SET Data = source.Data, UpdatedAt = source.UpdatedAt
			WHEN NOT MATCHED THEN
				INSERT (TenantId, ViewName, ViewId, Data, CreatedAt, UpdatedAt)
				VALUES (source.TenantId, source.ViewName, source.ViewId, source.Data, source.UpdatedAt, source.UpdatedAt);
			""";

		var json = JsonSerializer.Serialize(view, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { TenantId = ResolveTenantKey(), ViewName = viewName, ViewId = viewId, Data = json, UpdatedAt = now },
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
			DELETE FROM [{_viewTableName}]
			WHERE TenantId = @TenantId AND ViewName = @ViewName AND ViewId = @ViewId
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				// The tenant term is not optional on a delete: without it this statement removes whichever tenant
				// happens to hold that (ViewName, ViewId), which is not necessarily the caller.
				new { TenantId = ResolveTenantKey(), ViewName = viewName, ViewId = viewId },
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

		// ROWLOCK hint: prevents page-level escalation from blocking other views'
		// position reads. Consistent with the UPDLOCK pattern in SavePositionAsync.
		var sql = $"""
			SELECT Position FROM [{_positionTableName}] WITH (ROWLOCK)
			WHERE TenantId = @TenantId AND ViewName = @ViewName
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var position = await connection.QuerySingleOrDefaultAsync<long?>(
			new CommandDefinition(
				sql,
				new { TenantId = ResolveTenantKey(), ViewName = viewName },
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

		// UPDLOCK + HOLDLOCK is the pair, not either alone: HOLDLOCK holds the key range for the duration of
		// the statement so a concurrent insert of the same key cannot slip between the match and the write,
		// and UPDLOCK makes that range lock an UPDATE lock rather than a SHARED one. With HOLDLOCK alone two
		// processors upserting the same view each hold a shared range lock and each need to convert it to
		// exclusive, so the engine breaks the cycle by killing one as a deadlock victim. ROWLOCK keeps the
		// granularity at the row so contention stays within a view rather than across views.
		// `source.Position > target.Position` makes the advance monotonic, matching the position write inside
		// SaveViewAndPositionAsync. Without it a delayed or retried write carrying an older position rewinds the
		// checkpoint, and the projection replays events it has already applied. Both methods write this same row,
		// so both must enforce the invariant: a guarantee held by only one of a state's writers is not held.
		var sql = $"""
			MERGE [{_positionTableName}] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) AS target
			USING (SELECT @TenantId AS TenantId, @ViewName AS ViewName, @Position AS Position, @UpdatedAt AS UpdatedAt) AS source
			ON target.TenantId = source.TenantId AND target.ViewName = source.ViewName
			WHEN MATCHED AND source.Position > target.Position THEN
				UPDATE SET Position = source.Position, UpdatedAt = source.UpdatedAt
			WHEN NOT MATCHED THEN
				INSERT (TenantId, ViewName, Position, CreatedAt, UpdatedAt)
				VALUES (source.TenantId, source.ViewName, source.Position, source.UpdatedAt, source.UpdatedAt);
			""";

		var now = DateTimeOffset.UtcNow;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { TenantId = ResolveTenantKey(), ViewName = viewName, Position = position, UpdatedAt = now },
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
		// can never leave the view updated while the position lags (or vice versa) — the exactly-once
		// contract. The position advance is monotonic: a lower position never overwrites a higher one.
		var viewSql = $"""
			MERGE [{_viewTableName}] WITH (UPDLOCK, HOLDLOCK) AS target
			USING (SELECT @TenantId AS TenantId, @ViewName AS ViewName, @ViewId AS ViewId, @Data AS Data, @UpdatedAt AS UpdatedAt) AS source
			ON target.TenantId = source.TenantId AND target.ViewName = source.ViewName AND target.ViewId = source.ViewId
			WHEN MATCHED THEN
				UPDATE SET Data = source.Data, UpdatedAt = source.UpdatedAt
			WHEN NOT MATCHED THEN
				INSERT (TenantId, ViewName, ViewId, Data, CreatedAt, UpdatedAt)
				VALUES (source.TenantId, source.ViewName, source.ViewId, source.Data, source.UpdatedAt, source.UpdatedAt);
			""";

		var positionSql = $"""
			MERGE [{_positionTableName}] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) AS target
			USING (SELECT @TenantId AS TenantId, @ViewName AS ViewName, @Position AS Position, @UpdatedAt AS UpdatedAt) AS source
			ON target.TenantId = source.TenantId AND target.ViewName = source.ViewName
			WHEN MATCHED AND source.Position > target.Position THEN
				UPDATE SET Position = source.Position, UpdatedAt = source.UpdatedAt
			WHEN NOT MATCHED THEN
				INSERT (TenantId, ViewName, Position, CreatedAt, UpdatedAt)
				VALUES (source.TenantId, source.ViewName, source.Position, source.UpdatedAt, source.UpdatedAt);
			""";

		var json = JsonSerializer.Serialize(view, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		// Resolved ONCE, outside the transaction: the view row and the checkpoint recording how far that view
		// has been built must land in the SAME partition. Resolving per statement would let an ambient change
		// between them file the view under one tenant and its checkpoint under another.
		var tenantKey = ResolveTenantKey();

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					viewSql,
					new { TenantId = tenantKey, ViewName = viewName, ViewId = viewId, Data = json, UpdatedAt = now },
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
					new { TenantId = tenantKey, ViewName = viewName, Position = position, UpdatedAt = now },
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
	/// Ensures the materialized view tables exist in the database. Creates them if they do not exist.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// <para>
	/// Creates two tables:
	/// <list type="bullet">
	/// <item>
	/// <c>[ViewTableName]</c> — Stores serialized view data with composite key (ViewName, ViewId).
	/// </item>
	/// <item>
	/// <c>[PositionTableName]</c> — Tracks the last processed global stream position per view,
	/// enabling catch-up and rebuild scenarios.
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	/// This method is idempotent — it uses <c>IF NOT EXISTS</c> guards and can be called
	/// safely at application startup.
	/// </para>
	/// </remarks>
	public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
	{
		// INDEX KEY WIDTH -- STATED, BECAUSE IT IS WHAT DECIDES THE SHAPE BELOW.
		//
		// SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. The natural key
		// of each table is now tenant-qualified, and at these widths (2 bytes per NVARCHAR character):
		//
		//     views      TenantId NVARCHAR(64) -> 128   ViewName NVARCHAR(256) -> 512   ViewId NVARCHAR(256) -> 512
		//                                                                         total 1152 bytes
		//     positions  TenantId NVARCHAR(64) -> 128   ViewName NVARCHAR(256) -> 512   total 640 bytes
		//
		// The view table's natural key exceeds 900, so it cannot be the clustered key. The positions key now
		// fits under 900, but it keeps the same surrogate-clustered shape: the two tables are provisioned and
		// upgraded together, and a table already created by an earlier revision of this DDL carries the
		// surrogate key regardless, so diverging here would leave the same table in two shapes depending on
		// when it was created. Each table therefore takes a surrogate identity as its clustered key and
		// enforces its natural key with a UNIQUE constraint, which carries the same guarantee under the
		// 1700-byte cap. This is the shape the CDC state store uses, for this same reason.
		//
		// The view table was ALREADY over the limit before tenancy: (ViewName, ViewId) alone is 1024 bytes as a
		// clustered key. That failure is quiet in the worst way -- CREATE TABLE succeeds with only a warning,
		// and the table then refuses oversized inserts at run time with Msg 1946. So this change also removes a
		// pre-existing latent fault rather than introducing a new constraint.
		//
		// TenantId is NOT NULL with no DEFAULT and carries a binary collation. No DEFAULT because it is a
		// component of identity, and you do not default a key column: with one, a write that omitted the tenant
		// would land silently in the untenanted partition, making "I forgot to supply the tenant"
		// indistinguishable from "this row is deliberately untenanted". Latin1_General_BIN2 because the database
		// default is typically case-INSENSITIVE, under which 'Acme' and 'acme' would be the same tenant and a
		// scoped read would return another tenant's view -- the predicate failing OPEN.
		var sql = $"""
			IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{_viewTableName}')
			BEGIN
				CREATE TABLE [{_viewTableName}] (
					Id         BIGINT IDENTITY(1,1) NOT NULL,
					TenantId   NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
					ViewName   NVARCHAR(256)  NOT NULL,
					ViewId     NVARCHAR(256)  NOT NULL,
					Data       NVARCHAR(MAX)  NOT NULL,
					CreatedAt  DATETIMEOFFSET NOT NULL,
					UpdatedAt  DATETIMEOFFSET NOT NULL,
					CONSTRAINT PK_{_viewTableName} PRIMARY KEY CLUSTERED (Id),
					CONSTRAINT UQ_{_viewTableName}_Key UNIQUE (TenantId, ViewName, ViewId)
				);
			END

			IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{_positionTableName}')
			BEGIN
				CREATE TABLE [{_positionTableName}] (
					Id         BIGINT IDENTITY(1,1) NOT NULL,
					TenantId   NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
					ViewName   NVARCHAR(256)  NOT NULL,
					Position   BIGINT         NOT NULL,
					CreatedAt  DATETIMEOFFSET NOT NULL,
					UpdatedAt  DATETIMEOFFSET NOT NULL,
					CONSTRAINT PK_{_positionTableName} PRIMARY KEY CLUSTERED (Id),
					CONSTRAINT UQ_{_positionTableName}_Key UNIQUE (TenantId, ViewName)
				);
			END
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(sql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogSchemaEnsured(_viewTableName, _positionTableName);
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

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new SqlConnection(connectionString);
	}

	#region Logging

	[LoggerMessage(
		EventId = 3100,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} loaded")]
	private partial void LogViewLoaded(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3101,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} not found")]
	private partial void LogViewNotFound(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3102,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} saved")]
	private partial void LogViewSaved(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3103,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} deleted")]
	private partial void LogViewDeleted(string viewName, string viewId);

	[LoggerMessage(
		EventId = 3104,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} loaded: {Position}")]
	private partial void LogPositionLoaded(string viewName, long position);

	[LoggerMessage(
		EventId = 3105,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} saved: {Position}")]
	private partial void LogPositionSaved(string viewName, long position);

	[LoggerMessage(
		EventId = 3106,
		Level = LogLevel.Information,
		Message = "Materialized view schema ensured: tables [{ViewTable}] and [{PositionTable}]")]
	private partial void LogSchemaEnsured(string viewTable, string positionTable);

	#endregion
}
