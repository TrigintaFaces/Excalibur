// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.EventSourcing.Abstractions;

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
/// <item><c>materialized_views</c> table for view data (snake_case per ADR-109)</item>
/// <item><c>materialized_view_positions</c> table for position tracking</item>
/// </list>
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
public sealed partial class PostgresMaterializedViewStore : IMaterializedViewStore
{
	private const string DefaultViewTableName = "materialized_views";
	private const string DefaultPositionTableName = "materialized_view_positions";

	private readonly NpgsqlDataSource _dataSource;
	private readonly string _viewTableName;
	private readonly string _positionTableName;
	private readonly ILogger<PostgresMaterializedViewStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="viewTableName">Optional view table name. Defaults to "materialized_views".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "materialized_view_positions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public PostgresMaterializedViewStore(
		string connectionString,
		ILogger<PostgresMaterializedViewStore> logger,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
		: this(CreateDataSource(connectionString), logger, viewTableName, positionTableName, jsonOptions)
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
	/// <param name="viewTableName">Optional view table name. Defaults to "materialized_views".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "materialized_view_positions".</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public PostgresMaterializedViewStore(
		NpgsqlDataSource dataSource,
		ILogger<PostgresMaterializedViewStore> logger,
		string? viewTableName = null,
		string? positionTableName = null,
		JsonSerializerOptions? jsonOptions = null)
	{
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
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
			SELECT data FROM {_viewTableName}
			WHERE view_name = @view_name AND view_id = @view_id
			""";

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var json = await connection.QuerySingleOrDefaultAsync<string>(
			new CommandDefinition(sql, new { view_name = viewName, view_id = viewId }, cancellationToken: cancellationToken))
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
			INSERT INTO {_viewTableName} (view_name, view_id, data, created_at, updated_at)
			VALUES (@view_name, @view_id, @data::jsonb, @updated_at, @updated_at)
			ON CONFLICT (view_name, view_id)
			DO UPDATE SET data = EXCLUDED.data, updated_at = EXCLUDED.updated_at
			""";

		var json = JsonSerializer.Serialize(view, _jsonOptions);
		var now = DateTimeOffset.UtcNow;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { view_name = viewName, view_id = viewId, data = json, updated_at = now },
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
			WHERE view_name = @view_name AND view_id = @view_id
			""";

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(
			new CommandDefinition(sql, new { view_name = viewName, view_id = viewId }, cancellationToken: cancellationToken))
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
			WHERE view_name = @view_name
			""";

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var position = await connection.QuerySingleOrDefaultAsync<long?>(
			new CommandDefinition(sql, new { view_name = viewName }, cancellationToken: cancellationToken))
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
			INSERT INTO {_positionTableName} (view_name, position, created_at, updated_at)
			VALUES (@view_name, @position, @updated_at, @updated_at)
			ON CONFLICT (view_name)
			DO UPDATE SET position = EXCLUDED.position, updated_at = EXCLUDED.updated_at
			""";

		var now = DateTimeOffset.UtcNow;

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { view_name = viewName, position, updated_at = now },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogPositionSaved(viewName, position);
	}

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
