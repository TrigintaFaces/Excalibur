// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// Optional ambient tenant context. When supplied and a tenant is resolved, saga load/save and keyed
	/// summary reads are scoped to the current tenant (row-level <c>TenantId</c>) so a tenant can never load
	/// or overwrite another tenant's saga; the retention purge remains global. When <see langword="null"/>
	/// (the default) no tenant scoping is applied (byte-identical behavior). Fail-closed enforcement for
	/// tenant-facing reads is provided by the tenant-scoping decorator.
	/// </param>
	public OracleSagaStore(
		string connectionString,
		ILogger<OracleSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
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
	/// Optional ambient tenant context for row-level tenant scoping (see the primary constructor's remarks).
	/// </param>
	public OracleSagaStore(
		string connectionString,
		IOptions<OracleSagaStoreOptions> options,
		ILogger<OracleSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
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
	/// Optional ambient tenant context for row-level tenant scoping (see the primary constructor's remarks).
	/// </param>
	public OracleSagaStore(
		Func<OracleConnection> connectionFactory,
		ILogger<OracleSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
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
	/// Optional ambient tenant context for row-level tenant scoping (see the primary constructor's remarks).
	/// </param>
	public OracleSagaStore(
		Func<OracleConnection> connectionFactory,
		IOptions<OracleSagaStoreOptions> options,
		ILogger<OracleSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
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
		ITenantContext? tenantContext = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_tenantContext = tenantContext;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var result = await connection.ResolveAsync(
				new LoadSagaRequest<TSagaState>(sagaId, _serializer, _options.QualifiedTableName, TenantScope.FromContext(_tenantContext), cancellationToken))
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
				new SaveSagaRequest<TSagaState>(sagaState, _serializer, _options.QualifiedTableName, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);

		if (rowsAffected == 0)
		{
			// The version-gated MERGE matched no row: the persisted Version no longer equals the expected
			// (loaded) version -- a concurrent handler advanced this saga between load and save. Surface a
			// ConcurrencyException rather than silently losing the write.
			var expectedVersion = sagaState.Version;
			var current = await connection.ResolveAsync(
					new LoadSagaRequest<TSagaState>(sagaState.SagaId, _serializer, _options.QualifiedTableName, TenantScope.FromContext(_tenantContext), cancellationToken))
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
				new PurgeCompletedSagasRequest(
					threshold,
					_options.QualifiedTableName,
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
				new QuerySagaSummariesRequest(filter, _options.QualifiedTableName, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaInstanceSummary?> GetSummaryAsync(Guid sagaId, CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new GetSagaSummaryRequest(sagaId, _options.QualifiedTableName, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new GetSagaStatisticsRequest(_options.QualifiedTableName, TenantScope.FromContext(_tenantContext), cancellationToken))
			.ConfigureAwait(false);
	}

	private static Func<OracleConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new OracleConnection(connectionString);
	}
}
