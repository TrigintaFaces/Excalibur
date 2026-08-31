// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Globalization;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Inbox.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Excalibur.Inbox.Oracle;

/// <summary>
/// Oracle Database implementation of <see cref="IInboxStore"/> for idempotent message processing.
/// </summary>
/// <remarks>
/// <para>
/// Provides reliable message deduplication and processing tracking using Oracle. Messages are keyed by a
/// composite of (MessageId, HandlerType), allowing the same message to be processed independently by
/// multiple handlers.
/// </para>
/// <para>
/// Oracle has no <c>MERGE ... OUTPUT $action</c>, so first-writer-wins semantics
/// (<see cref="TryMarkAsProcessedAsync"/>, <see cref="TryClaimAsync(string, string, CancellationToken)"/>)
/// are implemented as an <c>INSERT</c> guarded by the unique key: a duplicate raises <c>ORA-00001</c>,
/// which the store interprets as "not the first writer". Concurrent processors of the same key in the
/// transactional path are serialized with <c>SELECT ... FOR UPDATE</c> after ensuring a row exists.
/// </para>
/// <para>
/// All Dapper SQL uses <c>:name</c> bind tokens with a distinct name for every occurrence within a single
/// statement, so binding is correct regardless of the provider's <c>BindByName</c> default (Oracle binds
/// positionally by default; distinct names keep parameter order unambiguous).
/// </para>
/// </remarks>
public sealed class OracleInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, ILeasedInboxStore, IInboxStoreAdmin, IBackoffSchedulableInboxStore, ITransactionalInboxStore, IScopedTransactionalInboxStore, IInboxStoreCapabilities, IInboxSchemaValidator
{
	// ORA-00001: unique constraint violated.
	private const int OracleUniqueViolation = 1;

	/// <inheritdoc/>
	public async ValueTask ValidateSchemaAsync(CancellationToken cancellationToken)
		=> await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	public bool SupportsClaim => true;

	/// <inheritdoc/>
	public bool SupportsLeasedClaim => true;

	/// <inheritdoc/>
	public bool SupportsProcessingTracking => true;

	/// <inheritdoc/>
	public bool SupportsTransactional => true;

	/// <inheritdoc />
	/// <remarks>This store implements both transactional seams, so it offers the scoped one too.</remarks>
	public bool SupportsScopedTransactional => true;

	/// <inheritdoc/>
	/// <remarks>This store implements IBackoffSchedulableInboxStore and persists NextAttemptAt.</remarks>
	public bool SupportsBackoffScheduling => true;

	private readonly Func<OracleConnection> _connectionFactory;
	private readonly OracleInboxOptions _options;
	private readonly ILogger<OracleInboxStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private KeyedTenantPartition CurrentTenantPartition =>
		KeyedTenantPartition.FromContext(_tenantContext);


	/// <summary>
	/// Gets the tenant term bound to every inbox row, for both the write and the match.
	/// </summary>
	/// <remarks>
	/// Always a concrete, non-null term: an unscoped host resolves to the reserved untenanted sentinel
	/// rather than to <see langword="null"/>. Binding the scope's nullable identifier directly was the
	/// defect — an untenanted host wrote NULL into the tenant column while every lookup used a bare
	/// equality, and NULL never equals NULL in SQL. The row was written and then unreachable, so the
	/// entry stayed unprocessed forever and neither side raised an error.
	/// <para>
	/// Routing every site through one property is what makes that inexpressible: there is no per-call-site
	/// opportunity left to bind the nullable value by hand.
	/// </para>
	/// </remarks>
	private string TenantTerm =>
		CurrentTenantPartition.TenantId;

