// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;
using Excalibur.EventSourcing.Abstractions;
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
///     ProjectionName NVARCHAR(256) NOT NULL,
///     StreamId NVARCHAR(256) NOT NULL,
///     Position BIGINT NOT NULL,
///     CONSTRAINT PK_ProjectionCursorMaps PRIMARY KEY (ProjectionName, StreamId)
/// );
/// </code>
/// </para>
/// </remarks>
public sealed class SqlServerCursorMapStore : ICursorMapStore
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerCursorMapStore> _logger;

	/// <summary>
	/// Initializes a new instance with a connection string.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerCursorMapStore(string connectionString, ILogger<SqlServerCursorMapStore> logger)
		: this(CreateConnectionFactory(connectionString), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerCursorMapStore(Func<SqlConnection> connectionFactory, ILogger<SqlServerCursorMapStore> logger)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, long>> GetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<CursorMapRow>(
			"SELECT StreamId, Position FROM ProjectionCursorMaps WHERE ProjectionName = @ProjectionName",
			new { ProjectionName = projectionName }).ConfigureAwait(false);

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

		foreach (var entry in cursorMap)
		{
			await connection.ExecuteAsync(
				"""
				MERGE ProjectionCursorMaps AS target
				USING (SELECT @ProjectionName AS ProjectionName, @StreamId AS StreamId) AS source
				ON target.ProjectionName = source.ProjectionName AND target.StreamId = source.StreamId
				WHEN MATCHED THEN UPDATE SET Position = @Position
				WHEN NOT MATCHED THEN INSERT (ProjectionName, StreamId, Position) VALUES (@ProjectionName, @StreamId, @Position);
				""",
				new { ProjectionName = projectionName, StreamId = entry.Key, Position = entry.Value },
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
			"DELETE FROM ProjectionCursorMaps WHERE ProjectionName = @ProjectionName",
			new { ProjectionName = projectionName }).ConfigureAwait(false);
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrEmpty(connectionString);
		return () => new SqlConnection(connectionString);
	}

	private sealed record CursorMapRow(string StreamId, long Position);
}
