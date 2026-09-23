// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Oracle.Requests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Oracle;

/// <summary>
/// Oracle implementation of <see cref="ISagaStore"/> for managing saga state persistence.
/// </summary>
/// <remarks>
/// Provides durable storage for saga state with optimistic concurrency control using a numeric
/// <c>Version</c> compare-and-swap. Mirrors the SQL Server store; Oracle-specific SQL (MERGE with
/// <c>FROM DUAL</c>, colon-prefixed bind variables, <c>SYS_EXTRACT_UTC(SYSTIMESTAMP)</c>) is inlined
/// per the persistence-seam ruling.
/// </remarks>
public sealed class OracleSagaStore : ISagaStore, ISagaStoreAdmin
{
	private readonly Func<OracleConnection> _connectionFactory;
	private readonly ILogger<OracleSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;
	private readonly OracleSagaStoreOptions _options;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);


	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaStore(
		string connectionString,
		ILogger<OracleSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString), new OracleSagaStoreOptions(), logger, serializer, tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaStore"/> class with options.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaStore(
		string connectionString,
		IOptions<OracleSagaStoreOptions> options,
		ILogger<OracleSagaStore> logger,
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
	/// Initializes a new instance of the <see cref="OracleSagaStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="OracleConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaStore(
		Func<OracleConnection> connectionFactory,
		ILogger<OracleSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
		: this(connectionFactory, new OracleSagaStoreOptions(), logger, serializer, tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaStore"/> class with a connection factory and options.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="OracleConnection"/> instances.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaStore(
		Func<OracleConnection> connectionFactory,
		IOptions<OracleSagaStoreOptions> options,
		ILogger<OracleSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
		: this(connectionFactory,
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			serializer,
			tenantContext)
	{
	}

	private OracleSagaStore(
		Func<OracleConnection> connectionFactory,
		OracleSagaStoreOptions options,
		ILogger<OracleSagaStore> logger,
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
				new LoadSagaRequest<TSagaState>(sagaId, _serializer, _options.QualifiedTableName, CurrentTenantScope, cancellationToken))
			.ConfigureAwait(false);

		if (result is not null)
		{
			_logger.LogDebug("Loaded saga {SagaType}/{SagaId}", typeof(TSagaState).Name, sagaId);
		}

		return result;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Saga state is serialized with a reflection-based serializer that may require types which cannot be statically analyzed.")]
	[RequiresDynamicCode("Saga state is serialized with a reflection-based serializer, which generates converters at run time.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ArgumentNullException.ThrowIfNull(sagaState);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ResolveAsync(
				new SaveSagaRequest<TSagaState>(sagaState, _serializer, _options.QualifiedTableName, CurrentTenantScope, cancellationToken))
			.ConfigureAwait(false);

		if (rowsAffected == 0)
		{
			// The version-gated MERGE matched no row: the persisted Version no longer equals the expected
			// (loaded) version -- a concurrent handler advanced this saga between load and save. Surface a
			// ConcurrencyException rather than silently losing the write.
			var expectedVersion = sagaState.Version;
			var current = await connection.ResolveAsync(
					new LoadSagaRequest<TSagaState>(sagaState.SagaId, _serializer, _options.QualifiedTableName, CurrentTenantScope, cancellationToken))
				.ConfigureAwait(false);

			throw new ConcurrencyException(
				nameof(SagaState),
				sagaState.SagaId.ToString(),
				expectedVersion,
				current?.Version ?? -1L);
		}

		// Optimistic-concurrency write-back: advance the in-memory token to the persisted version so a
		// subsequent save on the SAME object uses the new version rather than re-conflicting on the stale one.
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
				PurgeCompletedSagasRequest.ForTenant(
					threshold,
					_options.QualifiedTableName,
					CurrentTenantScope,
					cancellationToken))
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
				PurgeCompletedSagasRequest.ForAllTenants(
					threshold,
					_options.QualifiedTableName,
					cancellationToken))
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
				GetSagaStatisticsRequest.ForTenant(_options.QualifiedTableName, CurrentTenantScope, cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaStoreStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// No tenant discriminator: this counts every tenant's sagas. The estate-wide intent is spelled by the
		// factory NAME, and that factory accepts no scope at all -- so there is no ambient scope handed over
		// to be ignored, and no absent or permissive scope that could reach this path by omission.
		return await connection.ResolveAsync(
				GetSagaStatisticsRequest.ForAllTenants(_options.QualifiedTableName, cancellationToken))
			.ConfigureAwait(false);
	}

	private static Func<OracleConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new OracleConnection(connectionString);
	}
}