	private string Table => _options.QualifiedTableName;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options; its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode for the startup schema handshake.
	/// </param>
	// Deterministic DI construction: the advanced constructor below also accepts an ITenantContext, so
	// without this marker ActivatorUtilities' selection depends on which services happen to be
	// registered, and reports a missing dependency as a constructor ambiguity.
	[ActivatorUtilitiesConstructor]
	public OracleInboxStore(
		IOptions<OracleInboxOptions> options,
		ILogger<OracleInboxStore> logger,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
		: this(CreateConnectionFactory(options!.Value), options.Value, logger, tenantContext, tenantContextOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleInboxStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="OracleConnection"/> instances.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options; its <see cref="TenantContextOptions.RequireTenant"/> selects the deployment
	/// mode for the startup schema handshake.
	/// </param>
	public OracleInboxStore(
		Func<OracleConnection> connectionFactory,
		OracleInboxOptions options,
		ILogger<OracleInboxStore> logger,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_options = options;
		_logger = logger;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		// Deployment mode: multi-tenant iff the consumer opted in via AddMultiTenancy() (which sets
		// TenantContextOptions.RequireTenant) — NOT "is an ITenantContext present". Drives the leak-check.
		ArgumentNullException.ThrowIfNull(tenantContextOptions);
		_requireTenant = tenantContextOptions.Value.RequireTenant;
		// Canonical event-serialization contract shared with every store and the default serializer.
		_jsonOptions = EventSerializationDefaults.Canonical;
	}

	private readonly bool _requireTenant;

	// Whether the physical unique key includes the TenantId column. Read once from the live schema and
	// cached; SQL emission follows THIS (the actual column), never the mode flag.
	private volatile bool _schemaContractVerified;
	private bool _hasTenantColumn;

	private OracleConnection OpenBoundConnection()
	{
		var connection = _connectionFactory();
		// Bind parameters by name so a statement that reuses a bind name (or lists them out of positional
		// order) binds correctly. Distinct names are still used per statement as defence in depth.
		connection.BindByName = true;
		return connection;
	}

	private async ValueTask<OracleConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		var connection = OpenBoundConnection();
		try
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
			return connection;
		}
		catch
		{
			await connection.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	// The (b) fail-closed floor + emission driver: reads the physical schema once, verifies the deployment-
	// mode ↔ schema contract (fail-fast on mismatch), caches whether the tenant column is in the key, and
	// returns it so the caller emits the tenant term iff the column exists. Benign-idempotent — no lock. The
	// (a) hosted-service validator calls this at startup so host consumers fail before the first message.
	internal async ValueTask<bool> EnsureSchemaAsync(CancellationToken cancellationToken)
	{
		if (_schemaContractVerified)
		{
			return _hasTenantColumn;
		}

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		// I1 ENFORCED, not asserted in a comment: the dedup/claim key MUST be the triple
		// (MessageId, HandlerType, TenantId) and TenantId MUST be NOT NULL, so an untenanted host can never
		// collide with a real tenant on the pair key. Read the actual physical key and fail fast on a
		// mismatch, rather than trusting the operator to have applied a migration. Oracle folds unquoted
		// identifiers to upper case, so the catalog is queried with the upper-cased table/column names.
		var tableName = _options.TableName.ToUpperInvariant();

		var primaryKeyColumns = (await connection.QueryAsync<string>(
			new CommandDefinition(
				"""
				SELECT ucc.column_name
				FROM user_constraints uc
				JOIN user_cons_columns ucc ON ucc.constraint_name = uc.constraint_name
				WHERE uc.constraint_type = 'P' AND uc.table_name = :TableName
				ORDER BY ucc.position
				""",
				new { TableName = tableName },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

		// 'Y' / 'N' as a string avoids any provider-specific boolean coercion; null = column absent.
		var nullableFlag = await connection.ExecuteScalarAsync<string?>(
			new CommandDefinition(
				"SELECT nullable FROM user_tab_columns WHERE table_name = :TableName AND column_name = 'TENANTID'",
				new { TableName = tableName },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		var tenantIdIsNullable = nullableFlag is null ? (bool?)null : string.Equals(nullableFlag, "Y", StringComparison.OrdinalIgnoreCase);

		_hasTenantColumn = InboxSchemaContract.Verify(
			Table, _requireTenant, primaryKeyColumns, tenantIdIsNullable,
			"MessageId", "HandlerType", "TenantId");
		_schemaContractVerified = true;
		return _hasTenantColumn;
	}


	/// <inheritdoc/>
	public async ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);

		using var activity = InboxActivitySource.StartCreateEntryActivity(messageId, handlerType);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);

		// Tenant scope derives ONLY from the ambient context, never the row's own value. Deployment mode
		// decides the shape: CurrentTenantScope => None — a non-multi-tenant deployment has no TenantId column,
		// so the column and parameter are omitted entirely (zero bloat). A registered ITenantContext =>
		// Scoped — the TenantId column is written and the tenant term rides every keyed path. A context that
		// resolves no tenant fails closed (throws) rather than reaching a predicate-less query.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var tenantValue = hasTenantColumn ? ", :TenantId" : string.Empty;

		var sql = $"""
		           INSERT INTO {Table}
		           	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, Status, RetryCount, CorrelationId, Source{tenantColumn})
		           VALUES
		           	(:MessageId, :HandlerType, :MessageType, :Payload, :Metadata, :ReceivedAt, :Status, :RetryCount, :CorrelationId, :Source{tenantValue})
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				entry.MessageId,
				entry.HandlerType,
				entry.MessageType,
				entry.Payload,
				Metadata = SerializeMetadata(entry.Metadata),
				entry.ReceivedAt,
				Status = (int)entry.Status,
				entry.RetryCount,
				entry.CorrelationId,
				TenantId = TenantTerm,
				entry.Source
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
			_logger.LogDebug("Created inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return entry;
		}
		catch (OracleException ex) when (ex.Number == OracleUniqueViolation)
		{
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.", ex);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		var sql = $"""
		           UPDATE {Table}
		           SET Status = :ProcessedStatus, ProcessedAt = :ProcessedAt, LastAttemptAt = :LastAttemptAt, LastError = NULL
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType AND Status <> :ProcessedStatusFilter{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var command = new CommandDefinition(
			sql,
			new
			{
				ProcessedStatus = (int)InboxStatus.Processed,
				ProcessedAt = now,
				LastAttemptAt = now,
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessedStatusFilter = (int)InboxStatus.Processed,
				TenantId = TenantTerm
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (affected == 0)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found or already processed for message '{messageId}' and handler '{handlerType}'.");
		}

		_logger.LogDebug("Marked inbox entry as processed for message {MessageId} and handler {HandlerType}", messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IDbTransaction, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(handler);

		// One connection + one LOCAL transaction: claim-check, handler writes, and processed-mark all run on
		// THIS connection/transaction, committing or rolling back atomically. To serialize concurrent
		// processors of an as-yet-nonexistent key (Oracle FOR UPDATE cannot lock a missing row), first ensure
		// a placeholder row exists (INSERT, ORA-00001 ignored), then SELECT ... FOR UPDATE to hold the row for
		// the duration of the transaction — mirrors the Postgres INSERT-ON-CONFLICT + FOR UPDATE path.
		// Composite tenant key on the claim/dedup path, by deployment mode. Multi-tenant (an ITenantContext is
		// registered) → the resolved tenant joins the placeholder INSERT + the FOR UPDATE read predicate + the
		// processed-mark UPDATE (the triple), so two tenants sharing a message id never dedup against each
		// other. Non-MT → CurrentTenantScope=None: no tenant column/param/predicate emitted. An
		// MT-active-but-unresolved context fails closed.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			await EnsurePlaceholderRowAsync(connection, transaction, messageId, handlerType, cancellationToken).ConfigureAwait(false);

			var existingStatus = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
				$"SELECT Status FROM {Table} WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate} FOR UPDATE",
				new { MessageId = messageId, HandlerType = handlerType, TenantId = TenantTerm },
				transaction: transaction,
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

			if (existingStatus == (int)InboxStatus.Processed)
			{
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogDebug("Duplicate — inbox message {MessageId}/{HandlerType} already processed; handler skipped", messageId, handlerType);
				return false;
			}

			// Run the handler INSIDE this transaction; its writes enlist by passing transaction: tx.
			await handler(transaction, cancellationToken).ConfigureAwait(false);

			var now = DateTimeOffset.UtcNow;
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$"""
				 UPDATE {Table}
				 SET Status = :ProcessedStatus, ProcessedAt = :ProcessedAt, LastAttemptAt = :LastAttemptAt, LastError = NULL
				 WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate}
				 """,
				new { ProcessedStatus = (int)InboxStatus.Processed, ProcessedAt = now, LastAttemptAt = now, MessageId = messageId, HandlerType = handlerType, TenantId = TenantTerm },
				transaction: transaction,
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("Transactionally processed inbox message {MessageId} for handler {HandlerType}", messageId, handlerType);
			return true;
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc cref="IScopedTransactionalInboxStore.TryProcessTransactionallyAsync" />
	/// <remarks>
	/// Wires the inbox middleware's scoped exactly-once seam onto the relational implementation above. The
	/// middleware selects the atomic path by type-testing for the scoped interface, so without this bridge an
	/// undecorated Oracle store advertises <see cref="SupportsTransactional"/> and still falls through to the
	/// at-least-once claim protocol. The active BCL <see cref="IDbTransaction"/> is wrapped in the opaque
	/// <see cref="IInboxTransactionScope"/> so a handler enlists its own writes via <c>scope.AsSqlTransaction()</c>,
	/// committing atomically with the processed-mark. Guarantees are inherited unchanged from the overload above.
	/// </remarks>
	public ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IInboxTransactionScope, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		return TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(tx, ct) => handler(new SqlInboxTransactionScope(tx), ct),
			cancellationToken);
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		// Processed is absorbing: refuse rather than demote a finalized entry back to Processing, which
		// would re-admit the message and run the handler again. The guard sits in SET rather than in
		// WHERE (where MarkFailedAsync carries it) because this method reports a missing row by throwing
		// off the affected count, and a WHERE-side guard would make a refused Processed entry
		// indistinguishable from an absent one. Guarding the assignment leaves affected = 0 meaning
		// exactly "no such row" while the transition is still evaluated atomically under the row lock.
		var sql = $"""
		           UPDATE {Table}
		           SET Status = CASE WHEN Status = :ProcessedStatus THEN Status ELSE :ProcessingStatus END,
		               LastAttemptAt = CASE WHEN Status = :ProcessedStatus THEN LastAttemptAt ELSE :LastAttemptAt END
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				ProcessingStatus = (int)InboxStatus.Processing,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastAttemptAt = DateTimeOffset.UtcNow,
				MessageId = messageId,
				HandlerType = handlerType,
				TenantId = TenantTerm
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (affected == 0)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		_logger.LogDebug("Marked inbox entry as processing for message {MessageId} and handler {HandlerType}", messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// First-writer-wins: attempt to INSERT the terminal Processed row. Success => this caller was first.
		// ORA-00001 (row already exists for the key) => a prior writer won => false. Placeholder string
		// columns are NULL (Oracle folds '' -> NULL); the identity columns (MessageId/HandlerType) are
		// non-empty by contract so the unique key is never defeated by the fold.
		// When scoped, the ambient tenant joins the composite key (there is no Oracle ON CONFLICT — the
		// triple unique key enforces first-writer-wins across tenants), so tenant B's message can never be
		// suppressed by tenant A's matching (MessageId, HandlerType).
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var insertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", :TenantId" : string.Empty;
		var sql = $"""
		           INSERT INTO {Table}
		           	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount{insertTenantCol})
		           VALUES
		           	(:MessageId, :HandlerType, NULL, EMPTY_BLOB(), :EmptyJson, :ReceivedAt, :ProcessedAt, :ProcessedStatus, 0{insertTenantVal})
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ReceivedAt = now, ProcessedAt = now, ProcessedStatus = (int)InboxStatus.Processed, EmptyJson = "{}", TenantId = TenantTerm },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
			_logger.LogDebug("First processor for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return true;
		}
		catch (OracleException ex) when (ex.Number == OracleUniqueViolation)
		{
			_logger.LogDebug("Duplicate detected for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// Atomic first-writer claim into the NON-TERMINAL Processing state: INSERT the claim row. Success =>
		// claimed. ORA-00001 => already claimed/processed => false. Finalized via MarkProcessedAsync, removed
		// via ReleaseAsync. When scoped, the ambient tenant joins the composite key so two tenants sharing a
		// message id claim independently.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var insertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", :TenantId" : string.Empty;
		var sql = $"""
		           INSERT INTO {Table}
		           	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount{insertTenantCol})
		           VALUES
		           	(:MessageId, :HandlerType, NULL, EMPTY_BLOB(), :EmptyJson, :ReceivedAt, NULL, :ProcessingStatus, 0{insertTenantVal})
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ReceivedAt = DateTimeOffset.UtcNow, ProcessingStatus = (int)InboxStatus.Processing, EmptyJson = "{}", TenantId = TenantTerm },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
			_logger.LogDebug("Claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return true;
		}
		catch (OracleException ex) when (ex.Number == OracleUniqueViolation)
		{
			_logger.LogDebug("Claim denied (already claimed/processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<LeaseToken?> TryAcquireLeaseAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

		// Lease CAS in two steps that together are equivalent to the SqlServer MERGE lease claim:
		//   (1) try INSERT the claim row with a fresh lease (the absent-row case) — success => claimed;
		//   (2) on ORA-00001 the row exists => conditional UPDATE that re-claims IFF the row is Received,
		//       Failed, or a Processing row whose lease has expired. Affected-rows > 0 => claimed; a live
		//       lease or a terminal Processed row matches no predicate => 0 => not claimed.
		// The lease-expiry comparison uses the SERVER clock (SYS_EXTRACT_UTC(SYSTIMESTAMP)) so competing app
		// instances never decide expiry with a skewed local clock.
		//
		// Both statements append RETURNING LeaseExpiresAtUtc INTO an output parameter so the term returned to
		// the caller is read back from the SAME DML that wrote it — no separate SELECT, so there is no window
		// in which another writer could interleave between the write and the read (Oracle has no `MERGE ...
		// OUTPUT`/multi-row RETURNING equivalent, so a plain Dapper CommandDefinition can't express this; a
		// raw OracleCommand with an output OracleParameter is the mechanism, mirroring
		// InsertEventsBatchRequest's identical INSERT ... RETURNING POSITION INTO need).
		var leaseSeconds = leaseDuration.TotalSeconds;

		// When scoped, the ambient tenant joins the composite key on the INSERT (so two tenants sharing a
		// message id lease independently) and the UPDATE re-claim predicate (so a reclaim only ever touches
		// the caller's own tenant row).
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var insertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", :TenantId" : string.Empty;
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;

		var insertSql = $"""
		                 INSERT INTO {Table}
		                 	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount, LeaseExpiresAtUtc{insertTenantCol})
		                 VALUES
		                 	(:MessageId, :HandlerType, NULL, EMPTY_BLOB(), :EmptyJson, SYS_EXTRACT_UTC(SYSTIMESTAMP), NULL, :ProcessingStatus, 0,
		                 	 SYS_EXTRACT_UTC(SYSTIMESTAMP) + NUMTODSINTERVAL(:LeaseSeconds, 'SECOND'){insertTenantVal})
		                 RETURNING LeaseExpiresAtUtc INTO :OutLease
		                 """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			await using var insertCommand = connection.CreateCommand();
#pragma warning disable CA2100 // insertSql is built from the configured Table/tenant-column shape; no user input.
			insertCommand.CommandText = insertSql;
#pragma warning restore CA2100
			insertCommand.BindByName = true;
			insertCommand.CommandTimeout = _options.CommandTimeoutSeconds;

			_ = insertCommand.Parameters.Add(new OracleParameter("MessageId", messageId));
			_ = insertCommand.Parameters.Add(new OracleParameter("HandlerType", handlerType));
			_ = insertCommand.Parameters.Add(new OracleParameter("EmptyJson", "{}"));
			_ = insertCommand.Parameters.Add(IntParameter("ProcessingStatus", (int)InboxStatus.Processing));
			_ = insertCommand.Parameters.Add(new OracleParameter("LeaseSeconds", leaseSeconds));
			if (hasTenantColumn)
			{
				_ = insertCommand.Parameters.Add(new OracleParameter("TenantId", TenantTerm));
			}

			var insertOutLease = new OracleParameter("OutLease", OracleDbType.TimeStampTZ) { Direction = ParameterDirection.Output };
			_ = insertCommand.Parameters.Add(insertOutLease);

			_ = await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Lease-claimed (new) inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return ToLeaseToken(ReadLeaseTimestamp(insertOutLease.Value));
		}
		catch (OracleException ex) when (ex.Number == OracleUniqueViolation)
		{
			var updateSql = $"""
			                 UPDATE {Table}
			                 SET Status = :NewStatus,
			                 	LeaseExpiresAtUtc = SYS_EXTRACT_UTC(SYSTIMESTAMP) + NUMTODSINTERVAL(:LeaseSeconds, 'SECOND'),
			                 	LastAttemptAt = SYS_EXTRACT_UTC(SYSTIMESTAMP)
			                 WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate}
			                 	AND (Status = :ReceivedStatus
			                 		OR Status = :FailedStatus
			                 		OR (Status = :ProcessingStatusFilter AND LeaseExpiresAtUtc < SYS_EXTRACT_UTC(SYSTIMESTAMP)))
			                 RETURNING LeaseExpiresAtUtc INTO :OutLease
			                 """;

			await using var updateCommand = connection.CreateCommand();
#pragma warning disable CA2100 // updateSql is built from the configured Table/tenant-column shape; no user input.
			updateCommand.CommandText = updateSql;
#pragma warning restore CA2100
			updateCommand.BindByName = true;
			updateCommand.CommandTimeout = _options.CommandTimeoutSeconds;

			_ = updateCommand.Parameters.Add(IntParameter("NewStatus", (int)InboxStatus.Processing));
			_ = updateCommand.Parameters.Add(new OracleParameter("LeaseSeconds", leaseSeconds));
			_ = updateCommand.Parameters.Add(new OracleParameter("MessageId", messageId));
			_ = updateCommand.Parameters.Add(new OracleParameter("HandlerType", handlerType));
			_ = updateCommand.Parameters.Add(IntParameter("ReceivedStatus", (int)InboxStatus.Received));
			_ = updateCommand.Parameters.Add(IntParameter("FailedStatus", (int)InboxStatus.Failed));
			_ = updateCommand.Parameters.Add(IntParameter("ProcessingStatusFilter", (int)InboxStatus.Processing));
			if (hasTenantColumn)
			{
				_ = updateCommand.Parameters.Add(new OracleParameter("TenantId", TenantTerm));
			}

			var updateOutLease = new OracleParameter("OutLease", OracleDbType.TimeStampTZ) { Direction = ParameterDirection.Output };
			_ = updateCommand.Parameters.Add(updateOutLease);

			var affected = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			if (affected == 0)
			{
				_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
				return null;
			}

			_logger.LogDebug("Lease-claimed (reclaimed) inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return ToLeaseToken(ReadLeaseTimestamp(updateOutLease.Value));
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> CompleteAsync(
		string messageId,
		string handlerType,
		LeaseToken lease,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		// The term is ADDITIONAL to the tenant/existence/status predicates, never a replacement: a caller
		// whose lease lapsed presents the term it lost, which matches no row (the row's term has already
		// moved on to whoever reclaimed it), so this UPDATE affects 0 rows and CompleteAsync reports false.
		var sql = $"""
		           UPDATE {Table}
		           SET Status = :ProcessedStatus, ProcessedAt = :ProcessedAt, LastAttemptAt = :LastAttemptAt, LastError = NULL
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate}
		           	AND Status <> :ProcessedStatusFilter AND LeaseExpiresAtUtc = :Lease
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var command = new CommandDefinition(
			sql,
			new
			{
				ProcessedStatus = (int)InboxStatus.Processed,
				ProcessedAt = now,
				LastAttemptAt = now,
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessedStatusFilter = (int)InboxStatus.Processed,
				TenantId = TenantTerm,
				Lease = FromLeaseToken(lease)
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (affected > 0)
		{
			_logger.LogDebug("Completed leased inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Lease-complete denied (stale term) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return affected > 0;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> FailAsync(
		string messageId,
		string handlerType,
		LeaseToken lease,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		// Clears the term on success: a failed entry has no holder, so it must not keep a term a later
		// lease comparison (CompleteAsync/FailAsync) could match. Processed is absorbing: the predicate
		// refuses the transition rather than demoting a finalized entry to Failed, which would make it
		// re-admittable and run the handler again. The term is ADDITIONAL to that guard.
		var sql = $"""
		           UPDATE {Table}
		           SET Status = :FailedStatus, LastError = :LastError, RetryCount = RetryCount + 1, LastAttemptAt = :LastAttemptAt, LeaseExpiresAtUtc = NULL
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate}
		           	AND Status <> :ProcessedStatus AND LeaseExpiresAtUtc = :Lease
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				FailedStatus = (int)InboxStatus.Failed,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastError = NormalizeError(errorMessage),
				LastAttemptAt = DateTimeOffset.UtcNow,
				MessageId = messageId,
				HandlerType = handlerType,
				TenantId = TenantTerm,
				Lease = FromLeaseToken(lease)
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (affected > 0)
		{
			_logger.LogWarning("Marked leased inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
				messageId, handlerType, errorMessage);
		}
		else
		{
			_logger.LogDebug("Lease-fail denied (stale term) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return affected > 0;
	}

	/// <summary>
	/// Binds an integer value as an explicit <see cref="OracleDbType.Int32"/> parameter.
	/// </summary>
	/// <remarks>
	/// Never write <c>new OracleParameter(name, someInt)</c> for a status or enum value. ODP.NET offers
	/// both <c>OracleParameter(string, object)</c> and <c>OracleParameter(string, OracleDbType)</c>, and a
	/// compile-time constant <c>0</c> converts implicitly to any enum — so an argument that is
	/// <i>constant zero</i> silently binds the <c>OracleDbType</c> overload as <c>(OracleDbType)0</c>,
	/// which is not a defined member, and the constructor throws
	/// <see cref="ArgumentOutOfRangeException"/> before a single statement is issued. Every other value
	/// picks the intended <c>object</c> overload, so the trap fires only for the one status that happens
	/// to be numbered zero and is invisible everywhere else. Stating the type makes the wrong overload
	/// unreachable rather than merely unlikely.
	/// </remarks>
	/// <param name="name">The bind variable name.</param>
	/// <param name="value">The integer value to bind.</param>
	/// <returns>A parameter bound as <see cref="OracleDbType.Int32"/>.</returns>
	private static OracleParameter IntParameter(string name, int value)
		=> new(name, OracleDbType.Int32) { Value = value };

	/// <summary>
	/// Encodes the server-written <c>LeaseExpiresAtUtc</c> value as the opaque <see cref="LeaseToken"/> term,
	/// via <see cref="DateTimeOffset"/>'s round-trip (<c>"O"</c>) format.
	/// </summary>
	/// <remarks>
	/// The column is <c>TIMESTAMP(7) WITH TIME ZONE</c> — 100-nanosecond precision, exactly matching
	/// <see cref="DateTimeOffset"/>'s tick resolution — so the round trip through <c>"O"</c> (7 fractional
	/// digits) loses nothing.
	/// </remarks>
	private static LeaseToken ToLeaseToken(DateTimeOffset leaseExpiresAtUtc)
		=> new(leaseExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));

	/// <summary>
	/// Decodes a <see cref="LeaseToken"/> back to the <c>DateTimeOffset</c> it was minted from, for binding
	/// as a real timestamp parameter so Oracle compares it as <c>TIMESTAMP WITH TIME ZONE</c>, never as text.
	/// </summary>
	private static DateTimeOffset FromLeaseToken(LeaseToken lease)
		=> DateTimeOffset.ParseExact(lease.Value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

	/// <summary>
	/// Converts a <c>RETURNING LeaseExpiresAtUtc INTO</c> output parameter's value to the CLR
	/// <see cref="DateTimeOffset"/> the lease-token encoding uses.
	/// </summary>
	/// <remarks>
	/// ODP.NET surfaces a <c>TIMESTAMP WITH TIME ZONE</c> RETURNING-INTO output as an
	/// <see cref="OracleTimeStampTZ"/>. Built component-wise — never via <c>ToString</c>/<c>Parse</c> — so
	/// the column's 100-nanosecond precision transfers exactly: <see cref="OracleTimeStampTZ.Nanosecond"/> is
	/// always a multiple of 100 for a <c>TIMESTAMP(7)</c> column, so dividing by 100 is an exact tick count
	/// with no rounding.
	/// </remarks>
	private static DateTimeOffset ReadLeaseTimestamp(object? value) => value switch
	{
		OracleTimeStampTZ tz => new DateTimeOffset(tz.Year, tz.Month, tz.Day, tz.Hour, tz.Minute, tz.Second, tz.GetTimeZoneOffset())
			.AddTicks(tz.Nanosecond / 100),
		DateTimeOffset dto => dto,
		DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
		null => throw new InvalidOperationException("Lease acquisition's RETURNING LeaseExpiresAtUtc produced no value."),
		_ => throw new InvalidOperationException($"Unexpected lease timestamp value type '{value.GetType()}'.")
	};

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// Remove the non-terminal claim so a redelivery can re-admit. Restricted to non-Processed rows so a
		// concurrently-finalized entry is never deleted. No-op if already removed or never claimed.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		var sql = $"""
		           DELETE FROM {Table}
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType AND Status <> :ProcessedStatus{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed, TenantId = TenantTerm },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogDebug("Released inbox claim for message {MessageId} and handler {HandlerType}", messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		var sql = $"""
		           SELECT CASE WHEN EXISTS (
		           	SELECT 1 FROM {Table}
		           	WHERE MessageId = :MessageId AND HandlerType = :HandlerType AND Status = :ProcessedStatus{tenantPredicate}
		           ) THEN 1 ELSE 0 END
		           FROM DUAL
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed, TenantId = TenantTerm },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await connection.QuerySingleAsync<bool>(command).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		var tenantSelectColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var sql = $"""
		           SELECT MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	   Status, LastError, RetryCount, LastAttemptAt, NextAttemptAt, CorrelationId, Source{tenantSelectColumn}
		           FROM {Table}
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, TenantId = TenantTerm },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var row = await connection.QuerySingleOrDefaultAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return row != null ? MapRowToEntry(row) : null;
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		// Processed is absorbing: the predicate refuses the transition rather than demoting a
		// finalized entry to Failed, which would make it re-admittable and run the handler again.
		// Also clears any lease term: a failed entry has no holder, so it must not keep a term a
		// later lease comparison (CompleteAsync/FailAsync) could match.
		var sql = $"""
		           UPDATE {Table}
		           SET Status = :FailedStatus, LastError = :LastError, RetryCount = RetryCount + 1, LastAttemptAt = :LastAttemptAt, LeaseExpiresAtUtc = NULL
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate} AND Status <> :ProcessedStatus
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				FailedStatus = (int)InboxStatus.Failed,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastError = NormalizeError(errorMessage),
				LastAttemptAt = DateTimeOffset.UtcNow,
				MessageId = messageId,
				HandlerType = handlerType,
				TenantId = TenantTerm
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		// Set RetryCount EXACTLY (no +1) so a transient short-circuit leaves the entry re-admittable
		// without consuming a delivery attempt.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		// Processed is absorbing: the predicate refuses the transition rather than demoting a
		// finalized entry to Failed, which would make it re-admittable and run the handler again.
		// Also clears any lease term: a failed entry has no holder, so it must not keep a term a
		// later lease comparison (CompleteAsync/FailAsync) could match.
		var sql = $"""
		           UPDATE {Table}
		           SET Status = :FailedStatus, LastError = :LastError, RetryCount = :RetryCount, LastAttemptAt = :LastAttemptAt, LeaseExpiresAtUtc = NULL
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate} AND Status <> :ProcessedStatus
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				FailedStatus = (int)InboxStatus.Failed,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastError = NormalizeError(errorMessage),
				RetryCount = retryCount,
				LastAttemptAt = DateTimeOffset.UtcNow,
				MessageId = messageId,
				HandlerType = handlerType,
				TenantId = TenantTerm
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		// Deliberately does NOT clear LeaseExpiresAtUtc, unlike the two MarkFailedAsync overloads above.
		// This method belongs to IBackoffSchedulableInboxStore, a separate capability from the leased-claim
		// protocol (ILeasedInboxStore) — it schedules a NextAttemptAt for the non-lease claim/processing-
		// tracking paths, which never populate the lease column in the first place. Left untouched to keep
		// this store consistent with SqlServerInboxStore.MarkFailedWithBackoffAsync, which makes the same
		// scope call for the same reason.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = :TenantId" : string.Empty;
		// Processed is absorbing, as on the two MarkFailedAsync overloads above: the predicate refuses the
		// transition rather than demoting a finalized entry to Failed, which would re-admit it for retry
		// and run the handler again on side effects already committed.
		var sql = $"""
		           UPDATE {Table}
		           SET Status = :FailedStatus, LastError = :LastError, RetryCount = :RetryCount,
		           	LastAttemptAt = :LastAttemptAt, NextAttemptAt = :NextAttemptAt
		           WHERE MessageId = :MessageId AND HandlerType = :HandlerType{tenantPredicate} AND Status <> :ProcessedStatus
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				FailedStatus = (int)InboxStatus.Failed,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastError = NormalizeError(errorMessage),
				RetryCount = retryCount,
				LastAttemptAt = DateTimeOffset.UtcNow,
				NextAttemptAt = nextAttemptAt,
				MessageId = messageId,
				HandlerType = handlerType,
				TenantId = TenantTerm
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogWarning(
			"Marked inbox entry as failed with backoff for message {MessageId} and handler {HandlerType} (next attempt at {NextAttemptAt:O}): {Error}",
			messageId, handlerType, nextAttemptAt, errorMessage);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantSelectColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var sql = $"""
		           SELECT MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	Status, LastError, RetryCount, LastAttemptAt, NextAttemptAt, CorrelationId, Source{tenantSelectColumn}
		           FROM {Table}
		           WHERE (Status = :FailedStatus
		           		OR (Status = :ProcessingStatus AND LeaseExpiresAtUtc IS NOT NULL
		           			AND LeaseExpiresAtUtc < SYS_EXTRACT_UTC(SYSTIMESTAMP)))
		           	AND RetryCount < :MaxRetries
		           	AND (:OlderThan IS NULL OR LastAttemptAt < :OlderThanFilter)
		           	AND (NextAttemptAt IS NULL OR NextAttemptAt <= :Now)
		           ORDER BY RetryCount ASC, LastAttemptAt ASC
		           FETCH FIRST :BatchSize ROWS ONLY
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				FailedStatus = (int)InboxStatus.Failed,
				ProcessingStatus = (int)InboxStatus.Processing,
				MaxRetries = maxRetries,
				OlderThan = olderThan,
				OlderThanFilter = olderThan,
				Now = DateTimeOffset.UtcNow,
				BatchSize = batchSize
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return rows.Select(MapRowToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken)
	{
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantSelectColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var sql = $"""
		           SELECT MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	   Status, LastError, RetryCount, LastAttemptAt, NextAttemptAt, CorrelationId, Source{tenantSelectColumn}
		           FROM {Table}
		           ORDER BY ReceivedAt DESC
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return rows.Select(MapRowToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		var sql = $"""
		           SELECT
		           	COUNT(*) AS TotalEntries,
		           	SUM(CASE WHEN Status = :ProcessedStatus THEN 1 ELSE 0 END) AS ProcessedEntries,
		           	SUM(CASE WHEN Status = :FailedStatus THEN 1 ELSE 0 END) AS FailedEntries,
		           	SUM(CASE WHEN Status = :ReceivedStatus OR Status = :ProcessingStatus THEN 1 ELSE 0 END) AS PendingEntries
		           FROM {Table}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				ProcessedStatus = (int)InboxStatus.Processed,
				FailedStatus = (int)InboxStatus.Failed,
				ReceivedStatus = (int)InboxStatus.Received,
				ProcessingStatus = (int)InboxStatus.Processing
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await connection.QuerySingleAsync<InboxStatistics>(command).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		var sql = $"""
		           DELETE FROM {Table}
		           WHERE Status = :ProcessedStatus AND ProcessedAt < :CutoffDate
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { ProcessedStatus = (int)InboxStatus.Processed, CutoffDate = olderThan },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var deleted = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogInformation("Cleaned up {Count} processed inbox entries older than {CutoffDate}", deleted, olderThan);

		return deleted;
	}

	// Ensures a placeholder row exists for (messageId, handlerType) so the subsequent FOR UPDATE has a row to
	// lock. ORA-00001 means a concurrent caller already inserted it — benign.
	private async Task EnsurePlaceholderRowAsync(
		OracleConnection connection,
		IDbTransaction transaction,
		string messageId,
		string handlerType,
		CancellationToken cancellationToken)
	{
		// When the physical schema carries the tenant column (multi-tenant), the ambient tenant joins the
		// composite key so the placeholder row (and the FOR UPDATE lock it enables) is per-tenant — two tenants
		// sharing a message id serialize independently.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var insertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", :TenantId" : string.Empty;
		var sql = $"""
		           INSERT INTO {Table}
		           	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount{insertTenantCol})
		           VALUES
		           	(:MessageId, :HandlerType, NULL, EMPTY_BLOB(), :EmptyJson, :ReceivedAt, NULL, :ProcessingStatus, 0{insertTenantVal})
		           """;

		try
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(
				sql,
				new { MessageId = messageId, HandlerType = handlerType, ReceivedAt = DateTimeOffset.UtcNow, ProcessingStatus = (int)InboxStatus.Processing, EmptyJson = "{}", TenantId = TenantTerm },
				transaction: transaction,
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == OracleUniqueViolation)
		{
			// Row already exists — the FOR UPDATE below will lock it.
		}
	}

	private static Func<OracleConnection> CreateConnectionFactory(OracleInboxOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return () => new OracleConnection(options.ConnectionString);
	}

	// Oracle folds an empty string to NULL; a genuinely-empty error message is therefore stored as NULL,
	// which round-trips as null (matching "no error"). Non-empty messages are unaffected.
	private static string? NormalizeError(string errorMessage) =>
		string.IsNullOrEmpty(errorMessage) ? null : errorMessage;

#pragma warning disable IL2026, IL3050 // JSON serialization for inbox metadata
	private string SerializeMetadata(IDictionary<string, object> metadata)
	{
		return JsonSerializer.Serialize(metadata, _jsonOptions);
	}
#pragma warning restore IL2026, IL3050

	private static InboxEntry MapRowToEntry(InboxEntryRow row)
	{
		return new InboxEntry
		{
			MessageId = row.MessageId,
			HandlerType = row.HandlerType,
			MessageType = row.MessageType ?? string.Empty,
			Payload = row.Payload ?? [],
			ReceivedAt = row.ReceivedAt,
			ProcessedAt = row.ProcessedAt,
			Status = (InboxStatus)row.Status,
			LastError = row.LastError,
			RetryCount = row.RetryCount,
			LastAttemptAt = row.LastAttemptAt,
			NextAttemptAt = row.NextAttemptAt,
			CorrelationId = row.CorrelationId,
			TenantId = row.TenantId,
			Source = row.Source
		};
	}

	private sealed class InboxEntryRow
	{
		public string MessageId { get; set; } = string.Empty;
		public string HandlerType { get; set; } = string.Empty;
		public string? MessageType { get; set; }
		public byte[]? Payload { get; set; }
		public string? Metadata { get; set; }
		public DateTimeOffset ReceivedAt { get; set; }
		public DateTimeOffset? ProcessedAt { get; set; }
		public int Status { get; set; }
		public string? LastError { get; set; }
		public int RetryCount { get; set; }
		public DateTimeOffset? LastAttemptAt { get; set; }
		public DateTimeOffset? NextAttemptAt { get; set; }
		public string? CorrelationId { get; set; }
		public string? TenantId { get; set; }
		public string? Source { get; set; }
	}
}
