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
/// SQL Server implementation of <see cref="ISagaTimeoutStore"/> for persistent saga timeout storage.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses Dapper for all database operations and provides:
/// <list type="bullet">
/// <item><description>Durable timeout storage that survives process restarts</description></item>
/// <item><description>Efficient polling via indexed DueAt column</description></item>
/// <item><description>OpenTelemetry activity spans for observability</description></item>
/// </list>
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class SqlServerSagaTimeoutStore : ISagaTimeoutStore
{
	private const string SourceName = "Excalibur.Dispatch.Sagas.SqlServer";
	private static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerSagaTimeoutStore> _logger;
	private readonly SqlServerSagaTimeoutStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerSagaTimeoutStore(Func{SqlConnection}, ILogger{SqlServerSagaTimeoutStore})"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerSagaTimeoutStore(
		string connectionString,
		ILogger<SqlServerSagaTimeoutStore> logger)
		: this(CreateConnectionFactory(connectionString),
			new SqlServerSagaTimeoutStoreOptions(),
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStore"/> class with options.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="options">The saga timeout store options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerSagaTimeoutStore(
		string connectionString,
		IOptions<SqlServerSagaTimeoutStoreOptions> options,
		ILogger<SqlServerSagaTimeoutStore> logger)
		: this(CreateConnectionFactory(connectionString),
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStore"/> class with a connection factory.
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
	public SqlServerSagaTimeoutStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerSagaTimeoutStore> logger)
		: this(connectionFactory,
			new SqlServerSagaTimeoutStoreOptions(),
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStore"/> class with a connection factory and options.
	/// </summary>
	/// <param name="connectionFactory">The connection factory.</param>
	/// <param name="options">The saga timeout store options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerSagaTimeoutStore(
		Func<SqlConnection> connectionFactory,
		IOptions<SqlServerSagaTimeoutStoreOptions> options,
		ILogger<SqlServerSagaTimeoutStore> logger)
		: this(connectionFactory,
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger)
	{
	}

	private SqlServerSagaTimeoutStore(
		Func<SqlConnection> connectionFactory,
		SqlServerSagaTimeoutStoreOptions options,
		ILogger<SqlServerSagaTimeoutStore> logger)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(timeout);

		using var activity = ActivitySource.StartActivity("ScheduleTimeout");
		_ = (activity?.SetTag("saga.id", timeout.SagaId));
		_ = (activity?.SetTag("timeout.id", timeout.TimeoutId));
		_ = (activity?.SetTag("timeout.type", timeout.TimeoutType));

		var sql = $@"
            INSERT INTO {_options.QualifiedTableName}
                (TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt)
            VALUES
                (@TimeoutId, @SagaId, @SagaType, @TimeoutType, @TimeoutData, @DueAt, @ScheduledAt)";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new
			{
				timeout.TimeoutId,
				timeout.SagaId,
				timeout.SagaType,
				timeout.TimeoutType,
				timeout.TimeoutData,
				timeout.DueAt,
				timeout.ScheduledAt
			},
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogTimeoutScheduled(timeout.TimeoutId, timeout.SagaId);
		}
	}

	/// <inheritdoc />
	public async Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);

		using var activity = ActivitySource.StartActivity("CancelTimeout");
		_ = (activity?.SetTag("saga.id", sagaId));
		_ = (activity?.SetTag("timeout.id", timeoutId));

		var sql = $"DELETE FROM {_options.QualifiedTableName} WHERE SagaId = @SagaId AND TimeoutId = @TimeoutId";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new { SagaId = sagaId, TimeoutId = timeoutId },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogTimeoutCancelled(timeoutId, sagaId);
		}
	}

	/// <inheritdoc />
	public async Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);

		using var activity = ActivitySource.StartActivity("CancelAllTimeouts");
		_ = (activity?.SetTag("saga.id", sagaId));

		var sql = $"DELETE FROM {_options.QualifiedTableName} WHERE SagaId = @SagaId";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new { SagaId = sagaId },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		_ = (activity?.SetTag("timeout.count", rowsAffected));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogAllTimeoutsCancelled(sagaId, rowsAffected);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("GetDueTimeouts");
		_ = (activity?.SetTag("timeout.as_of", asOf.ToString("O")));

		var sql = $@"
            SELECT TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt
            FROM {_options.QualifiedTableName}
            WHERE DueAt <= @AsOf
            ORDER BY DueAt ASC";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var results = await connection.QueryAsync<TimeoutRecord>(new CommandDefinition(
			sql,
			new { AsOf = asOf },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		var timeouts = results
			.Select(r => new SagaTimeout(
				r.TimeoutId,
				r.SagaId,
				r.SagaType,
				r.TimeoutType,
				r.TimeoutData,
				r.DueAt,
				r.ScheduledAt))
			.ToList();

		_ = (activity?.SetTag("timeout.count", timeouts.Count));

		return timeouts;
	}

	/// <inheritdoc />
	public async Task MarkDeliveredAsync(string timeoutId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);

		using var activity = ActivitySource.StartActivity("MarkDelivered");
		_ = (activity?.SetTag("timeout.id", timeoutId));

		var sql = $"DELETE FROM {_options.QualifiedTableName} WHERE TimeoutId = @TimeoutId";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new { TimeoutId = timeoutId },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogTimeoutDelivered(timeoutId);
		}
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new SqlConnection(connectionString);
	}

	/// <summary>
	/// Internal record for Dapper mapping.
	/// </summary>
	private sealed record TimeoutRecord(
		string TimeoutId,
		string SagaId,
		string SagaType,
		string TimeoutType,
		byte[]? TimeoutData,
		DateTimeOffset DueAt,
		DateTimeOffset ScheduledAt);
}
