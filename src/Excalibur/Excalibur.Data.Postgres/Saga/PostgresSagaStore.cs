// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.Saga;

/// <summary>
/// Postgres implementation of <see cref="ISagaStore"/> for managing saga state persistence.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for saga state using Postgres with JSONB column type
/// for efficient state serialization. Uses INSERT ON CONFLICT for atomic upserts.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Via dependency injection with IOptions for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class PostgresSagaStore : ISagaStore
{
	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly PostgresSagaOptions _options;
	private readonly ILogger<PostgresSagaStore> _logger;
	private readonly IJsonSerializer _serializer;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSagaStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// This is the primary constructor for dependency injection scenarios.
	/// </remarks>
	public PostgresSagaStore(
		IOptions<PostgresSagaOptions> options,
		ILogger<PostgresSagaStore> logger,
		IJsonSerializer serializer)
		: this(CreateConnectionFactory(options?.Value), options?.Value, logger, serializer)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSagaStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="NpgsqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="options">The configuration options.</param>
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
	/// new PostgresSagaStore(
	///     () => (NpgsqlConnection)sagaDb.Connection,
	///     options,
	///     logger,
	///     serializer);
	/// </code>
	/// </para>
	/// </remarks>
	public PostgresSagaStore(
		Func<NpgsqlConnection> connectionFactory,
		PostgresSagaOptions options,
		ILogger<PostgresSagaStore> logger,
		IJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		options.Validate();

		_connectionFactory = connectionFactory;
		_options = options;
		_logger = logger;
		_serializer = serializer;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var result = await connection.ResolveAsync(
				new LoadSagaRequest<TSagaState>(sagaId, _options, _serializer, cancellationToken))
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
				new SaveSagaRequest<TSagaState>(sagaState, _options, _serializer, cancellationToken))
			.ConfigureAwait(false);

		_logger.LogDebug(
			"Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}",
			typeof(TSagaState).Name,
			sagaState.SagaId,
			sagaState.Completed);
	}

	private static Func<NpgsqlConnection> CreateConnectionFactory(PostgresSagaOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return () => new NpgsqlConnection(options.ConnectionString);
	}
}
