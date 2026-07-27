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
/// Table DDL:
/// <code>
/// CREATE TABLE ProjectionCursorMaps (
///     TenantId NVARCHAR(256) NOT NULL,
///     ProjectionName NVARCHAR(256) NOT NULL,
///     StreamId NVARCHAR(256) NOT NULL,
///     Position BIGINT NOT NULL,
///     CONSTRAINT PK_ProjectionCursorMaps PRIMARY KEY (TenantId, ProjectionName, StreamId)
/// );
/// </code>
/// </para>
/// </remarks>
public sealed class SqlServerCursorMapStore : ICursorMapStore
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerCursorMapStore> _logger;
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance with a connection string.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerCursorMapStore(string connectionString, ILogger<SqlServerCursorMapStore> logger)
		: this(CreateConnectionFactory(connectionString), logger, null)
	{
	}

	/// <summary>
	/// Initializes a new instance with a connection string and an ambient tenant context.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">The ambient tenant context.</param>
	public SqlServerCursorMapStore(
		string connectionString,
		ILogger<SqlServerCursorMapStore> logger,
		ITenantContext? tenantContext)
		: this(CreateConnectionFactory(connectionString), logger, tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerCursorMapStore(Func<SqlConnection> connectionFactory, ILogger<SqlServerCursorMapStore> logger)
		: this(connectionFactory, logger, null)
	{
	}

	/// <summary>
	/// Initializes a new instance with a connection factory and an ambient tenant context.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Cursor
	/// maps are partitioned by the tenant this resolves -- never by a tenant the caller names, which is why
	/// no method takes a tenant argument. The two-argument overloads are kept so the shipped public surface
	/// is unchanged: an existing caller compiles untouched and lands in the untenanted partition, exactly
	/// where its rows already were.
	/// </param>
	public SqlServerCursorMapStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerCursorMapStore> logger,
		ITenantContext? tenantContext)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_logger = logger;
		_tenantContext = tenantContext;
	}

	/// <summary>Resolves the partition every cursor map is confined to.</summary>
	/// <returns>The reserved partition key for the ambient tenant.</returns>
	private string ResolveTenantKey() =>
		KeyedTenantPartition.FromScope(TenantScope.FromContext(_tenantContext)).TenantId;

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
				MERGE ProjectionCursorMaps AS target
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
