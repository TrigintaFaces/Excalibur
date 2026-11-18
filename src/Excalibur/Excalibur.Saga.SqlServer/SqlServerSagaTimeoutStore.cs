// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Excalibur.Data;

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
	private readonly ITenantContext _tenantContext;
	private readonly SqlServerSagaTimeoutStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerSagaTimeoutStore(Func{SqlConnection}, ILogger{SqlServerSagaTimeoutStore}, ITenantContext)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerSagaTimeoutStore(
		string connectionString,
		ILogger<SqlServerSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString),
			new SqlServerSagaTimeoutStoreOptions(),
			logger,
			tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStore"/> class with options.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="options">The saga timeout store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
	public SqlServerSagaTimeoutStore(
		string connectionString,
		IOptions<SqlServerSagaTimeoutStoreOptions> options,
		ILogger<SqlServerSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString),
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			tenantContext)
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
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
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
		ILogger<SqlServerSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(connectionFactory,
			new SqlServerSagaTimeoutStoreOptions(),
			logger,
			tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStore"/> class with a connection factory and options.
	/// </summary>
	/// <param name="connectionFactory">The connection factory.</param>
	/// <param name="options">The saga timeout store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The tenant context. Required: this store partitions timeouts by tenant and resolves that partition
	/// from here, so there is no state in which the partition is undecided. A single-tenant host receives the
	/// framework default context and operates as the one canonical tenant.
	/// </param>
	public SqlServerSagaTimeoutStore(
		Func<SqlConnection> connectionFactory,
		IOptions<SqlServerSagaTimeoutStoreOptions> options,
		ILogger<SqlServerSagaTimeoutStore> logger,
		ITenantContext tenantContext)
		: this(connectionFactory,
			options?.Value ?? throw new ArgumentNullException(nameof(options)),
			logger,
			tenantContext)
	{
	}

	private SqlServerSagaTimeoutStore(
		Func<SqlConnection> connectionFactory,
		SqlServerSagaTimeoutStoreOptions options,
		ILogger<SqlServerSagaTimeoutStore> logger,
		ITenantContext tenantContext)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		// Stable protocol component; ambient identity and configured table are resolved per operation.
		_timerRecovery = new SqlSagaTimerRecoveryReceiver(_connectionFactory, () => CurrentPartition().TenantId, CaptureQualifiedTarget);
        _failureStore = new SqlSagaTimeoutFailureStore(() => CurrentPartition().TenantId, CaptureQualifiedTarget, _timerRecovery);
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

		var qualifiedTarget = CaptureQualifiedTarget();

		using var activity = ActivitySource.StartActivity("ScheduleTimeout");
		_ = (activity?.SetTag("saga.id", timeout.SagaId));
		_ = (activity?.SetTag("timeout.id", timeout.TimeoutId));
		_ = (activity?.SetTag("timeout.type", timeout.TimeoutType));

		// The owning tenant is stamped from the AMBIENT scope, not from timeout.TenantId. The caller does not
		// supply it: a scheduled timeout must not be able to claim a tenant its caller never established, and the
		// ambient tenant is the isolation authority every other store here uses. timeout.TenantId is an
		// output-of-read on this type and is deliberately ignored on the write.
		var partition = CurrentPartition();

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (await TryScheduleWithRecoveryAuthorityAsync(connection, qualifiedTarget, timeout, cancellationToken).ConfigureAwait(false))
        {
            if (_logger.IsEnabled(LogLevel.Debug)) { LogTimeoutScheduled(timeout.TimeoutId, timeout.SagaId); }
            return;
        }

		var hasOwnedColumns = await HasOwnedClaimColumnsAsync(connection, qualifiedTarget, cancellationToken).ConfigureAwait(false);
		var ordinaryColumns = hasOwnedColumns ? ", OwnedIncarnationId, OwnedGeneration, OwnedStateType" : string.Empty;
		var ordinaryValues = hasOwnedColumns ? ", NULL, NULL, NULL" : string.Empty;

		var sql = $@"
            INSERT INTO {qualifiedTarget}
                (TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt, TenantId{ordinaryColumns})
            VALUES
                (@TimeoutId, @SagaId, @SagaType, @TimeoutType, @TimeoutData, @DueAt, @ScheduledAt, @TenantId{ordinaryValues})";


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
				timeout.ScheduledAt,
				TenantId = partition.TenantId
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

		_ = await CancelWithRetainedAuthorityAsync(sagaId, timeoutId, cancellationToken).ConfigureAwait(false);

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

		var rowsAffected = await CancelWithRetainedAuthorityAsync(sagaId, null, cancellationToken).ConfigureAwait(false);

		_ = (activity?.SetTag("timeout.count", rowsAffected));

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogAllTimeoutsCancelled(sagaId, rowsAffected);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ClaimedSagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var qualifiedTarget = CaptureQualifiedTarget();

		using var activity = ActivitySource.StartActivity("ClaimDueTimeouts");
		_ = (activity?.SetTag("timeout.as_of", asOf.ToString("O")));
		_ = (activity?.SetTag("timeout.batch_size", batchSize));
		_ = (activity?.SetTag("timeout.processor_id", _options.ProcessorId));

		// Atomic claim + fetch, mirroring the outbox lease-claim pattern: an ordered CTE selects
		// the eligible rows (due, and either never claimed or whose lease has gone stale), then
		// UPDATE sets lease ownership and OUTPUT returns the claimed rows in one round trip.
		// Two concurrent callers can never claim the same row: SQL Server holds the UPDLOCK for
		// the duration of the statement, and READPAST skips rows already locked by another
		// concurrent claimer instead of blocking on them.
		// SYSDATETIMEOFFSET(), not SYSUTCDATETIME(). The lease columns are DATETIMEOFFSET, and comparing one
		// against SYSUTCDATETIME()'s DATETIME2 forces an implicit conversion that drops the offset — the same
		// defect in the comparison that the column type was widened to remove. DATETIMEOFFSET comparisons are
		// evaluated on the underlying instant, so a server in any time zone reaches the same verdict.
		// ClaimedBy stamps THIS CLAIM, not this process. ProcessorId is {MachineName}:{ProcessId} and is
		// constant across every claim a process makes -- and it is a settable option, so two pods configured
		// from one config map share it exactly. Either way a retirement predicate testing it could not tell a
		// processor's stale claim from its own re-claim, and would silently permit the foreign delete it was
		// added to prevent. A per-claim suffix makes the two distinguishable, which is the whole point.
		// This is an ownership identity, not a secret.
		var claimToken = $"{_options.ProcessorId}:{Guid.NewGuid():N}";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		var hasOwnedColumns = await HasOwnedClaimColumnsAsync(connection, qualifiedTarget, cancellationToken).ConfigureAwait(false);

		// NOTE — this claim is deliberately ESTATE-WIDE and must stay that way. It is the background delivery
		// loop's lease, mirroring the outbox drain: it leases due timeouts across every tenant in one batch.
		// Adding an ambient tenant predicate here would make a tenant-less background service claim only the
		// untenanted partition, so every tenant's timeouts would sit due and unclaimed forever — a total stall
		// that presents as silence, not as an error. Isolation is enforced instead by returning each row's own
		// TenantId so the delivery service can re-establish that tenant before dispatching.


        var preserving = await _failureStore.TryClaimAsync(connection, qualifiedTarget, asOf, batchSize,
            _options.LeaseTimeoutSeconds, claimToken, cancellationToken).ConfigureAwait(false);
        var timeouts = preserving ?? (await connection.ResolveAsync(new ClaimTimeoutRowsRequest(qualifiedTarget, hasOwnedColumns,
            asOf, batchSize, _options.LeaseTimeoutSeconds, claimToken, cancellationToken)).ConfigureAwait(false))
            .Select(row => CreateClaim(row, claimToken)).ToList();

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
		cancellationToken.ThrowIfCancellationRequested();

		using var activity = ActivitySource.StartActivity("GetDueTimeouts");
		_ = (activity?.SetTag("timeout.as_of", asOf.ToString("O")));


		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var results = await connection.ResolveAsync(new GetDueTimeoutRowsRequest(
            _options.QualifiedTableName, asOf, cancellationToken)).ConfigureAwait(false);

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
	public async Task<SagaTimeoutRetirementOutcome> MarkDeliveredAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(claim);

		var timeoutId = claim.Timeout.TimeoutId;

		using var activity = ActivitySource.StartActivity("MarkDelivered");
		_ = (activity?.SetTag("timeout.id", timeoutId));

		// Scoped to the ambient tenant as well as the TimeoutId. TimeoutId is the primary key and is globally
		// unique, so the tenant term is not what identifies the row — it is what prevents one tenant from
		// retiring another's timeout by identifier.
		//
		// The liveness requirement this creates is deliberate and must be honoured by callers: the ambient tenant
		// here has to be the timeout's own tenant, or the DELETE matches nothing and the timeout is redelivered
		// forever. The delivery service establishes each claimed timeout's tenant around BOTH the dispatch and
		// this call for exactly that reason. A caller that retires a timeout outside its tenant scope is the
		// failure mode to watch for.
		var partition = CurrentPartition();
        var qualifiedTarget = CaptureQualifiedTarget();
		// AND ClaimedBy = @ClaimToken is what makes this retirement OWNED rather than merely addressed. The
		// tenant term stops a foreign tenant retiring by identifier; it does nothing about a processor whose
		// lease has already been taken over by another. Without the claim term a stalled processor that
		// resumes deletes the row out from under the live claimant, whose own delivery then has nothing left
		// to retry -- zero deliveries and no row. One conditional DELETE is a single atomic step, so there is
		// no window between testing ownership and acting on it.
		var sql = $"""
            DELETE FROM {qualifiedTarget}
            WHERE TenantId = @TenantId
              AND DATALENGTH(TenantId) = DATALENGTH(@TenantId)
              AND CONVERT(VARBINARY(128), TenantId) = CONVERT(VARBINARY(128), @TenantId) AND TimeoutId = @TimeoutId AND ClaimedBy = @ClaimToken
            """;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var preserving = await _failureStore.TryRetireAsync(connection, qualifiedTarget, partition.TenantId, claim, cancellationToken).ConfigureAwait(false);
        if (preserving is { } disposition)
        {
            if (disposition == SagaTimeoutRetirementOutcome.Retired && _logger.IsEnabled(LogLevel.Debug)) { LogTimeoutDelivered(timeoutId); }
            return disposition;
        }


		var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new { TenantId = partition.TenantId, TimeoutId = timeoutId, ClaimToken = claim.ClaimToken },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		// Zero rows means the claim presented is no longer the current one. It is reported, not swallowed:
		// the caller must be able to tell "retired" from "someone else owns this now", and a void return
		// made those the same observation.
		if (rowsAffected == 0)
		{
			return SagaTimeoutRetirementOutcome.Superseded;
		}

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogTimeoutDelivered(timeoutId);
		}

		return SagaTimeoutRetirementOutcome.Retired;
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new SqlConnection(connectionString);
	}

	/// <summary>
	/// Internal record for Dapper mapping.
	/// </summary>
	/// <remarks>
	/// <c>TenantId</c> is nullable here even though the column is NOT NULL: a table created by an earlier version
	/// of the shipped script, before the discriminator existed, can still hold NULL until its upgrade path has
	/// run. The projection maps that through the store-read factory rather than rejecting it, so a legacy row is
	/// treated as untenanted instead of aborting the whole claimed batch on the first one.
	/// </remarks>
	private sealed class TimeoutRecord
	{
		/// <summary>Gets or sets the exact timeout ID.</summary>
		public string TimeoutId { get; set; } = string.Empty;
		/// <summary>Gets or sets the saga ID.</summary>
		public string SagaId { get; set; } = string.Empty;
		/// <summary>Gets or sets the saga routing type.</summary>
		public string SagaType { get; set; } = string.Empty;
		/// <summary>Gets or sets the timeout routing type.</summary>
		public string TimeoutType { get; set; } = string.Empty;
		/// <summary>Gets or sets the encoded payload.</summary>
		public byte[]? TimeoutData { get; set; }
		/// <summary>Gets or sets the due instant.</summary>
		public DateTimeOffset DueAt { get; set; }
		/// <summary>Gets or sets the original scheduling instant.</summary>
		public DateTimeOffset ScheduledAt { get; set; }
		/// <summary>Gets or sets the stored tenant identity.</summary>
		public string? TenantId { get; set; }
		/// <summary>Gets or sets the original owned incarnation.</summary>
		public Guid? OwnedIncarnationId { get; set; }
		/// <summary>Gets or sets the original scheduling generation.</summary>
		public long? OwnedGeneration { get; set; }
		/// <summary>Gets or sets the original state storage type.</summary>
		public string? OwnedStateType { get; set; }
	}
}
