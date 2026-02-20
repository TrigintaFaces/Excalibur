// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.EventSourcing.Abstractions;

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
/// <b>Performance Note:</b> Methods return <see cref="ValueTask{TResult}"/> to avoid heap allocations
/// for common patterns where the operation completes synchronously or is already cached.
/// </para>
/// </remarks>
public sealed partial class SqlServerMaterializedViewStore : IMaterializedViewStore
{
	private const string DefaultViewTableName = "MaterializedViews";
	private const string DefaultPositionTableName = "MaterializedViewPositions";

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _viewTableName;
	private readonly string _positionTableName;
	private readonly ILogger<SqlServerMaterializedViewStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="viewTableName">Optional view table name. Defaults to "MaterializedViews".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "MaterializedViewPositions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public SqlServerMaterializedViewStore(
		string connectionString,
		ILogger<SqlServerMaterializedViewStore> logger,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
		: this(CreateConnectionFactory(connectionString), logger, viewTableName, positionTableName, jsonOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerMaterializedViewStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="viewTableName">Optional view table name. Defaults to "MaterializedViews".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "MaterializedViewPositions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public SqlServerMaterializedViewStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerMaterializedViewStore> logger,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_viewTableName = viewTableName ?? DefaultViewTableName;
		_positionTableName = positionTableName ?? DefaultPositionTableName;
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
			WHERE ViewName = @ViewName AND ViewId = @ViewId
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var json = await connection.QuerySingleOrDefaultAsync<string>(
			new CommandDefinition(sql, new { ViewName = viewName, ViewId = viewId }, cancellationToken: cancellationToken))
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
			MERGE [{_viewTableName}] AS target
			USING (SELECT @ViewName AS ViewName, @ViewId AS ViewId, @Data AS Data, @UpdatedAt AS UpdatedAt) AS source
			ON target.ViewName = source.ViewName AND target.ViewId = source.ViewId
			WHEN MATCHED THEN
				UPDATE SET Data = source.Data, UpdatedAt = source.UpdatedAt
			WHEN NOT MATCHED THEN
				INSERT (ViewName, ViewId, Data, CreatedAt, UpdatedAt)
				VALUES (source.ViewName, source.ViewId, source.Data, source.UpdatedAt, source.UpdatedAt);
			""";

		var json = JsonSerializer.Serialize(view, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { ViewName = viewName, ViewId = viewId, Data = json, UpdatedAt = now },
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
			WHERE ViewName = @ViewName AND ViewId = @ViewId
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(
			new CommandDefinition(sql, new { ViewName = viewName, ViewId = viewId }, cancellationToken: cancellationToken))
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
			SELECT Position FROM [{_positionTableName}]
			WHERE ViewName = @ViewName
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var position = await connection.QuerySingleOrDefaultAsync<long?>(
			new CommandDefinition(sql, new { ViewName = viewName }, cancellationToken: cancellationToken))
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

		var sql = $"""
			MERGE [{_positionTableName}] AS target
			USING (SELECT @ViewName AS ViewName, @Position AS Position, @UpdatedAt AS UpdatedAt) AS source
			ON target.ViewName = source.ViewName
			WHEN MATCHED THEN
				UPDATE SET Position = source.Position, UpdatedAt = source.UpdatedAt
			WHEN NOT MATCHED THEN
				INSERT (ViewName, Position, CreatedAt, UpdatedAt)
				VALUES (source.ViewName, source.Position, source.UpdatedAt, source.UpdatedAt);
			""";

		var now = DateTimeOffset.UtcNow;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { ViewName = viewName, Position = position, UpdatedAt = now },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogPositionSaved(viewName, position);
	}

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

	#endregion
}
