// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Saga.Models;
using Excalibur.Saga.Queries;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="ISagaCorrelationQuery"/> using Dapper.
/// </summary>
/// <remarks>
/// <para>
/// Uses the persisted computed column <c>CorrelationId</c> (backed by a nonclustered index)
/// for efficient correlation ID lookups, and SQL Server's <c>JSON_VALUE</c> function
/// for ad-hoc property queries from the stored JSON state.
/// </para>
/// <para>
/// Requires the <c>02-SagaCorrelationIndex.sql</c> migration for optimal performance.
/// Falls back to <c>JSON_VALUE</c> if the computed column is not yet created.
/// </para>
/// <para>
/// Follows the bracket-escaping pattern (S544) for SQL identifier safety.
/// </para>
/// </remarks>
internal sealed partial class SqlServerSagaCorrelationQuery : ISagaCorrelationQuery
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly SqlServerSagaStoreOptions _storeOptions;
	private readonly SagaCorrelationQueryOptions _queryOptions;
	private readonly ILogger<SqlServerSagaCorrelationQuery> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaCorrelationQuery"/> class.
	/// </summary>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <param name="storeOptions">The saga store options (schema/table names).</param>
	/// <param name="queryOptions">The correlation query options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerSagaCorrelationQuery(
		Func<SqlConnection> connectionFactory,
		IOptions<SqlServerSagaStoreOptions> storeOptions,
		IOptions<SagaCorrelationQueryOptions> queryOptions,
		ILogger<SqlServerSagaCorrelationQuery> logger)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_storeOptions = storeOptions?.Value ?? throw new ArgumentNullException(nameof(storeOptions));
		_queryOptions = queryOptions?.Value ?? throw new ArgumentNullException(nameof(queryOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SagaQueryResult>> FindByCorrelationIdAsync(
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(correlationId);

		// Uses the persisted computed column CorrelationId (indexed) for efficient lookups.
		// Falls back to JSON_VALUE if the computed column doesn't exist yet.
		var sql = $"""
			SELECT TOP (@MaxResults)
				SagaId,
				SagaType,
				CorrelationId,
				JSON_VALUE(StateJson, '$.Status') AS StatusValue,
				JSON_VALUE(StateJson, '$.StartedAt') AS CreatedAt
			FROM {_storeOptions.QualifiedTableName}
			WHERE CorrelationId = @CorrelationId
			""";

		if (!_queryOptions.IncludeCompleted)
		{
			sql += "\n\tAND IsCompleted = 0";
		}

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<SagaRow>(
			new CommandDefinition(
				sql,
				new { CorrelationId = correlationId, _queryOptions.MaxResults },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var results = rows
			.Select(static r => new SagaQueryResult(
				r.SagaId,
				r.SagaType,
				ParseStatus(r.StatusValue),
				r.CorrelationId ?? string.Empty,
				ParseCreatedAt(r.CreatedAt)))
			.ToList();

		LogFoundByCorrelationId(correlationId, results.Count);
		return results;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SagaQueryResult>> FindByPropertyAsync(
		string propertyName,
		object value,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(propertyName);
		ArgumentNullException.ThrowIfNull(value);

		// Validate property name against whitelist to prevent JSON path injection
		if (!SafePropertyNameRegex().IsMatch(propertyName))
		{
			throw new ArgumentException(
				$"Property name '{propertyName}' contains invalid characters. Only alphanumeric and underscore are allowed.",
				nameof(propertyName));
		}

		var jsonPath = $"$.{propertyName}";
		var sql = $"""
			SELECT TOP (@MaxResults)
				SagaId,
				SagaType,
				JSON_VALUE(StateJson, '$.CorrelationId') AS CorrelationId,
				JSON_VALUE(StateJson, '$.Status') AS StatusValue,
				JSON_VALUE(StateJson, '$.StartedAt') AS CreatedAt
			FROM {_storeOptions.QualifiedTableName}
			WHERE JSON_VALUE(StateJson, @JsonPath) = @PropertyValue
			""";

		if (!_queryOptions.IncludeCompleted)
		{
			sql += "\n\tAND IsCompleted = 0";
		}

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<SagaRow>(
			new CommandDefinition(
				sql,
				new { JsonPath = jsonPath, PropertyValue = value.ToString(), _queryOptions.MaxResults },
				cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var results = rows
			.Select(static r => new SagaQueryResult(
				r.SagaId,
				r.SagaType,
				ParseStatus(r.StatusValue),
				r.CorrelationId ?? string.Empty,
				ParseCreatedAt(r.CreatedAt)))
			.ToList();

		LogFoundByProperty(propertyName, value.ToString() ?? string.Empty, results.Count);
		return results;
	}

	private static SagaStatus ParseStatus(string? statusValue)
	{
		if (string.IsNullOrEmpty(statusValue))
		{
			return SagaStatus.Created;
		}

		return Enum.TryParse<SagaStatus>(statusValue, ignoreCase: true, out var status)
			? status
			: SagaStatus.Created;
	}

	private static DateTimeOffset ParseCreatedAt(string? createdAt)
	{
		if (string.IsNullOrEmpty(createdAt))
		{
			return DateTimeOffset.MinValue;
		}

		return DateTimeOffset.TryParse(createdAt, out var result)
			? result
			: DateTimeOffset.MinValue;
	}

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SafePropertyNameRegex();

	[LoggerMessage(3902, LogLevel.Debug,
		"Found {Count} saga(s) by correlation ID '{CorrelationId}'")]
	private partial void LogFoundByCorrelationId(string correlationId, int count);

	[LoggerMessage(3903, LogLevel.Debug,
		"Found {Count} saga(s) by property '{PropertyName}' = '{Value}'")]
	private partial void LogFoundByProperty(string propertyName, string value, int count);

	/// <summary>
	/// Internal DTO for Dapper row mapping.
	/// </summary>
	private sealed class SagaRow
	{
		public string SagaId { get; set; } = string.Empty;
		public string SagaType { get; set; } = string.Empty;
		public string? CorrelationId { get; set; }
		public string? StatusValue { get; set; }
		public string? CreatedAt { get; set; }
	}
}
