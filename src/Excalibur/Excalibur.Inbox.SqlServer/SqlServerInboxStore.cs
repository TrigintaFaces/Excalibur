// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Inbox.Observability;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IInboxStore"/> for idempotent message processing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides reliable message deduplication and processing tracking using SQL Server.
/// Messages are keyed by a composite of (MessageId, HandlerType), allowing the same message to be
/// processed independently by multiple handlers.
/// </para>
/// <para>
/// The <see cref="TryMarkAsProcessedAsync"/> method provides atomic "first writer wins" semantics
/// using SQL Server's MERGE statement with HOLDLOCK hint for proper isolation.
/// </para>
/// </remarks>
public sealed class SqlServerInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IInboxStoreAdmin, IBackoffSchedulableInboxStore, ITransactionalInboxStore, IScopedTransactionalInboxStore, IInboxStoreCapabilities, IInboxSchemaValidator
{
	/// <inheritdoc/>
	public async ValueTask ValidateSchemaAsync(CancellationToken cancellationToken)
		=> await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	public bool SupportsClaim => true;

	/// <inheritdoc/>
	public bool SupportsProcessingTracking => true;

	/// <inheritdoc/>
	public bool SupportsTransactional => true;

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly SqlServerInboxOptions _options;
	private readonly ILogger<SqlServerInboxStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Gets the tenant term bound to every inbox row, for both the write and the match.
	/// </summary>
	/// <remarks>
	/// Always a concrete, non-null term: an unscoped host resolves to the reserved untenanted sentinel
	/// rather than to <see langword="null"/>. Binding <c>TenantScope.TenantId</c> directly was the defect —
	/// an untenanted host wrote NULL into the tenant column and every later lookup used a bare
	/// <c>TenantId = @TenantId</c>, and <c>NULL = NULL</c> is never true in SQL. The row was written and
	/// then unreachable, so the entry stayed unprocessed forever and no error was raised on either side.
	/// <para>
	/// Routing every site through one property is what makes that inexpressible: there is no longer a
	/// per-call-site opportunity to bind the nullable scope value by hand.
	/// </para>
	/// </remarks>
	private string TenantTerm =>
		KeyedTenantPartition.FromScope(TenantScope.FromContext(_tenantContext)).TenantId;
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// Ambient tenant context. The write/dedup/claim paths and keyed reads scope on the composite key
	/// (<c>TenantId</c>, MessageId, HandlerType) so two tenants carrying the same message id never dedup
	/// against each other. Tenant-facing paths fail closed: if the resolved tenant is null they throw rather
	/// than silently running cross-tenant. The registration path composes the store with a non-null context
	/// (a single-tenant default or the ambient multi-tenant context), so a null resolved tenant means "ambient
	/// scope not established." The <c>IInboxStoreAdmin</c> operator surface (<c>GetAllEntries</c>,
	/// <c>GetFailedEntries</c>, <c>GetStatistics</c>, <c>Cleanup</c>) is estate-wide by design: it serves
	/// operators and background services (dashboards, retention, retry processors) and is not resolved on the
	/// per-tenant request path — a message handler depends on <c>IInboxStore</c>, which does not expose these
	/// methods, so a per-tenant caller cannot reach a cross-tenant read or delete.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode for the startup schema handshake.
	/// </param>
	public SqlServerInboxStore(
		IOptions<SqlServerInboxOptions> options,
		ILogger<SqlServerInboxStore> logger,
		ITenantContext? tenantContext = null,
		IOptions<TenantContextOptions>? tenantContextOptions = null)
		: this(CreateConnectionFactory(options!.Value), options.Value, logger, tenantContext, tenantContextOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerInboxStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// Optional ambient tenant context for row-level tenant scoping (see the options-based constructor's remarks).
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options; its <see cref="TenantContextOptions.RequireTenant"/> selects the deployment
	/// mode for the startup schema handshake.
	/// </param>
	public SqlServerInboxStore(
		Func<SqlConnection> connectionFactory,
		SqlServerInboxOptions options,
		ILogger<SqlServerInboxStore> logger,
		ITenantContext? tenantContext = null,
		IOptions<TenantContextOptions>? tenantContextOptions = null)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_options = options;
		_logger = logger;
		_tenantContext = tenantContext;
		// Deployment mode: multi-tenant iff the consumer opted in via AddMultiTenancy(), which sets
		// TenantContextOptions.RequireTenant. This is NOT "is an ITenantContext present" (the framework always
		// registers a single-tenant default). Drives the startup handshake's leak-check.
		_requireTenant = tenantContextOptions?.Value.RequireTenant ?? false;
		// Canonical event-serialization contract (camelCase + enum-as-string + omit-null), shared with every
		// store and the default serializer, so persisted inbox metadata round-trips byte-for-byte.
		_jsonOptions = EventSerializationDefaults.Canonical;
	}

