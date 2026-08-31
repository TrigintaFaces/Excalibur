// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="ICursorMapStore"/> using a key-value table.
/// </summary>
/// <remarks>
/// <para>
/// Uses a <c>ProjectionCursorMaps</c> table with columns
/// <c>(ProjectionName, StreamId, Position)</c>. Saves are atomic via MERGE.
/// </para>
/// <para>
/// This store issues no DDL at run time. Provision the table from the
/// <c>008_CreateCursorMapSchema.sql</c> script shipped in this package's <c>scripts</c> folder
/// before the first checkpoint; without it every save fails with
/// <c>Msg 208, Invalid object name 'ProjectionCursorMaps'</c>.
/// </para>
/// <para>
/// Table DDL, abridged — run the script rather than transcribing this. The script carries the
/// binary collation on the three key columns and the non-clustered primary key, both of which are
/// load-bearing and neither of which is visible here: the natural key is 1152 bytes, over SQL
/// Server's 900-byte clustered limit, so a plain <c>PRIMARY KEY</c> creates a table that accepts
/// the definition and then refuses long rows at run time.
/// <code>
/// CREATE TABLE ProjectionCursorMaps (
///     TenantId NVARCHAR(64) NOT NULL,
///     ProjectionName NVARCHAR(256) NOT NULL,
///     StreamId NVARCHAR(256) NOT NULL,
///     Position BIGINT NOT NULL,
///     CONSTRAINT PK_ProjectionCursorMaps PRIMARY KEY NONCLUSTERED (TenantId, ProjectionName, StreamId)
/// );
/// </code>
/// </para>
/// </remarks>
public sealed class SqlServerCursorMapStore : ICursorMapStore
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerCursorMapStore> _logger;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private KeyedTenantPartition CurrentTenantPartition =>
		KeyedTenantPartition.FromContext(_tenantContext);


	/// <summary>
	/// Initializes a new instance with a connection string and an ambient tenant context.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public SqlServerCursorMapStore(
		string connectionString,
		ILogger<SqlServerCursorMapStore> logger,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString), logger, tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance with a connection factory and an ambient tenant context.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public SqlServerCursorMapStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerCursorMapStore> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_connectionFactory = connectionFactory;
		_logger = logger;
		_tenantContext = tenantContext;
	}

	/// <summary>Resolves the partition every cursor map is confined to.</summary>
	/// <returns>The reserved partition key for the ambient tenant.</returns>
	private string ResolveTenantKey() =>
		CurrentTenantPartition.TenantId;

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, long>> GetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<CursorMapRow>(
			"SELECT StreamId, Position FROM ProjectionCursorMaps "
			+ "WHERE TenantId = @TenantId AND ProjectionName = @ProjectionName",
			new { TenantId = ResolveTenantKey(), ProjectionName = projectionName }).ConfigureAwait(false);

		var result = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (var row in rows)
		{
			result[row.StreamId] = row.Position;
		}

		return result;
	}

	/// <inheritdoc />
	public async Task SaveCursorMapAsync(
		string projectionName,
		IReadOnlyDictionary<string, long> cursorMap,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);
		ArgumentNullException.ThrowIfNull(cursorMap);

		if (cursorMap.Count == 0)
		{
			return;
		}

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		// Resolved ONCE: every row of one save must land in the same partition.
		var tenantKey = ResolveTenantKey();

		foreach (var entry in cursorMap)
		{
			await connection.ExecuteAsync(
				"""
				MERGE ProjectionCursorMaps WITH (UPDLOCK, HOLDLOCK) AS target
				USING (SELECT @TenantId AS TenantId, @ProjectionName AS ProjectionName, @StreamId AS StreamId) AS source
				ON target.TenantId = source.TenantId
					AND target.ProjectionName = source.ProjectionName
					AND target.StreamId = source.StreamId
				WHEN MATCHED THEN UPDATE SET Position = @Position
				WHEN NOT MATCHED THEN INSERT (TenantId, ProjectionName, StreamId, Position)
					VALUES (@TenantId, @ProjectionName, @StreamId, @Position);
				""",
				new
				{
					TenantId = tenantKey,
					ProjectionName = projectionName,
					StreamId = entry.Key,
					Position = entry.Value
				},
				transaction).ConfigureAwait(false);
		}

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task ResetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await connection.ExecuteAsync(
			// The tenant term is NOT optional here: a reset without it deletes EVERY tenant's cursors for
			// the projection, and the next run reprojects the whole stream for all of them -- or loses
			// their positions outright if the source has since been truncated.
			"DELETE FROM ProjectionCursorMaps "
			+ "WHERE TenantId = @TenantId AND ProjectionName = @ProjectionName",
			new { TenantId = ResolveTenantKey(), ProjectionName = projectionName }).ConfigureAwait(false);
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrEmpty(connectionString);
		return () => new SqlConnection(connectionString);
	}

	private sealed record CursorMapRow(string StreamId, long Position);
}
