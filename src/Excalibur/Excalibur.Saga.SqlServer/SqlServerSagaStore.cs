// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
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
public sealed class SqlServerSagaStore : ISagaStore, ISagaStoreAdmin
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;
	private readonly SqlServerSagaStoreOptions _options;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);


	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerSagaStore(Func{SqlConnection}, ILogger{SqlServerSagaStore}, DispatchJsonSerializer, ITenantContext)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerSagaStore(
		string connectionString,
		ILogger<SqlServerSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString),
			new SqlServerSagaStoreOptions(),
			logger,
			serializer,
			tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaStore"/> class with options.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public SqlServerSagaStore(
		string connectionString,
		IOptions<SqlServerSagaStoreOptions> options,
		ILogger<SqlServerSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString),
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			serializer,
			tenantContext)
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
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
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
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
		: this(connectionFactory,
			new SqlServerSagaStoreOptions(),
			logger,
			serializer,
			tenantContext)
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
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public SqlServerSagaStore(
		Func<SqlConnection> connectionFactory,
		IOptions<SqlServerSagaStoreOptions> options,
		ILogger<SqlServerSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
		: this(connectionFactory,
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			serializer,
			tenantContext)
	{
	}

	private SqlServerSagaStore(
		Func<SqlConnection> connectionFactory,
		SqlServerSagaStoreOptions options,
		ILogger<SqlServerSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
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
					CurrentTenantScope,
					cancellationToken))
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

		var rowsAffected = await connection.ResolveAsync(
				new SaveSagaRequest<TSagaState>(
					sagaState,
					_serializer,
					_options.QualifiedTableName,
					CurrentTenantScope,
					cancellationToken))
			.ConfigureAwait(false);

		if (rowsAffected == 0)
		{
			// The version-gated MERGE matched no row: the persisted Version no longer equals the expected
			// (loaded) version, i.e. a concurrent handler advanced this saga between our load and save.
			// Surface it as a ConcurrencyException instead of silently losing the write.
			var expectedVersion = sagaState.Version;
			var current = await connection.ResolveAsync(
					new LoadSagaRequest<TSagaState>(
						sagaState.SagaId,
						_serializer,
						_options.QualifiedTableName,
						CurrentTenantScope,
						cancellationToken))
				.ConfigureAwait(false);

			throw new ConcurrencyException(
				nameof(SagaState),
				sagaState.SagaId.ToString(),
				expectedVersion,
				current?.Version ?? -1L);
		}

		// Optimistic-concurrency write-back (EF-style; store-owns-increment,): on a successful save,
		// advance the in-memory token to the persisted version so a subsequent save on the SAME object
		// (create -> save -> mutate -> save) uses the new loaded version rather than re-conflicting on the stale one.
		sagaState.Version += 1;

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
					_options.QualifiedTableName,
					cancellationToken,
					CurrentTenantScope))
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
					_options.QualifiedTableName,
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
				new QuerySagaSummariesRequest(filter, _options.QualifiedTableName, CurrentTenantScope, cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaInstanceSummary?> GetSummaryAsync(Guid sagaId, CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new GetSagaSummaryRequest(sagaId, _options.QualifiedTableName, CurrentTenantScope, cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new GetSagaStatisticsRequest(_options.QualifiedTableName, CurrentTenantScope, cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaStoreStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// No tenant discriminator: this counts every tenant's sagas. The ambient scope is passed through
		// unchanged and deliberately ignored by the request when allTenants is set -- the estate-wide intent
		// is spelled at the call site, never reached by an absent or permissive scope.
		return await connection.ResolveAsync(
				new GetSagaStatisticsRequest(_options.QualifiedTableName, CurrentTenantScope, cancellationToken, allTenants: true))
			.ConfigureAwait(false);
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new SqlConnection(connectionString);
	}
}
