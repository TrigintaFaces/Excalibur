// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Saga.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="ISagaStore"/> for managing saga state persistence.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for saga state with optimistic concurrency control using row versioning.
/// Uses database transactions to ensure consistency.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class SqlServerSagaStore : ISagaStore
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerSagaStore> _logger;
	private readonly IJsonSerializer _serializer;
	private readonly SqlServerSagaStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerSagaStore(Func{SqlConnection}, ILogger{SqlServerSagaStore}, IJsonSerializer)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerSagaStore(
		string connectionString,
		ILogger<SqlServerSagaStore> logger,
		IJsonSerializer serializer)
		: this(CreateConnectionFactory(connectionString),
			new SqlServerSagaStoreOptions(),
			logger,
			serializer)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaStore"/> class with options.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	public SqlServerSagaStore(
		string connectionString,
		IOptions<SqlServerSagaStoreOptions> options,
		ILogger<SqlServerSagaStore> logger,
		IJsonSerializer serializer)
		: this(CreateConnectionFactory(connectionString),
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			serializer)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Multi-database setups with marker interfaces (e.g., IDomainDb, ISagaDb)</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with <see cref="IDb"/> abstraction</description></item>
	/// </list>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// new SqlServerSagaStore(
	///     () => (SqlConnection)domainDb.Connection,
	///     logger,
	///     serializer);
	/// </code>
	/// </para>
	/// </remarks>
	public SqlServerSagaStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerSagaStore> logger,
		IJsonSerializer serializer)
		: this(connectionFactory,
			new SqlServerSagaStoreOptions(),
			logger,
			serializer)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaStore"/> class with a connection factory and options.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	public SqlServerSagaStore(
		Func<SqlConnection> connectionFactory,
		IOptions<SqlServerSagaStoreOptions> options,
		ILogger<SqlServerSagaStore> logger,
		IJsonSerializer serializer)
		: this(connectionFactory,
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			serializer)
	{
	}

	private SqlServerSagaStore(
		Func<SqlConnection> connectionFactory,
		SqlServerSagaStoreOptions options,
		ILogger<SqlServerSagaStore> logger,
		IJsonSerializer serializer)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var result = await connection.ResolveAsync(
				new LoadSagaRequest<TSagaState>(
					sagaId,
					_serializer,
					_options.QualifiedTableName,
					cancellationToken))
			.ConfigureAwait(false);

		if (result is not null)
		{
			_logger.LogDebug("Loaded saga {SagaType}/{SagaId}", typeof(TSagaState).Name, sagaId);
		}

		return result;
	}

	/// <inheritdoc/>
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ArgumentNullException.ThrowIfNull(sagaState);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ResolveAsync(
				new SaveSagaRequest<TSagaState>(
					sagaState,
					_serializer,
					_options.QualifiedTableName,
					cancellationToken))
			.ConfigureAwait(false);

		_logger.LogDebug(
			"Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}",
			typeof(TSagaState).Name,
			sagaState.SagaId,
			sagaState.Completed);
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new SqlConnection(connectionString);
	}
}