	private readonly bool _requireTenant;

	// Whether the physical unique key includes the TenantId column. Read once from the live schema (the
	// handshake) and cached; SQL emission follows THIS (the actual column), never the mode flag — so the
	// store can never emit a term for an absent column, nor omit one that is present.
	private volatile bool _schemaContractVerified;
	private bool _hasTenantColumn;

	private async ValueTask<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
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

	// The (b) fail-closed floor + the emission driver: on first use, reads the physical schema, verifies the
	// deployment-mode ↔ schema contract (fail-fast on mismatch), caches whether the tenant column is in the
	// key, and returns it so the caller emits the tenant term iff the column exists. A concurrent first-use
	// may run it more than once, each converging on the same result, so no lock is needed. The (a)
	// hosted-service validator calls this at startup so host consumers fail before the first message.
	internal async ValueTask<bool> EnsureSchemaAsync(CancellationToken cancellationToken)
	{
		if (_schemaContractVerified)
		{
			return _hasTenantColumn;
		}

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var primaryKeyColumns = (await connection.QueryAsync<string>(
			new CommandDefinition(
				"""
				SELECT c.name
				FROM sys.indexes i
				JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
				JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
				WHERE i.object_id = OBJECT_ID(@TableName) AND i.is_primary_key = 1
				ORDER BY ic.key_ordinal
				""",
				new { TableName = _options.QualifiedTableName },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

		var tenantIdIsNullable = await connection.ExecuteScalarAsync<bool?>(
			new CommandDefinition(
				"SELECT c.is_nullable FROM sys.columns c WHERE c.object_id = OBJECT_ID(@TableName) AND c.name = N'TenantId'",
				new { TableName = _options.QualifiedTableName },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		_hasTenantColumn = InboxSchemaContract.Verify(
			_options.QualifiedTableName, _requireTenant, primaryKeyColumns, tenantIdIsNullable,
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
		// decides the shape: FromContext(null) => None — a non-multi-tenant deployment has no TenantId column,
		// so the column and parameter are omitted entirely (zero bloat). A registered ITenantContext =>
		// Scoped — the TenantId column is written and the tenant term rides every keyed path. A context that
		// resolves no tenant fails closed (throws) rather than reaching a predicate-less query.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var tenantValue = hasTenantColumn ? ", @TenantId" : string.Empty;

		var sql = $"""
		           INSERT INTO {_options.QualifiedTableName}
		           	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, Status, RetryCount, CorrelationId, Source{tenantColumn})
		           VALUES
		           	(@MessageId, @HandlerType, @MessageType, @Payload, @Metadata, @ReceivedAt, @Status, @RetryCount, @CorrelationId, @Source{tenantValue})
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
		catch (SqlException ex) when (ex.Number is 2627 or 2601) // Unique constraint violation
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
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET Status = @ProcessedStatus, ProcessedAt = @ProcessedAt, LastAttemptAt = @ProcessedAt, LastError = NULL
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType AND Status != @ProcessedStatus{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessedStatus = (int)InboxStatus.Processed,
				ProcessedAt = DateTimeOffset.UtcNow,
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

		// One connection + one LOCAL transaction (no MSDTC): the claim-check, the handler's writes, and the
		// processed-mark all run on THIS connection/transaction, so they commit or roll back atomically —
		// an exactly-once STATE TRANSITION on success (handler writes + processed-mark commit as one unit).
		// This is NOT exactly-once DELIVERY: on any throw everything rolls back, nothing is marked, and the
		// message redelivers for retry; a later duplicate is caught by the processed-check above, so delivery
		// is at-least-once and idempotent dedup makes processing effectively-once. Handler writes are atomic
		// with the mark ONLY when the handler routes them through the passed transaction (its Dapper calls
		// pass transaction: tx).
		// Tenant scoping on the claim/dedup path, by deployment mode. Multi-tenant (an ITenantContext is
		// registered) → the lock-check, the MERGE match key, and the synthesized INSERT all carry the resolved
		// TenantId (the triple), so two tenants sharing a message id can never dedup against each other. Non-MT
		// → FromContext(null)=None: no tenant predicate/fragments emitted and the MERGE keys on the pair. An
		// MT-active-but-unresolved context fails closed rather than reaching a predicate-less query.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var mergeSourceTenant = hasTenantColumn ? ", @TenantId AS TenantId" : string.Empty;
		var mergeOnTenant = hasTenantColumn ? " AND target.TenantId = source.TenantId" : string.Empty;
		var mergeInsertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var mergeInsertTenantVal = hasTenantColumn ? ", source.TenantId" : string.Empty;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// Serialize concurrent processors of the same (MessageId, HandlerType): UPDLOCK+HOLDLOCK holds a
			// key/range lock for the transaction, so a second caller blocks until this one commits/rolls back.
			var existingStatus = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
				$"SELECT Status FROM {_options.QualifiedTableName} WITH (UPDLOCK, HOLDLOCK) WHERE MessageId = @MessageId AND HandlerType = @HandlerType{tenantPredicate}",
				new { MessageId = messageId, HandlerType = handlerType, TenantId = TenantTerm },
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

			// Mark processed on the SAME transaction (insert-or-update to Processed) — atomic with the handler's writes.
			var now = DateTimeOffset.UtcNow;
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$$"""
				  MERGE {{_options.QualifiedTableName}} WITH (HOLDLOCK) AS target
				  USING (SELECT @MessageId AS MessageId, @HandlerType AS HandlerType{{mergeSourceTenant}}) AS source
				  ON target.MessageId = source.MessageId AND target.HandlerType = source.HandlerType{{mergeOnTenant}}
				  WHEN MATCHED THEN
				  	UPDATE SET Status = @ProcessedStatus, ProcessedAt = @Now, LastAttemptAt = @Now, LastError = NULL
				  WHEN NOT MATCHED THEN
				  	INSERT (MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount{{mergeInsertTenantCol}})
				  	VALUES (@MessageId, @HandlerType, '', 0x, '{}', @Now, @Now, @ProcessedStatus, 0{{mergeInsertTenantVal}});
				  """,
				new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed, Now = now, TenantId = TenantTerm },
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

		// Durably persist the in-flight Processing status (and the LastAttemptAt stamp the stuck-processing
		// timeout reads) BEFORE handler execution, so a concurrent delivery observes Processing via
		// GetEntryAsync and is skipped by the at-most-once guard. Keyed by (MessageId, HandlerType); the
		// middleware only calls this for an entry it just found in a non-Processed state.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET Status = @ProcessingStatus, LastAttemptAt = @LastAttemptAt
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				ProcessingStatus = (int)InboxStatus.Processing,
				LastAttemptAt = DateTimeOffset.UtcNow,
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

		// Atomic "first writer wins" using MERGE with HOLDLOCK
		// Returns 1 if this is a new insert (first processor), 0 if already exists
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var mergeSourceTenant = hasTenantColumn ? ", @TenantId AS TenantId" : string.Empty;
		var mergeOnTenant = hasTenantColumn ? " AND target.TenantId = source.TenantId" : string.Empty;
		var mergeInsertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var mergeInsertTenantVal = hasTenantColumn ? ", source.TenantId" : string.Empty;
		var sql = $$"""
		            MERGE {{_options.QualifiedTableName}} WITH (HOLDLOCK) AS target
		            USING (SELECT @MessageId AS MessageId, @HandlerType AS HandlerType{{mergeSourceTenant}}) AS source
		            ON target.MessageId = source.MessageId AND target.HandlerType = source.HandlerType{{mergeOnTenant}}
		            WHEN NOT MATCHED THEN
		            	INSERT (MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount{{mergeInsertTenantCol}})
		            	VALUES (@MessageId, @HandlerType, '', 0x, '{}', @Now, @Now, @ProcessedStatus, 0{{mergeInsertTenantVal}})
		            WHEN MATCHED AND target.Status = @ProcessedStatus THEN
		            	UPDATE SET MessageId = target.MessageId -- No-op update to satisfy MERGE syntax
		            OUTPUT $action AS Action;
		            """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				Now = DateTimeOffset.UtcNow,
				ProcessedStatus = (int)InboxStatus.Processed,
				TenantId = TenantTerm
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var action = await connection.QuerySingleOrDefaultAsync<string>(command).ConfigureAwait(false);
		var isFirstProcessor = action == "INSERT";

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

		// Atomic "first writer wins" claim into the NON-TERMINAL Processing state using MERGE with HOLDLOCK.
		// When NOT MATCHED a row is inserted ($action = INSERT -> claimed); when MATCHED no action is taken
		// (already claimed/processed -> OUTPUT yields no row -> claimed = false). Finalized via
		// MarkProcessedAsync, removed via ReleaseAsync.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var mergeSourceTenant = hasTenantColumn ? ", @TenantId AS TenantId" : string.Empty;
		var mergeOnTenant = hasTenantColumn ? " AND target.TenantId = source.TenantId" : string.Empty;
		var mergeInsertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var mergeInsertTenantVal = hasTenantColumn ? ", source.TenantId" : string.Empty;
		var sql = $$"""
		            MERGE {{_options.QualifiedTableName}} WITH (HOLDLOCK) AS target
		            USING (SELECT @MessageId AS MessageId, @HandlerType AS HandlerType{{mergeSourceTenant}}) AS source
		            ON target.MessageId = source.MessageId AND target.HandlerType = source.HandlerType{{mergeOnTenant}}
		            WHEN NOT MATCHED THEN
		            	INSERT (MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount{{mergeInsertTenantCol}})
		            	VALUES (@MessageId, @HandlerType, '', 0x, '{}', @Now, NULL, @ProcessingStatus, 0{{mergeInsertTenantVal}})
		            OUTPUT $action AS Action;
		            """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				Now = DateTimeOffset.UtcNow,
				ProcessingStatus = (int)InboxStatus.Processing,
				TenantId = TenantTerm
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var action = await connection.QuerySingleOrDefaultAsync<string>(command).ConfigureAwait(false);
		var claimed = action == "INSERT";

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
	public async ValueTask<bool> TryClaimAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

		// Single atomic lease CAS (MERGE with HOLDLOCK). Claim IFF the row is absent, Received, Failed
		// (a handler-failed entry is re-admittable so a redelivery retries — matching the non-lease
		// fallback path and the interface's release-on-failure contract), or a Processing entry whose
		// lease has expired (reclaiming a dead processor). A live Processing lease or a terminal Processed
		// row matches no action clause -> no OUTPUT row -> false. The lease-expiry comparison uses the
		// SERVER clock (SYSUTCDATETIME()) inside the statement so competing app instances never decide
		// expiry with a skewed local clock.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var mergeSourceTenant = hasTenantColumn ? ", @TenantId AS TenantId" : string.Empty;
		var mergeOnTenant = hasTenantColumn ? " AND target.TenantId = source.TenantId" : string.Empty;
		var mergeInsertTenantCol = hasTenantColumn ? ", TenantId" : string.Empty;
		var mergeInsertTenantVal = hasTenantColumn ? ", source.TenantId" : string.Empty;
		var sql = $$"""
		            MERGE {{_options.QualifiedTableName}} WITH (HOLDLOCK) AS target
		            USING (SELECT @MessageId AS MessageId, @HandlerType AS HandlerType{{mergeSourceTenant}}) AS source
		            ON target.MessageId = source.MessageId AND target.HandlerType = source.HandlerType{{mergeOnTenant}}
		            WHEN NOT MATCHED THEN
		            	INSERT (MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount, LeaseExpiresAtUtc{{mergeInsertTenantCol}})
		            	VALUES (@MessageId, @HandlerType, '', 0x, '{}', SYSUTCDATETIME(), NULL, @ProcessingStatus, 0, DATEADD(MILLISECOND, @LeaseMsRemainder, DATEADD(SECOND, @LeaseSeconds, SYSUTCDATETIME())){{mergeInsertTenantVal}})
		            WHEN MATCHED AND (target.Status = @ReceivedStatus
		            	OR target.Status = @FailedStatus
		            	OR (target.Status = @ProcessingStatus AND target.LeaseExpiresAtUtc < SYSUTCDATETIME())) THEN
		            	UPDATE SET Status = @ProcessingStatus,
		            		LeaseExpiresAtUtc = DATEADD(MILLISECOND, @LeaseMsRemainder, DATEADD(SECOND, @LeaseSeconds, SYSUTCDATETIME())),
		            		LastAttemptAt = SYSUTCDATETIME()
		            OUTPUT $action AS Action;
		            """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				TenantId = TenantTerm,
				// Split into whole seconds (int-seconds spans ~68 years, no overflow) + the sub-second
				// millisecond remainder, both applied server-side off SYSUTCDATETIME(). The prior single
				// (int)TotalMilliseconds overflowed past ~24.8 days into a negative DATEADD, writing an
				// already-expired lease (immediately reclaimable -> double-processing).
				LeaseSeconds = checked((int)(leaseDuration.Ticks / TimeSpan.TicksPerSecond)),
				LeaseMsRemainder = leaseDuration.Milliseconds,
				ProcessingStatus = (int)InboxStatus.Processing,
				ReceivedStatus = (int)InboxStatus.Received,
				FailedStatus = (int)InboxStatus.Failed
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var action = await connection.QuerySingleOrDefaultAsync<string>(command).ConfigureAwait(false);
		var claimed = action is "INSERT" or "UPDATE";

		if (claimed)
		{
			_logger.LogDebug("Lease-claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return claimed;
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// Remove the non-terminal claim so a redelivery can re-admit. Restricted to non-Processed rows so a
		// concurrently-finalized entry is never deleted. No-op if already removed or never claimed.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType AND Status != @ProcessedStatus{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { MessageId = messageId, HandlerType = handlerType, ProcessedStatus = (int)InboxStatus.Processed, TenantId = TenantTerm },
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

		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var sql = $"""
		           SELECT CASE WHEN EXISTS (
		           	SELECT 1 FROM {_options.QualifiedTableName}
		           	WHERE MessageId = @MessageId AND HandlerType = @HandlerType AND Status = @ProcessedStatus{tenantPredicate}
		           ) THEN 1 ELSE 0 END
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
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var tenantSelectColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var sql = $"""
		           SELECT MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	   Status, LastError, RetryCount, LastAttemptAt, NextAttemptAt, CorrelationId, Source{tenantSelectColumn}
		           FROM {_options.QualifiedTableName}
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType{tenantPredicate}
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
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET Status = @FailedStatus, LastError = @LastError, RetryCount = RetryCount + 1, LastAttemptAt = @LastAttemptAt
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				FailedStatus = (int)InboxStatus.Failed,
				LastError = errorMessage,
				LastAttemptAt = DateTimeOffset.UtcNow,
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
		// without consuming a delivery attempt (FR-4). UPDATE-only: same existence semantics as the
		// incrementing overload (a missing row affects 0 rows).
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET Status = @FailedStatus, LastError = @LastError, RetryCount = @RetryCount, LastAttemptAt = @LastAttemptAt
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				FailedStatus = (int)InboxStatus.Failed,
				LastError = errorMessage,
				RetryCount = retryCount,
				LastAttemptAt = DateTimeOffset.UtcNow,
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

		// Set RetryCount EXACTLY (the processor passes the consumed attempt count it used to compute
		// nextAttemptAt) and persist the backoff schedule, so the re-admission claim (GetFailedEntriesAsync)
		// excludes this entry until nextAttemptAt has elapsed -- the inbox half of FR-R3.2 (aed1gl),
		// mirroring SqlServerOutboxStore.MarkFailedWithBackoffAsync.
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantPredicate = hasTenantColumn ? " AND TenantId = @TenantId" : string.Empty;
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET Status = @FailedStatus, LastError = @LastError, RetryCount = @RetryCount,
		           	LastAttemptAt = @LastAttemptAt, NextAttemptAt = @NextAttemptAt
		           WHERE MessageId = @MessageId AND HandlerType = @HandlerType{tenantPredicate}
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new
			{
				MessageId = messageId,
				HandlerType = handlerType,
				FailedStatus = (int)InboxStatus.Failed,
				LastError = errorMessage,
				RetryCount = retryCount,
				LastAttemptAt = DateTimeOffset.UtcNow,
				NextAttemptAt = nextAttemptAt,
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
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantSelectColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var sql = $"""
		           SELECT TOP (@BatchSize)
		           	MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	Status, LastError, RetryCount, LastAttemptAt, NextAttemptAt, CorrelationId, Source{tenantSelectColumn}
		           FROM {_options.QualifiedTableName}
		           WHERE Status = @FailedStatus
		           	AND RetryCount < @MaxRetries
		           	AND (@OlderThan IS NULL OR LastAttemptAt < @OlderThan)
		           	AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)
		           ORDER BY RetryCount ASC, LastAttemptAt ASC
		           """;

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var command = new CommandDefinition(
			sql,
			new { BatchSize = batchSize, FailedStatus = (int)InboxStatus.Failed, MaxRetries = maxRetries, OlderThan = olderThan, Now = DateTimeOffset.UtcNow },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<InboxEntryRow>(command).ConfigureAwait(false);
		return rows.Select(MapRowToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		var hasTenantColumn = await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
		var tenantSelectColumn = hasTenantColumn ? ", TenantId" : string.Empty;
		var sql = $"""
		           SELECT MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt,
		           	   Status, LastError, RetryCount, LastAttemptAt, NextAttemptAt, CorrelationId, Source{tenantSelectColumn}
		           FROM {_options.QualifiedTableName}
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
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var sql = $"""
		           SELECT
		           	COUNT(*) AS TotalEntries,
		           	SUM(CASE WHEN Status = @ProcessedStatus THEN 1 ELSE 0 END) AS ProcessedEntries,
		           	SUM(CASE WHEN Status = @FailedStatus THEN 1 ELSE 0 END) AS FailedEntries,
		           	SUM(CASE WHEN Status = @ReceivedStatus OR Status = @ProcessingStatus THEN 1 ELSE 0 END) AS PendingEntries
		           FROM {_options.QualifiedTableName}
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
	public async ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE Status = @ProcessedStatus AND ProcessedAt < @CutoffDate
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

	private static Func<SqlConnection> CreateConnectionFactory(SqlServerInboxOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return () => new SqlConnection(options.ConnectionString);
	}

	#region Private Methods

#pragma warning disable IL2026, IL3050 // JSON serialization for inbox metadata
	private string SerializeMetadata(IDictionary<string, object> metadata)
	{
		return JsonSerializer.Serialize(metadata, _jsonOptions);
	}
#pragma warning restore IL2026, IL3050

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
			NextAttemptAt = row.NextAttemptAt,
			CorrelationId = row.CorrelationId,
			TenantId = row.TenantId,
			Source = row.Source
		};
	}

	#endregion Private Methods

	#region Row Types

	private sealed class InboxEntryRow
	{
		public string MessageId { get; set; } = string.Empty;
		public string HandlerType { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public byte[] Payload { get; set; } = [];
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

	#endregion Row Types
}
