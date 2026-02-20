// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Dapper;

using Excalibur.Saga.Abstractions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="ISagaMonitoringService"/> for saga operational visibility.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses Dapper for all database operations and provides:
/// <list type="bullet">
/// <item><description>Efficient queries optimized for monitoring dashboards</description></item>
/// <item><description>OpenTelemetry activity spans for observability</description></item>
/// <item><description>Stuck saga detection based on last update time</description></item>
/// <item><description>Failure analysis via FailureReason column</description></item>
/// </list>
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Prerequisites:</b> Run the schema migration script <c>02-SagaMonitoringSchema.sql</c>
/// to add the required CompletedAt and FailureReason columns.
/// </para>
/// </remarks>
public sealed partial class SqlServerSagaMonitoringService : ISagaMonitoringService
{
	private const string SourceName = "Excalibur.Dispatch.Sagas.SqlServer.Monitoring";
	private static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerSagaMonitoringService> _logger;
	private readonly SqlServerSagaStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaMonitoringService"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerSagaMonitoringService(Func{SqlConnection}, ILogger{SqlServerSagaMonitoringService})"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerSagaMonitoringService(
		string connectionString,
		ILogger<SqlServerSagaMonitoringService> logger)
		: this(CreateConnectionFactory(connectionString),
			new SqlServerSagaStoreOptions(),
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaMonitoringService"/> class with options.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerSagaMonitoringService(
		string connectionString,
		IOptions<SqlServerSagaStoreOptions> options,
		ILogger<SqlServerSagaMonitoringService> logger)
		: this(CreateConnectionFactory(connectionString),
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaMonitoringService"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Multi-database setups with marker interfaces (e.g., IDomainDb, ISagaDb)</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with <c>IDb</c> abstraction</description></item>
	/// </list>
	/// </remarks>
	public SqlServerSagaMonitoringService(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerSagaMonitoringService> logger)
		: this(connectionFactory,
			new SqlServerSagaStoreOptions(),
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaMonitoringService"/> class with a connection factory and options.
	/// </summary>
	/// <param name="connectionFactory">The connection factory.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerSagaMonitoringService(
		Func<SqlConnection> connectionFactory,
		IOptions<SqlServerSagaStoreOptions> options,
		ILogger<SqlServerSagaMonitoringService> logger)
		: this(connectionFactory,
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger)
	{
	}

	private SqlServerSagaMonitoringService(
		Func<SqlConnection> connectionFactory,
		SqlServerSagaStoreOptions options,
		ILogger<SqlServerSagaMonitoringService> logger)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<int> GetRunningCountAsync(string? sagaType, CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("GetRunningCount");
		_ = (activity?.SetTag("saga.type", sagaType ?? "all"));

		var sql = $@"
            SELECT COUNT(*)
            FROM {_options.QualifiedTableName}
            WHERE IsCompleted = 0
            AND (@SagaType IS NULL OR SagaType = @SagaType)";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			sql,
			new { SagaType = sagaType },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		_ = (activity?.SetTag("saga.count", count));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogRunningCount(count, sagaType);
		}

		return count;
	}

	/// <inheritdoc />
	public async Task<int> GetCompletedCountAsync(string? sagaType, DateTime? since, CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("GetCompletedCount");
		_ = (activity?.SetTag("saga.type", sagaType ?? "all"));
		_ = (activity?.SetTag("saga.since", since?.ToString("O") ?? "all"));

		var sql = $@"
            SELECT COUNT(*)
            FROM {_options.QualifiedTableName}
            WHERE IsCompleted = 1
            AND (@SagaType IS NULL OR SagaType = @SagaType)
            AND (@Since IS NULL OR CompletedAt >= @Since)";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			sql,
			new { SagaType = sagaType, Since = since },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		_ = (activity?.SetTag("saga.count", count));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogCompletedCount(count, sagaType);
		}

		return count;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SagaInstanceInfo>> GetStuckSagasAsync(
		TimeSpan threshold,
		int limit,
		CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("GetStuckSagas");
		_ = (activity?.SetTag("saga.threshold_minutes", threshold.TotalMinutes));
		_ = (activity?.SetTag("saga.limit", limit));

		var thresholdDate = DateTimeOffset.UtcNow - threshold;

		var sql = $@"
            SELECT TOP (@Limit)
                SagaId, SagaType, IsCompleted, CreatedUtc, UpdatedUtc, CompletedAt, FailureReason
            FROM {_options.QualifiedTableName}
            WHERE IsCompleted = 0
            AND UpdatedUtc < @ThresholdDate
            ORDER BY UpdatedUtc ASC";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var results = await connection.QueryAsync<SagaRecord>(new CommandDefinition(
			sql,
			new { Limit = limit, ThresholdDate = thresholdDate },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		var sagas = results
			.Select(r => new SagaInstanceInfo(
				r.SagaId,
				r.SagaType,
				r.IsCompleted,
				r.CreatedUtc,
				r.UpdatedUtc,
				r.CompletedAt,
				r.FailureReason))
			.ToList();

		_ = (activity?.SetTag("saga.count", sagas.Count));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogStuckSagas(sagas.Count, threshold.TotalMinutes);
		}

		return sagas;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SagaInstanceInfo>> GetFailedSagasAsync(
		int limit,
		CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("GetFailedSagas");
		_ = (activity?.SetTag("saga.limit", limit));

		var sql = $@"
            SELECT TOP (@Limit)
                SagaId, SagaType, IsCompleted, CreatedUtc, UpdatedUtc, CompletedAt, FailureReason
            FROM {_options.QualifiedTableName}
            WHERE FailureReason IS NOT NULL
            ORDER BY UpdatedUtc DESC";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var results = await connection.QueryAsync<SagaRecord>(new CommandDefinition(
			sql,
			new { Limit = limit },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		var sagas = results
			.Select(r => new SagaInstanceInfo(
				r.SagaId,
				r.SagaType,
				r.IsCompleted,
				r.CreatedUtc,
				r.UpdatedUtc,
				r.CompletedAt,
				r.FailureReason))
			.ToList();

		_ = (activity?.SetTag("saga.count", sagas.Count));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogFailedSagas(sagas.Count);
		}

		return sagas;
	}

	/// <inheritdoc />
	public async Task<TimeSpan?> GetAverageCompletionTimeAsync(
		string sagaType,
		DateTime since,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);

		using var activity = ActivitySource.StartActivity("GetAverageCompletionTime");
		_ = (activity?.SetTag("saga.type", sagaType));
		_ = (activity?.SetTag("saga.since", since.ToString("O")));

		var sql = $@"
            SELECT AVG(CAST(DATEDIFF(MILLISECOND, CreatedUtc, CompletedAt) AS BIGINT))
            FROM {_options.QualifiedTableName}
            WHERE SagaType = @SagaType
            AND IsCompleted = 1
            AND CompletedAt IS NOT NULL
            AND CompletedAt >= @Since";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var averageMs = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
			sql,
			new { SagaType = sagaType, Since = since },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		_ = (activity?.SetTag("saga.average_ms", averageMs));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogAverageCompletionTime(sagaType, averageMs);
		}

		return averageMs.HasValue ? TimeSpan.FromMilliseconds(averageMs.Value) : null;
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new SqlConnection(connectionString);
	}

	/// <summary>
	/// Internal record for Dapper mapping.
	/// </summary>
	private sealed record SagaRecord(
		Guid SagaId,
		string SagaType,
		bool IsCompleted,
		DateTime CreatedUtc,
		DateTime UpdatedUtc,
		DateTime? CompletedAt,
		string? FailureReason);
}
