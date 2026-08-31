// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Inbox.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Inbox.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IInboxStore"/> for idempotent message processing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides reliable message deduplication and processing tracking using Postgres.
/// Messages are keyed by a composite of (MessageId, HandlerType), allowing the same message to be
/// processed independently by multiple handlers.
/// </para>
/// <para>
/// The <see cref="TryMarkAsProcessedAsync"/> method provides atomic "first writer wins" semantics
/// using Postgres's INSERT ... ON CONFLICT DO NOTHING for proper isolation.
/// </para>
/// </remarks>
public sealed class PostgresInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, ILeasedInboxStore, IInboxStoreAdmin, ITransactionalInboxStore, IScopedTransactionalInboxStore, IInboxStoreCapabilities, IInboxSchemaValidator
{
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
	/// <remarks>This store records no per-entry next-attempt time.</remarks>
	public bool SupportsBackoffScheduling => false;

	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly PostgresInboxOptions _options;
	private readonly ILogger<PostgresInboxStore> _logger;
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
	/// A tenant term that has already been resolved. Holding it in a distinct type is what makes the
	/// fail-closed check structural rather than positional: every tenant-facing connection open and schema
	/// read takes one, so the term is resolved before any SQL exists on that path by construction, not by
	/// where a statement happens to sit in the method body.
	/// </summary>
	private readonly struct ResolvedTenantTerm(string term)
	{
		/// <summary>Gets the resolved term bound to every keyed statement on this path.</summary>
		public string Term { get; } = term;
	}

