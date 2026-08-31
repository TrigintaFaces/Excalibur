// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Oracle;

/// <summary>
/// Oracle implementation of <see cref="ISagaTimeoutStore"/> for persistent saga timeout storage.
/// </summary>
/// <remarks>
/// Uses Dapper with colon-prefixed Oracle bind variables (bound by name) and provides durable timeout
/// storage that survives process restarts, efficient polling via the indexed DueAt column, and
/// OpenTelemetry activity spans.
/// </remarks>
public sealed partial class OracleSagaTimeoutStore : ISagaTimeoutStore
{
	private const string SourceName = "Excalibur.Dispatch.Sagas.Oracle";
	private static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

	private readonly Func<OracleConnection> _connectionFactory;
	private readonly ILogger<OracleSagaTimeoutStore> _logger;
	private readonly ITenantContext _tenantContext;
	private readonly OracleSagaTimeoutStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaTimeoutStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaTimeoutStore(
		string connectionString,
		ILogger<OracleSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString), new OracleSagaTimeoutStoreOptions(), logger, tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaTimeoutStore"/> class with options.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <param name="options">The saga timeout store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaTimeoutStore(
		string connectionString,
		IOptions<OracleSagaTimeoutStoreOptions> options,
		ILogger<OracleSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString),
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaTimeoutStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="OracleConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaTimeoutStore(
		Func<OracleConnection> connectionFactory,
		ILogger<OracleSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(connectionFactory, new OracleSagaTimeoutStoreOptions(), logger, tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSagaTimeoutStore"/> class with a connection factory and options.
	/// </summary>
	/// <param name="connectionFactory">The connection factory.</param>
	/// <param name="options">The saga timeout store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleSagaTimeoutStore(
		Func<OracleConnection> connectionFactory,
		IOptions<OracleSagaTimeoutStoreOptions> options,
		ILogger<OracleSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(connectionFactory,
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			tenantContext)
	{
	}

	private OracleSagaTimeoutStore(
		Func<OracleConnection> connectionFactory,
		OracleSagaTimeoutStoreOptions options,
		ILogger<OracleSagaTimeoutStore> logger,
		ITenantContext tenantContext)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
	}

	/// <summary>
	/// Resolves the tenant partition this store writes under, from its required tenant context.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Resolved through the context fold, which fails closed, and never through the fold that rehydrates a
	/// value read back from storage. Those two folds both take a tenant term and differ in what an absent one
	/// means, so feeding an ambient read into the storage fold turned <em>"no tenant context was established
	/// here"</em> into <em>"this row belongs to no tenant"</em>. Nothing in the type could tell them apart.
	/// </para>
	/// <para>
	/// The consequences that substitution had here were both silent. On a single-tenant host the ambient value
	/// is unset, so the timeout was stamped with the reserved untenanted sentinel while every other store in
	/// the framework stamps the default tenant identity &#8212; a partition nothing else addresses. On a
	/// multi-tenant host with no scope established it was the same sentinel, so the timeout fired against a
	/// partition the saga does not live in and the saga never completed. Resolving from the context instead
	/// yields the default tenant identity on a single-tenant host, the established tenant on a multi-tenant
	/// one, and refuses outright when a multi-tenant host has established none.
	/// </para>
	/// </remarks>
	/// <returns>The partition named by the current tenant context.</returns>
	private KeyedTenantPartition CurrentPartition() =>
		KeyedTenantPartition.FromContext(_tenantContext);

	/// <inheritdoc />
	public async Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(timeout);

		using var activity = ActivitySource.StartActivity("ScheduleTimeout");
		_ = (activity?.SetTag("saga.id", timeout.SagaId));
		_ = (activity?.SetTag("timeout.id", timeout.TimeoutId));
		_ = (activity?.SetTag("timeout.type", timeout.TimeoutType));

		// The owning tenant is stamped from the AMBIENT scope, not from timeout.TenantId: a scheduled timeout
		// must not be able to claim a tenant its caller never established. timeout.TenantId is an output-of-read
		// on that type and is deliberately ignored here.
		var partition = CurrentPartition();

		var sql = $"""
			INSERT INTO {_options.QualifiedTableName}
				(TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt, TenantId)
			VALUES
				(:TimeoutId, :SagaId, :SagaType, :TimeoutType, :TimeoutData, :DueAt, :ScheduledAt, :TenantId)
			""";

		// ODP.NET binds by position unless BindByName is set; the add order below mirrors the placeholder order
		// in the SQL above, so TenantId is added last to match.
		var dp = new DynamicParameters();
		dp.Add("TimeoutId", timeout.TimeoutId);
		dp.Add("SagaId", timeout.SagaId);
		dp.Add("SagaType", timeout.SagaType);
		dp.Add("TimeoutType", timeout.TimeoutType);
		dp.Add("TimeoutData", timeout.TimeoutData);
		dp.Add("DueAt", timeout.DueAt);
		dp.Add("ScheduledAt", timeout.ScheduledAt);
		dp.Add("TenantId", partition.TenantId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken)).ConfigureAwait(false);

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

		// Addressed by the full ruled saga identity (TenantId, SagaId), not by SagaId alone: two tenants' sagas
		// can share a SagaId, so a tenant-less predicate deletes another tenant's pending timeout.
		var partition = CurrentPartition();
		var sql = $"DELETE FROM {_options.QualifiedTableName} WHERE TenantId = :TenantId AND SagaId = :SagaId AND TimeoutId = :TimeoutId";

		var dp = new DynamicParameters();
		dp.Add("TenantId", partition.TenantId);
		dp.Add("SagaId", sagaId);
		dp.Add("TimeoutId", timeoutId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken)).ConfigureAwait(false);

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

		// Cancel-all is the most dangerous of the tenant-less predicates: keyed on SagaId alone it deletes EVERY
		// tenant's timeouts for a shared SagaId, not just the caller's.
		var partition = CurrentPartition();
		var sql = $"DELETE FROM {_options.QualifiedTableName} WHERE TenantId = :TenantId AND SagaId = :SagaId";

		var dp = new DynamicParameters();
		dp.Add("TenantId", partition.TenantId);
		dp.Add("SagaId", sagaId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
			sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken)).ConfigureAwait(false);

		_ = (activity?.SetTag("timeout.count", rowsAffected));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogAllTimeoutsCancelled(sagaId, rowsAffected);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		using var activity = ActivitySource.StartActivity("ClaimDueTimeouts");
		_ = (activity?.SetTag("timeout.as_of", asOf.ToString("O")));
		_ = (activity?.SetTag("timeout.batch_size", batchSize));
		_ = (activity?.SetTag("timeout.processor_id", _options.ProcessorId));

		// Oracle claim — PL/SQL cursor with TRUE skip-locked, mirroring the outbox
		// ReserveOutboxMessages pattern (SA-ruled, bead): ORA-02014 forbids `FETCH FIRST n`
		// in the same statement as `FOR UPDATE SKIP LOCKED`, so the row cap lives on the CURSOR
		// FETCH rather than the SELECT. Opening a `... FOR UPDATE SKIP LOCKED` cursor and
		// `FETCH BULK COLLECT INTO ... LIMIT n` locks exactly n eligible rows and skips rows
		// already locked by a concurrent claimer's open cursor, so two concurrent claimers always
		// claim disjoint id sets. The FORALL then stamps ClaimedBy/ClaimedAt on exactly those n
		// rows under the locks; the select-back reads the rows this processor now owns.
		var claimSql = $"""
			DECLARE
			    CURSOR eligible IS
			        SELECT TimeoutId
			        FROM {_options.QualifiedTableName}
			        WHERE DueAt <= :AsOf
			          AND (ClaimedAt IS NULL OR ClaimedAt < :AsOf - NUMTODSINTERVAL(:LeaseTimeoutSeconds, 'SECOND'))
			        ORDER BY DueAt
			        FOR UPDATE SKIP LOCKED;
			    TYPE id_table IS TABLE OF {_options.QualifiedTableName}.TimeoutId%TYPE;
			    claimed_ids id_table;
			BEGIN
			    OPEN eligible;
			    FETCH eligible BULK COLLECT INTO claimed_ids LIMIT :BatchSize;
			    CLOSE eligible;
			    FORALL i IN 1 .. claimed_ids.COUNT
			        UPDATE {_options.QualifiedTableName}
			        SET ClaimedAt = SYSTIMESTAMP, ClaimedBy = :ClaimToken
			        WHERE TimeoutId = claimed_ids(i);
			END;
			""";

		// The select-back reads THIS claim, not this processor's history.
		//
		// Keying the read on the processor id alone returns every row the processor has ever claimed and
		// not yet deleted -- including the batch a previous, still-in-flight poll is delivering right now.
		// The result is unbounded by batchSize and re-delivers rows that are already being delivered, so a
		// second poll cycle both violates the batch contract and duplicates work. Each claim therefore
		// stamps a token unique to that call and reads exactly the rows it just stamped.
		//
		// The token carries the processor id as a prefix so a row still names its owner in a diagnostic
		// query. Crash recovery is unaffected: a claim that dies before delivery leaves its rows stamped
		// with a token nobody will read again, and they become eligible once the lease expires -- which is
		// keyed on ClaimedAt, not on the token. Lease expiry, not owner identity, is the recovery mechanism.
		//
		// FETCH FIRST is legal here (no FOR UPDATE in this statement, so ORA-02014 does not apply) and caps
		// the read even if a caller's token were ever reused.
		var selectSql = $"""
			SELECT TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt, TenantId
			FROM {_options.QualifiedTableName}
			WHERE ClaimedBy = :ClaimToken
			ORDER BY DueAt ASC
			FETCH FIRST :BatchSize ROWS ONLY
			""";

		// Per-statement DynamicParameters. Both statements are wrapped in OracleDynamicParameters, which sets
		// OracleCommand.BindByName = true, so parameters bind by name and the claim's repeated :AsOf resolves
		// correctly. Do not "correct" that: under ODP.NET's positional default, a placeholder appearing twice
		// consumes two parameters and this statement would silently mis-bind. The per-statement split is kept
		// because each statement should carry only the parameters it names — a shared set is a standing
		// invitation to reintroduce the positional footgun the moment BindByName is lost.
		//
		// Unique per claim call. Not security material -- it identifies a batch, it does not authorize
		// anything -- so a Guid is the right primitive here rather than a CSPRNG draw.
		var claimToken = FormattableString.Invariant($"{_options.ProcessorId}:{Guid.NewGuid():N}");

		var claimParameters = new DynamicParameters();
		claimParameters.Add("AsOf", asOf);
		claimParameters.Add("LeaseTimeoutSeconds", _options.LeaseTimeoutSeconds);
		claimParameters.Add("BatchSize", batchSize);
		claimParameters.Add("ClaimToken", claimToken);

		// Positional binding: this set must match the select-back's placeholder order (:ClaimToken, :BatchSize).
		var selectParameters = new DynamicParameters();
		selectParameters.Add("ClaimToken", claimToken);
		selectParameters.Add("BatchSize", batchSize);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			claimSql, new OracleDynamicParameters(claimParameters), cancellationToken: cancellationToken)).ConfigureAwait(false);

		var results = await connection.QueryAsync<TimeoutRecord>(new CommandDefinition(
			selectSql, new OracleDynamicParameters(selectParameters), cancellationToken: cancellationToken)).ConfigureAwait(false);

		var timeouts = results
			.Select(r => new SagaTimeout(
				r.TimeoutId,
				r.SagaId,
				r.SagaType,
				r.TimeoutType,
				r.TimeoutData,
				r.DueAt,
				r.ScheduledAt)
			{
				// Carried out so the delivery service can re-establish this timeout's own tenant before dispatch.
				// Read through the store-read factory, which maps a legacy NULL or the sentinel onto the
				// untenanted partition without rejecting either.
				TenantId = KeyedTenantPartition.FromStoredValue(r.TenantId).TenantId,
			})
			.ToList();

		_ = (activity?.SetTag("timeout.count", timeouts.Count));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogTimeoutsClaimed(timeouts.Count, _options.ProcessorId);
		}

		return timeouts;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("GetDueTimeouts");
		_ = (activity?.SetTag("timeout.as_of", asOf.ToString("O")));

		// Plain read: no FOR UPDATE, no claim/lease mutation - diagnostic only.
		var sql = $"""
			SELECT TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt, TenantId
			FROM {_options.QualifiedTableName}
			WHERE DueAt <= :AsOf
			ORDER BY DueAt ASC
			""";

		var dp = new DynamicParameters();
		dp.Add("AsOf", asOf);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var results = await connection.QueryAsync<TimeoutRecord>(new CommandDefinition(
			sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken)).ConfigureAwait(false);

		var timeouts = results
			.Select(r => new SagaTimeout(
				r.TimeoutId,
				r.SagaId,
				r.SagaType,
				r.TimeoutType,
				r.TimeoutData,
				r.DueAt,
				r.ScheduledAt)
			{
				// Carried out so the delivery service can re-establish this timeout's own tenant before dispatch.
				// Read through the store-read factory, which maps a legacy NULL or the sentinel onto the
				// untenanted partition without rejecting either.
				TenantId = KeyedTenantPartition.FromStoredValue(r.TenantId).TenantId,
			})
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

		// Scoped to the ambient tenant as well as the TimeoutId. TimeoutId is the primary key and is globally
		// unique, so the tenant term is not what identifies the row — it is what prevents one tenant retiring
		// another's timeout by identifier. The liveness requirement this creates is deliberate: the ambient tenant
		// must be the timeout's own, or the DELETE matches nothing and the timeout is redelivered forever. The
		// delivery service establishes each claimed timeout's tenant around both the dispatch and this call.
		var partition = CurrentPartition();
		var sql = $"DELETE FROM {_options.QualifiedTableName} WHERE TenantId = :TenantId AND TimeoutId = :TimeoutId";

		var dp = new DynamicParameters();
		dp.Add("TenantId", partition.TenantId);
		dp.Add("TimeoutId", timeoutId);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogTimeoutDelivered(timeoutId);
		}
	}

	private static Func<OracleConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new OracleConnection(connectionString);
	}

	/// <summary>
	/// Internal record for Dapper mapping.
	/// </summary>
	/// <remarks>
	/// Settable properties, not a positional record: Dapper binds a positional record through its constructor,
	/// and the Oracle provider's native column types do not convert on a constructor parameter. Property
	/// assignment does convert, so the row materializes rather than throwing on the first read.
	/// </remarks>
	private sealed class TimeoutRecord
	{
		public string TimeoutId { get; set; } = string.Empty;

		public string SagaId { get; set; } = string.Empty;

		public string SagaType { get; set; } = string.Empty;

		public string TimeoutType { get; set; } = string.Empty;

		public byte[]? TimeoutData { get; set; }

		public DateTimeOffset DueAt { get; set; }

		public DateTimeOffset ScheduledAt { get; set; }

		/// <summary>
		/// Gets or sets the owning tenant term as stored on the row.
		/// </summary>
		/// <value>The stored tenant term, or <see langword="null"/> on a legacy row.</value>
		/// <remarks>
		/// Nullable even though the column is NOT NULL: a table created by an earlier version of the shipped
		/// script, before the discriminator existed, can still hold NULL until its upgrade path has run. The
		/// projection maps that through the store-read factory rather than rejecting it, so a legacy row is
		/// treated as untenanted instead of aborting the whole claimed batch on the first one.
		/// </remarks>
		public string? TenantId { get; set; }
	}
}
