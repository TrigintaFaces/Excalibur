// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Saga.Postgres;

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
public sealed class PostgresSagaStore : ISagaStore, ISagaStoreAdmin
{
	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly PostgresSagaOptions _options;
	private readonly ILogger<PostgresSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSagaStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// Optional ambient tenant context. When supplied and a tenant is resolved, saga load/save and keyed
	/// summary reads are scoped to the current tenant (row-level <c>tenant_id</c>) so a tenant can never load
	/// or overwrite another tenant's saga; the retention purge remains global. When <see langword="null"/>
	/// (the default) no tenant scoping is applied (byte-identical behavior). Fail-closed enforcement for
	/// tenant-facing reads is provided by the tenant-scoping decorator.
	/// </param>
	/// <remarks>
	/// This is the primary constructor for dependency injection scenarios.
	/// </remarks>
	public PostgresSagaStore(
		IOptions<PostgresSagaOptions> options,
		ILogger<PostgresSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
		: this(CreateConnectionFactory(options?.Value), options?.Value!, logger, serializer, tenantContext)
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
	/// <param name="tenantContext">
	/// Optional ambient tenant context for row-level tenant scoping (see the primary constructor's remarks).
	/// </param>
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
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
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
		_tenantContext = tenantContext;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var result = await connection.ResolveAsync(
				new LoadSagaRequest<TSagaState>(sagaId, _options, _serializer, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);

		if (result is not null)
		{
			_logger.LogDebug("Loaded saga {SagaType}/{SagaId}", typeof(TSagaState).Name, sagaId);
		}

		return result;
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "JSON serialization of saga state is intentional.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "JSON serialization of saga state is intentional.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ArgumentNullException.ThrowIfNull(sagaState);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var expectedVersion = sagaState.Version;

		var rowsAffected = await connection.ResolveAsync(
				new SaveSagaRequest<TSagaState>(sagaState, _options, _serializer, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);

		if (rowsAffected == 0)
		{
			// The version-gated upsert matched no row: the persisted version no longer equals the expected
			// (loaded) version, i.e. a concurrent handler advanced this saga between our load and save.
			// Surface it as a ConcurrencyException instead of silently losing the write (skl8r7).
			var current = await connection.ResolveAsync(
					new LoadSagaRequest<TSagaState>(sagaState.SagaId, _options, _serializer, TenantScope.FromContext(_tenantContext), cancellationToken))
				.ConfigureAwait(false);

			throw new ConcurrencyException(
				nameof(SagaState),
				sagaState.SagaId.ToString(),
				expectedVersion,
				current?.Version ?? -1L);
		}

		// Optimistic-concurrency write-back (EF-style; store-owns-increment, skl8r7): on a successful save,
		// advance the in-memory token to the persisted version so a subsequent save on the SAME object
		// (create -> save -> mutate -> save) uses the new loaded version rather than re-conflicting on the stale one.
		sagaState.Version = expectedVersion + 1;

		_logger.LogDebug(
			"Saved saga {SagaType}/{SagaId}, Version={Version}, Completed={IsCompleted}",
			typeof(TSagaState).Name,
			sagaState.SagaId,
			sagaState.Version,
			sagaState.Completed);
	}

	/// <inheritdoc/>
	public async Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var removed = await connection.ResolveAsync(
				new PurgeCompletedSagasRequest(
					threshold,
					_options,
					cancellationToken,
					TenantScope.FromContext(_tenantContext)))
			.ConfigureAwait(false);

		_logger.LogDebug("Purged {Count} completed sagas older than {Threshold}", removed, threshold);

		return removed;
	}

	/// <inheritdoc/>
	public async Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var removed = await connection.ResolveAsync(
				new PurgeCompletedSagasRequest(
					threshold,
					_options,
					cancellationToken,
					allTenants: true))
			.ConfigureAwait(false);

		_logger.LogDebug(
			"Purged {Count} completed sagas older than {Threshold} across all tenants", removed, threshold);

		return removed;
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<SagaInstanceSummary>> QuerySagasAsync(
		SagaQueryFilter filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new QuerySagaSummariesRequest(filter, _options, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaInstanceSummary?> GetSummaryAsync(Guid sagaId, CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new GetSagaSummaryRequest(sagaId, _options, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new GetSagaStatisticsRequest(_options, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);
	}

	private static Func<NpgsqlConnection> CreateConnectionFactory(PostgresSagaOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return () => new NpgsqlConnection(options.ConnectionString);
	}
}