	/// <summary>
	/// Resolves the tenant term bound to every inbox row, for both the write and the match, refusing a
	/// multi-tenant deployment whose ambient tenant is unresolved.
	/// </summary>
	/// <remarks>
	/// Always a concrete, non-null term: an unscoped host resolves to the reserved untenanted sentinel
	/// rather than to <see langword="null"/>. Binding the scope's nullable identifier directly was the
	/// defect — an untenanted host wrote NULL into the tenant column while every lookup used a bare
	/// equality, and NULL never equals NULL in SQL. The row was written and then unreachable, so the
	/// entry stayed unprocessed forever and neither side raised an error.
	/// <para>
	/// Routing every site through one method is what makes that inexpressible: there is no per-call-site
	/// opportunity left to bind the nullable value by hand.
	/// </para>
	/// </remarks>
	private ResolvedTenantTerm ResolveTenantTerm() => new(CurrentTenantPartition.TenantId);

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresInboxStore"/> class.
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
	public PostgresInboxStore(
		IOptions<PostgresInboxOptions> options,
		ILogger<PostgresInboxStore> logger,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
		: this(CreateConnectionFactory(options?.Value!), options!.Value, logger, tenantContext, tenantContextOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresInboxStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="NpgsqlConnection"/> instances.</param>
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
	public PostgresInboxStore(
		Func<NpgsqlConnection> connectionFactory,
		PostgresInboxOptions options,
		ILogger<PostgresInboxStore> logger,
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
		// Canonical event-serialization contract (camelCase + enum-as-string + omit-null), shared with every
		// store and the default serializer, so persisted inbox metadata round-trips byte-for-byte.
		_jsonOptions = EventSerializationDefaults.Canonical;
	}

	private readonly bool _requireTenant;

	// Whether the physical unique key includes the tenant_id column. Read once from the live schema and
	// cached; SQL emission follows THIS (the actual column), never the mode flag.
	private volatile bool _schemaContractVerified;
	private bool _hasTenantColumn;

	// The UNTENANTED opener. Named to make its use a deliberate declaration of cross-tenant intent: only the
	// estate-wide admin drains and the schema read call it directly. Tenant-facing callers cannot reach it
	// except through the overload below, which demands a term that has already been resolved.
	private async ValueTask<NpgsqlConnection> OpenUntenantedConnectionAsync(CancellationToken cancellationToken)
	{
		var connection = _connectionFactory();
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

	/// <summary>
	/// Opens a connection for a TENANT-FACING statement. The resolved term is a required argument, so it
	/// cannot be evaluated after the connection exists: a caller that has not resolved a tenant has nothing
	/// to pass and does not compile. This is what replaces the hand-placed ordering the guard used to rely on.
	/// </summary>
	private ValueTask<NpgsqlConnection> OpenConnectionAsync(ResolvedTenantTerm tenant, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenant.Term);
		return OpenUntenantedConnectionAsync(cancellationToken);
	}

	/// <summary>
	/// Reads the schema for a TENANT-FACING statement. Overload of the untenanted read that demands the
	/// resolved term, because that read opens a connection of its own and so is also past the point of no
	/// return for an unresolved tenant.
	/// </summary>
	private ValueTask<bool> EnsureSchemaAsync(ResolvedTenantTerm tenant, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenant.Term);
		return EnsureSchemaAsync(cancellationToken);
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

		await using var connection = await OpenUntenantedConnectionAsync(cancellationToken).ConfigureAwait(false);

		var primaryKeyColumns = (await connection.QueryAsync<string>(
			new CommandDefinition(
				"""
				SELECT kcu.column_name
				FROM information_schema.table_constraints tc
				JOIN information_schema.key_column_usage kcu
				  ON kcu.constraint_name = tc.constraint_name
				 AND kcu.constraint_schema = tc.constraint_schema
				WHERE tc.constraint_type = 'PRIMARY KEY'
				  AND tc.table_schema = @Schema AND tc.table_name = @Table
				ORDER BY kcu.ordinal_position
				""",
				new { Schema = _options.SchemaName, Table = _options.TableName },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

		var tenantIdIsNullable = await connection.ExecuteScalarAsync<bool?>(
			new CommandDefinition(
				"""
				SELECT (is_nullable = 'YES')
				FROM information_schema.columns
				WHERE table_schema = @Schema AND table_name = @Table AND column_name = 'tenant_id'
				""",
				new { Schema = _options.SchemaName, Table = _options.TableName },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		_hasTenantColumn = InboxSchemaContract.Verify(
			_options.QualifiedTableName, _requireTenant, primaryKeyColumns, tenantIdIsNullable,
			"message_id", "handler_type", "tenant_id");
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
		// decides the shape: CurrentTenantScope => None — a non-multi-tenant deployment has no tenant_id column,
		// so the column and parameter are omitted entirely (zero bloat). A registered ITenantContext =>
		// Scoped — the tenant_id column is written and the tenant term rides every keyed path. A context that
		// resolves no tenant fails closed (throws) rather than reaching a predicate-less query.
		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantColumn = hasTenantColumn ? ", tenant_id" : string.Empty;
		var tenantValue = hasTenantColumn ? ", @TenantId" : string.Empty;

		var sql = $"""
		           INSERT INTO {_options.QualifiedTableName}
		           	(message_id, handler_type, message_type, payload, metadata, received_at, status, retry_count, correlation_id, source{tenantColumn})
		           VALUES
		           	(@MessageId, @HandlerType, @MessageType, @Payload, @Metadata::jsonb, @ReceivedAt, @Status, @RetryCount, @CorrelationId, @Source{tenantValue})
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

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
				TenantId = tenant.Term,
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
		catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
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

		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = @ProcessedStatus, processed_at = @ProcessedAt, last_attempt_at = @ProcessedAt, last_error = NULL
		           WHERE message_id = @MessageId AND handler_type = @HandlerType AND status != @ProcessedStatus{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessedStatus = (int)InboxStatus.Processed,
				ProcessedAt = DateTimeOffset.UtcNow,
				TenantId = tenant.Term
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

		// One connection + one LOCAL transaction (no distributed coordinator): the claim, the handler's writes,
		// and the processed-mark all run on THIS connection/transaction, so they commit or roll back atomically
		// — an exactly-once STATE TRANSITION on success (handler writes + processed-mark commit as one unit).
		// This is NOT exactly-once DELIVERY: on any throw everything rolls back, nothing is marked, and the
		// message redelivers for retry; a later duplicate is caught by the processed-check, so delivery is
		// at-least-once and idempotent dedup makes processing effectively-once. Handler writes are atomic with
		// the mark ONLY when the handler routes them through the passed transaction (its Dapper calls pass
		// transaction: tx).
		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();

		// Composite tenant key on the claim/dedup path, by deployment mode. Multi-tenant (an ITenantContext is
		// registered) → the resolved tenant joins the read predicate + the synthesized INSERT + the ON CONFLICT
		// target (the triple), so two tenants sharing a message id never dedup against each other. Non-MT →
		// CurrentTenantScope=None: no tenant column/param/predicate and the ON CONFLICT target degrades to the
		// pair (message_id, handler_type). An MT-active-but-unresolved context fails closed.
		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		var insertTenantCol = hasTenantColumn ? ", tenant_id" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", @TenantId" : string.Empty;
		var conflictTarget = hasTenantColumn ? "(message_id, handler_type, tenant_id)" : "(message_id, handler_type)";

		try
		{
			var now = DateTimeOffset.UtcNow;

			// Ensure a row exists (atomic first-writer claim into Processing) so the FOR UPDATE below has a row
			// to lock — the serialization primitive for a not-yet-present key.
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$$"""
				  INSERT INTO {{_options.QualifiedTableName}}
				  	(message_id, handler_type, message_type, payload, metadata, received_at, processed_at, status, retry_count{{insertTenantCol}})
				  VALUES
				  	(@MessageId, @HandlerType, '', ''::bytea, '{}'::jsonb, @Now, NULL, @ProcessingStatus, 0{{insertTenantVal}})
				  ON CONFLICT {{conflictTarget}} DO NOTHING
				  """,
				new { MessageId = messageId, HandlerType = handlerType, ProcessingStatus = (int)InboxStatus.Processing, Now = now, TenantId = tenant.Term },
				transaction: transaction,
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

			// Lock the row + read its status; FOR UPDATE serializes concurrent processors of this key — a second
			// caller blocks here until this transaction commits/rolls back.
			var existingStatus = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
				$"SELECT status FROM {_options.QualifiedTableName} WHERE message_id = @MessageId AND handler_type = @HandlerType{tenantPredicate} FOR UPDATE",
				new { MessageId = messageId, HandlerType = handlerType, TenantId = tenant.Term },
				transaction: transaction,
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

			if (existingStatus == (int)InboxStatus.Processed)
			{
				// Already processed by a prior committed transaction — a duplicate. Handler is NOT invoked.
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogDebug("Duplicate — inbox message {MessageId}/{HandlerType} already processed; handler skipped", messageId, handlerType);
				return false;
			}

			// Run the handler INSIDE this transaction; its writes enlist by passing transaction: tx.
			await handler(transaction, cancellationToken).ConfigureAwait(false);

			// Mark processed on the SAME transaction — atomic with the handler's writes.
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$"UPDATE {_options.QualifiedTableName} SET status = @ProcessedStatus, processed_at = @Now, last_attempt_at = @Now, last_error = NULL WHERE message_id = @MessageId AND handler_type = @HandlerType{tenantPredicate}",
				new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed, Now = now, TenantId = tenant.Term },
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
	/// Wires the inbox middleware's scoped exactly-once-state-transition seam onto the relational implementation
	/// above: the active BCL <see cref="IDbTransaction"/> is wrapped in the opaque
	/// <see cref="IInboxTransactionScope"/> so a consumer handler enlists its own writes via
	/// <c>scope.AsSqlTransaction()</c>, committing atomically with the processed-mark. The same guarantees are
	/// inherited unchanged: an exactly-once state transition (handler writes and processed-mark commit as one)
	/// over at-least-once delivery with idempotent deduplication, and tenant scoping.
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

		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		// Processed is absorbing: refuse rather than demote a finalized entry back to Processing, which
		// would re-admit the message and run the handler again. The guard sits in SET rather than in
		// WHERE (where MarkFailedAsync carries it) because this method reports a missing row by throwing
		// off the affected count, and a WHERE-side guard would make a refused Processed entry
		// indistinguishable from an absent one. Guarding the assignment leaves affected = 0 meaning
		// exactly "no such row" while the transition is still evaluated atomically under the row lock.
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = CASE WHEN status = @ProcessedStatus THEN status ELSE @ProcessingStatus END,
		               last_attempt_at = CASE WHEN status = @ProcessedStatus THEN last_attempt_at ELSE @LastAttemptAt END
		           WHERE message_id = @MessageId AND handler_type = @HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessingStatus = (int)InboxStatus.Processing,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastAttemptAt = DateTimeOffset.UtcNow,
				TenantId = tenant.Term
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

		// Atomic "first writer wins" using INSERT ... ON CONFLICT DO NOTHING
		// Returns true if row was inserted (first processor), false if conflict (duplicate)
		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var insertTenantCol = hasTenantColumn ? ", tenant_id" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", @TenantId" : string.Empty;
		var conflictTarget = hasTenantColumn ? "(message_id, handler_type, tenant_id)" : "(message_id, handler_type)";
		var sql = $$"""
		            INSERT INTO {{_options.QualifiedTableName}}
		            	(message_id, handler_type, message_type, payload, metadata, received_at, processed_at, status, retry_count{{insertTenantCol}})
		            VALUES
		            	(@MessageId, @HandlerType, '', ''::bytea, '{}'::jsonb, @Now, @Now, @ProcessedStatus, 0{{insertTenantVal}})
		            ON CONFLICT {{conflictTarget}} DO NOTHING
		            """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				Now = DateTimeOffset.UtcNow,
				ProcessedStatus = (int)InboxStatus.Processed,
				TenantId = tenant.Term
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
		var isFirstProcessor = rowsAffected > 0;

		if (isFirstProcessor)
		{
			_logger.LogDebug("First processor for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Duplicate detected for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return isFirstProcessor;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// Atomic "first writer wins" claim into the NON-TERMINAL Processing state using
		// INSERT ... ON CONFLICT DO NOTHING. Returns true if the row was inserted (claim acquired),
		// false on conflict (already claimed/processed). Finalized via MarkProcessedAsync, removed via ReleaseAsync.
		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var insertTenantCol = hasTenantColumn ? ", tenant_id" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", @TenantId" : string.Empty;
		var conflictTarget = hasTenantColumn ? "(message_id, handler_type, tenant_id)" : "(message_id, handler_type)";
		var sql = $$"""
		            INSERT INTO {{_options.QualifiedTableName}}
		            	(message_id, handler_type, message_type, payload, metadata, received_at, processed_at, status, retry_count{{insertTenantCol}})
		            VALUES
		            	(@MessageId, @HandlerType, '', ''::bytea, '{}'::jsonb, @Now, NULL, @ProcessingStatus, 0{{insertTenantVal}})
		            ON CONFLICT {{conflictTarget}} DO NOTHING
		            """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				Now = DateTimeOffset.UtcNow,
				ProcessingStatus = (int)InboxStatus.Processing,
				TenantId = tenant.Term
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
		var claimed = rowsAffected > 0;

		if (claimed)
		{
			_logger.LogDebug("Claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Claim denied (already claimed/processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return claimed;
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

		// Single atomic lease CAS. Insert a fresh Processing claim, or — on conflict — reclaim IFF the
		// existing row is Received, Failed (a handler-failed entry is re-admittable so a redelivery retries
		// — matching the non-lease fallback path and the interface's release-on-failure contract), or a
		// Processing entry whose lease has expired. A live Processing lease or a terminal Processed row
		// fails the DO UPDATE WHERE -> no row updated -> RETURNING yields nothing -> false. The lease-expiry
		// comparison uses the SERVER clock (now()) inside the statement so competing app instances never
		// decide expiry with a skewed local clock.
		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var insertTenantCol = hasTenantColumn ? ", tenant_id" : string.Empty;
		var insertTenantVal = hasTenantColumn ? ", @TenantId" : string.Empty;
		var conflictTarget = hasTenantColumn ? "(message_id, handler_type, tenant_id)" : "(message_id, handler_type)";
		var sql = $$"""
		            INSERT INTO {{_options.QualifiedTableName}}
		            	(message_id, handler_type, message_type, payload, metadata, received_at, processed_at, status, retry_count, lease_expires_at{{insertTenantCol}})
		            VALUES
		            	(@MessageId, @HandlerType, '', ''::bytea, '{}'::jsonb, now(), NULL, @ProcessingStatus, 0, now() + @Lease{{insertTenantVal}})
		            ON CONFLICT {{conflictTarget}} DO UPDATE
		            	SET status = @ProcessingStatus,
		            		lease_expires_at = now() + @Lease,
		            		last_attempt_at = now()
		            	WHERE {{_options.QualifiedTableName}}.status = @ReceivedStatus
		            		OR {{_options.QualifiedTableName}}.status = @FailedStatus
		            		OR ({{_options.QualifiedTableName}}.status = @ProcessingStatus
		            			AND {{_options.QualifiedTableName}}.lease_expires_at < now())
		            RETURNING lease_expires_at
		            """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				Lease = leaseDuration,
				ProcessingStatus = (int)InboxStatus.Processing,
				ReceivedStatus = (int)InboxStatus.Received,
				FailedStatus = (int)InboxStatus.Failed,
				TenantId = tenant.Term
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		// The term is the expiry the SERVER resolved inside this statement (now() + @Lease), read back via
		// RETURNING — never recomputed app-side, so it is byte-identical to what CompleteAsync/FailAsync must
		// later match.
		var leaseExpiresAt = await connection.QuerySingleOrDefaultAsync<DateTimeOffset?>(command).ConfigureAwait(false);
		var lease = leaseExpiresAt is { } expiresAt ? ToLeaseToken(expiresAt) : (LeaseToken?)null;

		if (lease is not null)
		{
			_logger.LogDebug("Lease-claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return lease;
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

		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		// The term is ADDITIONAL to the tenant/existence predicates, never a replacement: a caller whose
		// lease lapsed presents the term it lost, which matches no row (the row's term has already moved
		// on to whoever reclaimed it), so this UPDATE affects 0 rows and CompleteAsync reports false.
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = @ProcessedStatus, processed_at = @ProcessedAt, last_attempt_at = @ProcessedAt, last_error = NULL
		           WHERE message_id = @MessageId AND handler_type = @HandlerType AND lease_expires_at = @Lease{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessedStatus = (int)InboxStatus.Processed,
				ProcessedAt = DateTimeOffset.UtcNow,
				Lease = FromLeaseToken(lease),
				TenantId = tenant.Term
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

		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		// Clears the term on success: a failed entry has no holder, so it must not keep a term a later
		// comparison could match. Processed is absorbing: the predicate refuses the transition rather than
		// demoting a finalized entry to Failed, which would make it re-admittable and run the handler again.
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = @FailedStatus, last_error = @LastError, retry_count = retry_count + 1, last_attempt_at = @LastAttemptAt, lease_expires_at = NULL
		           WHERE message_id = @MessageId AND handler_type = @HandlerType AND lease_expires_at = @Lease{tenantPredicate} AND status <> @ProcessedStatus
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				FailedStatus = (int)InboxStatus.Failed,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastError = errorMessage,
				LastAttemptAt = DateTimeOffset.UtcNow,
				Lease = FromLeaseToken(lease),
				TenantId = tenant.Term
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (affected > 0)
		{
			_logger.LogDebug("Recorded failure for leased inbox entry {MessageId}/{HandlerType}: {Error}", messageId, handlerType, errorMessage);
		}
		else
		{
			_logger.LogDebug("Lease-fail denied (stale term) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return affected > 0;
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// Remove the non-terminal claim so a redelivery can re-admit. Restricted to non-Processed rows so a
		// concurrently-finalized entry is never deleted. No-op if already removed or never claimed.
		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE message_id = @MessageId AND handler_type = @HandlerType AND status <> @ProcessedStatus{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed, TenantId = tenant.Term },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		_logger.LogDebug("Released inbox claim for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		var sql = $"""
		           SELECT EXISTS (
		           	SELECT 1 FROM {_options.QualifiedTableName}
		           	WHERE message_id = @MessageId AND handler_type = @HandlerType AND status = @ProcessedStatus{tenantPredicate}
		           )
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed, TenantId = tenant.Term },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await connection.QuerySingleAsync<bool>(command).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		var tenantSelectColumn = hasTenantColumn ? ", tenant_id" : string.Empty;
		var sql = $"""
		           SELECT message_id, handler_type, message_type, payload, metadata, received_at, processed_at,
		           	   status, last_error, retry_count, last_attempt_at, correlation_id, source{tenantSelectColumn}
		           FROM {_options.QualifiedTableName}
		           WHERE message_id = @MessageId AND handler_type = @HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, TenantId = tenant.Term },
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

		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		// Processed is absorbing: the predicate refuses the transition rather than demoting a
		// finalized entry to Failed, which would make it re-admittable and run the handler again.
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = @FailedStatus, last_error = @LastError, retry_count = retry_count + 1, last_attempt_at = @LastAttemptAt, lease_expires_at = NULL
		           WHERE message_id = @MessageId AND handler_type = @HandlerType{tenantPredicate} AND status <> @ProcessedStatus
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				FailedStatus = (int)InboxStatus.Failed,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastError = errorMessage,
				LastAttemptAt = DateTimeOffset.UtcNow,
				TenantId = tenant.Term
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

		// Set retry_count EXACTLY (no +1) so a transient short-circuit leaves the entry re-admittable
		// without consuming a delivery attempt. UPDATE-only: same existence semantics as the
		// incrementing overload (a missing row affects 0 rows).
		// FAIL-CLOSED BEFORE ANY CONNECTION, structurally. The resolved term is a REQUIRED ARGUMENT of
		// every tenant-facing connection open and schema read below, so it is evaluated — and refuses an
		// unresolved ambient tenant — before a connection can exist, whatever order the statements are in.
		// Moving or deleting this line does not compile. The drain methods take the untenanted overloads:
		// they are cross-tenant by contract.
		var tenant = ResolveTenantTerm();
		var hasTenantColumn = await EnsureSchemaAsync(tenant, cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND tenant_id = @TenantId" : string.Empty;
		// Processed is absorbing: the predicate refuses the transition rather than demoting a
		// finalized entry to Failed, which would make it re-admittable and run the handler again.
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET status = @FailedStatus, last_error = @LastError, retry_count = @RetryCount, last_attempt_at = @LastAttemptAt, lease_expires_at = NULL
		           WHERE message_id = @MessageId AND handler_type = @HandlerType{tenantPredicate} AND status <> @ProcessedStatus
		           """;

		await using var connection = await OpenConnectionAsync(tenant, cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				FailedStatus = (int)InboxStatus.Failed,
				ProcessedStatus = (int)InboxStatus.Processed,
				LastError = errorMessage,
				RetryCount = retryCount,
				LastAttemptAt = DateTimeOffset.UtcNow,
				TenantId = tenant.Term
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantSelectColumn = hasTenantColumn ? ", tenant_id" : string.Empty;
		var sql = $"""
		           SELECT message_id, handler_type, message_type, payload, metadata, received_at, processed_at,
		           	   status, last_error, retry_count, last_attempt_at, correlation_id, source{tenantSelectColumn}
		           FROM {_options.QualifiedTableName}
		           WHERE (status = @FailedStatus
		           		OR (status = @ProcessingStatus AND lease_expires_at IS NOT NULL AND lease_expires_at < NOW()))
		           	AND retry_count < @MaxRetries
		           	AND (@OlderThan IS NULL OR last_attempt_at < @OlderThan)
		           ORDER BY retry_count ASC, last_attempt_at ASC
		           LIMIT @BatchSize
		           """;

		await using var connection = await OpenUntenantedConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { BatchSize = batchSize, FailedStatus = (int)InboxStatus.Failed, ProcessingStatus = (int)InboxStatus.Processing, MaxRetries = maxRetries, OlderThan = olderThan },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return rows.Select(MapRowToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken)
	{
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantSelectColumn = hasTenantColumn ? ", tenant_id" : string.Empty;
		var sql = $"""
		           SELECT message_id, handler_type, message_type, payload, metadata, received_at, processed_at,
		           	   status, last_error, retry_count, last_attempt_at, correlation_id, source{tenantSelectColumn}
		           FROM {_options.QualifiedTableName}
		           ORDER BY received_at DESC
		           """;

		await using var connection = await OpenUntenantedConnectionAsync(cancellationToken).ConfigureAwait(false);

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
		           	COUNT(*) AS "TotalEntries",
		           	SUM(CASE WHEN status = @ProcessedStatus THEN 1 ELSE 0 END) AS "ProcessedEntries",
		           	SUM(CASE WHEN status = @FailedStatus THEN 1 ELSE 0 END) AS "FailedEntries",
		           	SUM(CASE WHEN status = @ReceivedStatus OR status = @ProcessingStatus THEN 1 ELSE 0 END) AS "PendingEntries"
		           FROM {_options.QualifiedTableName}
		           """;

		await using var connection = await OpenUntenantedConnectionAsync(cancellationToken).ConfigureAwait(false);

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
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE status = @ProcessedStatus AND processed_at < @CutoffDate
		           """;

		await using var connection = await OpenUntenantedConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { ProcessedStatus = (int)InboxStatus.Processed, CutoffDate = olderThan },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var deleted = await connection.ExecuteAsync(command).ConfigureAwait(false);
		_logger.LogInformation("Cleaned up {Count} processed inbox entries older than {CutoffDate}", deleted, olderThan);

		return deleted;
	}

	private static Func<NpgsqlConnection> CreateConnectionFactory(PostgresInboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return () => new NpgsqlConnection(options.ConnectionString);
	}

	#region Private Methods

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Serializing IDictionary<string, object> with simple JSON types is trim-safe.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Serializing IDictionary<string, object> with simple JSON types does not require dynamic code.")]
	private string SerializeMetadata(IDictionary<string, object> metadata)
	{
		return JsonSerializer.Serialize(metadata, _jsonOptions);
	}

	// Round-trip the lease term through the exact "O" (round-trip) format. lease_expires_at is TIMESTAMPTZ,
	// which Postgres stores at MICROSECOND resolution (6 fractional digits); Npgsql populates the resulting
	// DateTimeOffset's Ticks as an exact multiple of 10, so the 7th ("O"-format) fractional digit is always
	// 0 and round-trips losslessly through ParseExact back to the identical DateTimeOffset — the value
	// re-sent to Postgres on CompleteAsync/FailAsync is byte-identical to the one the server wrote.
	private static LeaseToken ToLeaseToken(DateTimeOffset leaseExpiresAt)
		=> new(leaseExpiresAt.ToString("O", CultureInfo.InvariantCulture));

	private static DateTimeOffset FromLeaseToken(LeaseToken lease)
		=> DateTimeOffset.ParseExact(lease.Value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

	private InboxEntry MapRowToEntry(InboxEntryRow row)
	{
		return new InboxEntry
		{
			MessageId = row.MessageId,
			HandlerType = row.HandlerType,
			MessageType = row.MessageType,
			Payload = row.Payload,
			ReceivedAt = row.ReceivedAt,
			ProcessedAt = row.ProcessedAt,
			Status = (InboxStatus)row.Status,
			LastError = row.LastError,
			RetryCount = row.RetryCount,
			LastAttemptAt = row.LastAttemptAt,
			CorrelationId = row.CorrelationId,
			TenantId = row.TenantId,
			Source = row.Source
		};
	}

	#endregion Private Methods

	#region Row Types

	private sealed class InboxEntryRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public string message_id { get; set; } = string.Empty;

		public string handler_type { get; set; } = string.Empty;
		public string message_type { get; set; } = string.Empty;
		public byte[] payload { get; set; } = [];
		public string? metadata { get; set; }
		public DateTimeOffset received_at { get; set; }
		public DateTimeOffset? processed_at { get; set; }
		public int status { get; set; }
		public string? last_error { get; set; }
		public int retry_count { get; set; }
		public DateTimeOffset? last_attempt_at { get; set; }
		public string? correlation_id { get; set; }
		public string? tenant_id { get; set; }
		public string? source { get; set; }
		// ReSharper restore InconsistentNaming

		// Map snake_case columns to PascalCase properties for use in code
		public string MessageId => message_id;

		public string HandlerType => handler_type;
		public string MessageType => message_type;
		public byte[] Payload => payload;
		public string? Metadata => metadata;
		public DateTimeOffset ReceivedAt => received_at;
		public DateTimeOffset? ProcessedAt => processed_at;
		public int Status => status;
		public string? LastError => last_error;
		public int RetryCount => retry_count;
		public DateTimeOffset? LastAttemptAt => last_attempt_at;
		public string? CorrelationId => correlation_id;
		public string? TenantId => tenant_id;
		public string? Source => source;
	}

	#endregion Row Types
}
