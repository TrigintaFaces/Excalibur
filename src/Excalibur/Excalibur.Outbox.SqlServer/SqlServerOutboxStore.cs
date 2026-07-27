// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Metadata;
using Excalibur.Dispatch.Serialization;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IOutboxStore" /> with per-transport delivery tracking.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides reliable message storage and delivery tracking for the transactional outbox pattern. It supports both
/// single-transport and multi-transport scenarios.
/// </para>
/// <para>
/// Multi-transport support allows messages to be published to multiple transports (e.g., RabbitMQ and Kafka) with independent delivery
/// tracking for each transport.
/// </para>
/// <para> This class supports two constructor patterns:
/// <list type="bullet">
/// <item>
/// <description> Simple: Options-based for most users </description>
/// </item>
/// <item>
/// <description> Advanced: Connection factory for multi-database, pooling, or IDb integration </description>
/// </item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
	Justification = "Store class implements multiple ISP sub-interfaces (IMultiTransportOutboxStore, IOutboxStoreAdmin, IOutboxStoreBatch, ITransactionalOutboxWriter) by design.")]
public sealed class SqlServerOutboxStore : IMultiTransportOutboxStore, IFencedOutboxStore, IMultiTransportOutboxStoreAdmin, IOutboxStoreAdmin, IOutboxStoreBatch, IDeadLetterableOutboxStore, IBackoffSchedulableOutboxStore, ITransactionalOutboxWriter
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly SqlServerOutboxOptions _options;
	private readonly SqlServerInboxOptions? _inboxOptions;
	private readonly ILogger<SqlServerOutboxStore> _logger;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly JsonSerializerOptions _jsonOptions;

	// Optional, exactly as SqlServerDeadLetterQueue takes it in this package. Absent in a single-tenant
	// host, where every read resolves to the reserved untenanted partition rather than to "no predicate".
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxStore" /> class.
	/// </summary>
	/// <param name="options"> The configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <remarks>
	/// This is the simple constructor for most users. Use the overload that takes a
	/// <see cref="Func{TResult}" /> connection factory together with a <see cref="SqlServerOutboxOptions" />
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	internal SqlServerOutboxStore(
		IOptions<SqlServerOutboxOptions> options,
		ILogger<SqlServerOutboxStore> logger)
		: this(options, payloadSerializer: null, inboxOptions: null, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxStore" /> class with inbox options for transactional completion.
	/// </summary>
	/// <param name="options"> The configuration options. </param>
	/// <param name="inboxOptions">
	/// Optional inbox configuration for transactional outbox+inbox completion. When provided and connection strings match, enables
	/// effectively-once processing via <see cref="TryMarkSentAndReceivedAsync" />. Delivery remains at-least-once.
	/// </param>
	/// <param name="logger"> The logger instance. </param>
	internal SqlServerOutboxStore(
		IOptions<SqlServerOutboxOptions> options,
		IOptions<SqlServerInboxOptions>? inboxOptions,
		ILogger<SqlServerOutboxStore> logger)
		: this(options, payloadSerializer: null, inboxOptions, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxStore" /> class with pluggable serialization.
	/// </summary>
	/// <param name="options"> The configuration options. </param>
	/// <param name="payloadSerializer">
	/// The payload serializer for message serialization. If null, falls back to System.Text.Json for backward compatibility.
	/// </param>
	/// <param name="inboxOptions">
	/// Optional inbox configuration for transactional outbox+inbox completion. When provided and connection strings match, enables
	/// effectively-once processing via <see cref="TryMarkSentAndReceivedAsync" />. Delivery remains at-least-once.
	/// </param>
	/// <param name="logger"> The logger instance. </param>
	/// <remarks>
	/// This is the simple constructor for most users; use the connection-factory overload for advanced scenarios
	/// like multi-database setups or custom connection pooling.
	/// </remarks>
	internal SqlServerOutboxStore(
		IOptions<SqlServerOutboxOptions> options,
		IPayloadSerializer? payloadSerializer,
		IOptions<SqlServerInboxOptions>? inboxOptions,
		ILogger<SqlServerOutboxStore> logger)
		: this(
			CreateConnectionFactory((options ?? throw new ArgumentNullException(nameof(options))).Value),
			options.Value,
			payloadSerializer,
			inboxOptions?.Value,
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxStore" /> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection" /> instances. The caller is responsible for ensuring the factory returns
	/// properly configured connections.
	/// </param>
	/// <param name="options"> The configuration options (used for table names, timeouts, etc.). </param>
	/// <param name="logger"> The logger instance. </param>
	/// <remarks>
	/// <para> This is the advanced constructor for scenarios that need custom connection management: </para>
	/// <list type="bullet">
	/// <item>
	/// <description> Multi-database setups with marker interfaces (e.g., IDomainDb, IOutboxDb) </description>
	/// </item>
	/// <item>
	/// <description> Custom connection pooling </description>
	/// </item>
	/// <item>
	/// <description> Integration with <see cref="IDb" /> abstraction </description>
	/// </item>
	/// </list>
	/// <para> Example with IDb:
	/// <code>
	///new SqlServerOutboxStore(
	///() =&gt; (SqlConnection)outboxDb.Connection,
	///options,
	///logger);
	/// </code>
	/// </para>
	/// </remarks>
	internal SqlServerOutboxStore(
		Func<SqlConnection> connectionFactory,
		SqlServerOutboxOptions options,
		ILogger<SqlServerOutboxStore> logger)
		: this(connectionFactory, options, payloadSerializer: null, inboxOptions: null, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxStore" /> class with a connection factory and pluggable serialization.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection" /> instances. The caller is responsible for ensuring the factory returns
	/// properly configured connections.
	/// </param>
	/// <param name="options"> The configuration options (used for table names, timeouts, etc.). </param>
	/// <param name="payloadSerializer">
	/// The payload serializer for message serialization. If null, falls back to System.Text.Json for backward compatibility.
	/// </param>
	/// <param name="logger"> The logger instance. </param>
	/// <remarks>
	/// <para> This is the advanced constructor for scenarios that need custom connection management. </para>
	/// <para>
	/// Tenant isolation is enforced on the write/stage path (each message carries and persists its own
	/// <c>TenantId</c>) and on tenant-facing queries; the drain/mark-by-Id path is cross-tenant infrastructure
	/// and stays global (it addresses the globally-unique outbox <c>Id</c>). The store reads no ambient tenant
	/// context — fail-closed enforcement is provided by the multi-tenancy composition.
	/// </para>
	/// <para>
	/// Retention is the one remaining unscoped path, and it is global for a different reason than the drain:
	/// it matches rows by age rather than by id, so the by-id justification above does not extend to it. It is
	/// an administrative estate-wide sweep, and <see cref="CleanupAllTenantsSentMessagesAsync"/> carries that
	/// scope in its name rather than inheriting an exemption written for a different shape.
	/// </para>
	/// <para> To enable transactional outbox+inbox completion, use the overload that accepts <see cref="SqlServerInboxOptions" />. </para>
	/// </remarks>
	internal SqlServerOutboxStore(
		Func<SqlConnection> connectionFactory,
		SqlServerOutboxOptions options,
		IPayloadSerializer? payloadSerializer,
		ILogger<SqlServerOutboxStore> logger)
		: this(connectionFactory, options, payloadSerializer, inboxOptions: null, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxStore" /> class with a connection factory, pluggable serialization, and
	/// inbox options for transactional completion.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection" /> instances. The caller is responsible for ensuring the factory returns
	/// properly configured connections.
	/// </param>
	/// <param name="options"> The configuration options (used for table names, timeouts, etc.). </param>
	/// <param name="payloadSerializer">
	/// The payload serializer for message serialization. If null, falls back to System.Text.Json for backward compatibility.
	/// </param>
	/// <param name="inboxOptions">
	/// Optional inbox configuration for transactional outbox+inbox completion. When provided and connection strings match, enables
	/// effectively-once processing via <see cref="TryMarkSentAndReceivedAsync" />. Delivery remains at-least-once.
	/// </param>
	/// <param name="logger"> The logger instance. </param>
	/// <remarks>
	/// <para> This is the advanced constructor for scenarios that need custom connection management: </para>
	/// <list type="bullet">
	/// <item>
	/// <description> Multi-database setups with marker interfaces (e.g., IDomainDb, IOutboxDb) </description>
	/// </item>
	/// <item>
	/// <description> Custom connection pooling </description>
	/// </item>
	/// <item>
	/// <description> Integration with <see cref="IDb" /> abstraction </description>
	/// </item>
	/// </list>
	/// <para> Example with IDb:
	/// <code>
	///new SqlServerOutboxStore(
	///() =&gt; (SqlConnection)outboxDb.Connection,
	///options,
	///payloadSerializer,
	///inboxOptions,
	///logger);
	/// </code>
	/// </para>
	/// </remarks>
	/// <param name="tenantContext">
	/// The ambient tenant, when the host established one. Optional: a single-tenant host supplies none and
	/// every tenant-facing read resolves to the reserved untenanted partition, which is a real predicate
	/// rather than an absent one. The drain path does not consult this — it is deliberately cross-tenant.
	/// </param>
	internal SqlServerOutboxStore(
		Func<SqlConnection> connectionFactory,
		SqlServerOutboxOptions options,
		IPayloadSerializer? payloadSerializer,
		SqlServerInboxOptions? inboxOptions,
		ILogger<SqlServerOutboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_options = options;
		_inboxOptions = inboxOptions;
		_payloadSerializer = payloadSerializer;
		_logger = logger;
		_tenantContext = tenantContext;
		// Canonical event-serialization contract (camelCase + enum-as-string + omit-null), shared with every
		// store and the default serializer, so persisted payloads/headers/metadata round-trip byte-for-byte.
		_jsonOptions = EventSerializationDefaults.Canonical;
	}

	/// <inheritdoc />
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Insert main message
			await InsertMessageAsync(connection, transaction, message, cancellationToken).ConfigureAwait(false);

			// Insert transport delivery records if multi-transport
			if (message.IsMultiTransport && message.TransportDeliveries.Count > 0)
			{
				foreach (var delivery in message.TransportDeliveries)
				{
					await InsertTransportDeliveryAsync(connection, transaction, delivery, message.TenantId, cancellationToken).ConfigureAwait(false);
				}
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Staged outbox message {MessageId} of type {MessageType}",
				message.Id, message.MessageType);
		}
		catch (Exception ex) when (TryGetDuplicateKeyViolation(ex, out var duplicateEx))
		{
			result = WriteStoreTelemetry.Results.Conflict;
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"stage",
				message.Id,
				message.CorrelationId,
				message.CausationId);
			_logger.LogWarning(
				duplicateEx,
				"Duplicate outbox message detected for {MessageId}",
				message.Id);
			throw new InvalidOperationException(
				$"Outbox message '{message.Id}' already exists.",
				ex);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"stage",
				message.Id,
				message.CorrelationId,
				message.CausationId);
			_logger.LogError(ex, "Failed to stage outbox message {MessageId}", message.Id);
			throw;
		}
		finally
		{
			RecordOperation("stage", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		// Derive the routing destination from the message context (cys98n) — falling back to the message
		// type name rather than a hardcoded "default", so a consumer's configured destination is persisted
		// and honored on dispatch (identical fix to the Postgres provider).
		var messageTypeName = message.GetType().FullName ?? message.GetType().Name;
		var outboundMessage = OutboundMessage.FromContext(
			messageTypeName,
			SerializePayload(message),
			context.ExtractMetadata().GetDestination() ?? messageTypeName,
			context,
			context.Items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal));
		try
		{
			await StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("enqueue", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
		GetUnsentMessagesCoreAsync(batchSize, fencingToken: null, cancellationToken);

	/// <inheritdoc />
	/// <remarks>
	/// Leadership fencing is durable: the high-water mark lives in the dedicated <c>OutboxFence</c> control
	/// table, which cleanup never touches, so a superseded leader's stale token is still rejected after a
	/// cleanup has purged the sent, token-bearing rows. The per-message lease independently prevents two
	/// processors from claiming the same row.
	/// </remarks>
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, long fencingToken, CancellationToken cancellationToken) =>
		GetUnsentMessagesCoreAsync(batchSize, fencingToken, cancellationToken);

	private async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesCoreAsync(
		int batchSize,
		long? fencingToken,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// Fence FIRST: advance the durable high-water and detect supersession before any claim. A stale
			// (superseded) token yields ZERO claimable rows and MUST NOT throw — throwing would crash-loop a
			// superseded leader's drain. The claim below re-guards against the same durable high-water, so a
			// leader superseded between this advance and the claim still claims nothing (no TOCTOU window).
			if (fencingToken.HasValue)
			{
				var highWater = await connection.ResolveAsync(
						new Requests.EnforceOutboxFenceRequest(
							_options.Tables.QualifiedFenceTableName,
							_options.Tables.QualifiedOutboxTableName,
							fencingToken.Value,
							_options.Processing.CommandTimeoutSeconds,
							transaction: null,
							cancellationToken))
					.ConfigureAwait(false);

				if (highWater > fencingToken.Value)
				{
					return [];
				}
			}

			var rows = await connection.ResolveAsync(
					new Requests.GetUnsentMessagesRequest(
						_options.Tables.QualifiedOutboxTableName,
						batchSize,
						_options.Processing.CommandTimeoutSeconds,
						_options.Processing.LeaseTimeoutSeconds,
						_options.ProcessorId,
						fencingToken,
						_options.Tables.QualifiedFenceTableName,
						_options.Tables.QualifiedOutboxTableName,
						cancellationToken))
				.ConfigureAwait(false);

			var messages = new List<OutboundMessage>();

			foreach (var row in rows)
			{
				var message = MapRowToMessage(row);

				// Load transport deliveries for multi-transport messages
				if (message.IsMultiTransport)
				{
					var deliveries = await GetTransportDeliveriesInternalAsync(connection, message.Id, cancellationToken)
						.ConfigureAwait(false);
					foreach (var delivery in deliveries)
					{
						message.TransportDeliveries.Add(delivery);
					}
				}

				messages.Add(message);
			}

			return messages;
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_unsent", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
		MarkSentCoreAsync(messageId, fencingToken: null, cancellationToken);

	/// <inheritdoc />
	/// <remarks>
	/// Leadership fencing is durable: the high-water mark lives in the dedicated <c>OutboxFence</c> control
	/// table, which cleanup never touches, so a superseded leader's stale token is still rejected after a
	/// cleanup has purged the sent, token-bearing rows. The per-message lease independently prevents two
	/// processors from marking the same row.
	/// </remarks>
	public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) =>
		MarkSentCoreAsync(messageId, fencingToken, cancellationToken);

	private async ValueTask MarkSentCoreAsync(string messageId, long? fencingToken, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// Fence FIRST: monotonically advance the durable high-water and capture it. The mark UPDATE below
			// re-guards against this same durable high-water inside its own statement (guard + mutation are one
			// atomic step), so a leader superseded between this advance and the mark affects zero rows and is
			// reported fail-closed. The captured value is the recorded high-water reported on the diagnostic —
			// it survives a cleanup that purges the token-bearing rows, so a stale token stays rejected.
			long? recordedHighWater = null;
			if (fencingToken.HasValue)
			{
				recordedHighWater = await connection.ResolveAsync(
						new Requests.EnforceOutboxFenceRequest(
							_options.Tables.QualifiedFenceTableName,
							_options.Tables.QualifiedOutboxTableName,
							fencingToken.Value,
							_options.Processing.CommandTimeoutSeconds,
							transaction: null,
							cancellationToken))
					.ConfigureAwait(false);
			}

			var affected = await connection.ResolveAsync(
					new Requests.MarkMessageSentRequest(
						_options.Tables.QualifiedOutboxTableName,
						messageId,
						_options.Processing.CommandTimeoutSeconds,
						fencingToken,
						_options.Tables.QualifiedFenceTableName,
						_options.Tables.QualifiedOutboxTableName,
						cancellationToken))
				.ConfigureAwait(false);

			if (affected == 0)
			{
				// Distinguish "not found" from "fencing rejected" so a superseded leader gets a fail-closed
				// signal (StaleOutboxFencingTokenException) rather than a generic not-found error.
				if (fencingToken.HasValue)
				{
					var exists = await connection.ExecuteScalarAsync<int>(
						new CommandDefinition(
							$"SELECT COUNT(1) FROM {_options.Tables.QualifiedOutboxTableName} WHERE Id = @MessageId",
							new { MessageId = messageId },
							commandTimeout: _options.Processing.CommandTimeoutSeconds,
							cancellationToken: cancellationToken)).ConfigureAwait(false);

					if (exists > 0)
					{
						// Report the recorded high-water the presented token was fenced against (the fencing
						// contract's diagnostic). It is the durable OutboxFence high-water captured by the
						// fence-first advance above — the same value the mark-sent guard compares against. Because
						// that control row is never deleted by cleanup, the high-water survives the purge of the
						// sent, token-bearing rows and a superseded leader's stale token stays rejected.
						result = WriteStoreTelemetry.Results.Conflict;
						throw new StaleOutboxFencingTokenException(
							$"The presented outbox fencing token ({fencingToken.Value}) for message '{messageId}' was rejected as stale (recorded high-water {recordedHighWater}).")
						{
							PresentedToken = fencingToken.Value,
							HighWaterToken = recordedHighWater,
						};
					}
				}

				result = WriteStoreTelemetry.Results.NotFound;
				throw new InvalidOperationException($"Message {messageId} not found or already sent.");
			}

			_logger.LogDebug("Marked message {MessageId} as sent", messageId);
		}
		catch
		{
			if (result is not (WriteStoreTelemetry.Results.NotFound or WriteStoreTelemetry.Results.Conflict))
			{
				result = WriteStoreTelemetry.Results.Failure;
			}

			throw;
		}
		finally
		{
			RecordOperation("mark_sent", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask MarkBatchSentAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
	{
		if (messageIds.Count == 0)
		{
			return;
		}

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var sql = $"""
				UPDATE {_options.Tables.QualifiedOutboxTableName}
				SET Status = 2, SentAt = @SentAt, LastError = NULL, LeasedAt = NULL, LeasedBy = NULL
				WHERE Id IN @Ids AND Status != 2
				""";

			var affected = await connection.ExecuteAsync(
				new CommandDefinition(
					sql,
					new { Ids = messageIds, SentAt = DateTimeOffset.UtcNow },
					commandTimeout: _options.Processing.CommandTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			_logger.LogDebug("Batch marked {Count}/{Total} messages as sent", affected, messageIds.Count);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_batch_sent", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask MarkBatchFailedAsync(IReadOnlyList<string> messageIds, string reason, int retryCount, CancellationToken cancellationToken)
	{
		if (messageIds.Count == 0)
		{
			return;
		}

		ArgumentNullException.ThrowIfNull(reason);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var sql = $"""
				UPDATE {_options.Tables.QualifiedOutboxTableName}
				SET Status = 3, RetryCount = RetryCount + 1, LastError = @Reason, LastAttemptAt = @Now,
					LeasedAt = NULL, LeasedBy = NULL
				WHERE Id IN @Ids
				""";

			var affected = await connection.ExecuteAsync(
				new CommandDefinition(
					sql,
					new { Ids = messageIds, Reason = reason, Now = DateTimeOffset.UtcNow },
					commandTimeout: _options.Processing.CommandTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			_logger.LogWarning("Batch marked {Count}/{Total} messages as failed: {Reason}", affected, messageIds.Count, reason);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_batch_failed", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para> This SQL Server implementation performs atomic transactional completion when:
	/// <list type="bullet">
	/// <item>
	/// <description> Inbox options are configured via constructor </description>
	/// </item>
	/// <item>
	/// <description> Outbox and inbox connection strings match (same database) </description>
	/// </item>
	/// </list>
	/// </para>
	/// <para> When these conditions are met, the method uses a local SQL Server transaction to:
	/// <list type="number">
	/// <item>
	/// <description> Mark the outbox message as sent (UPDATE) </description>
	/// </item>
	/// <item>
	/// <description> Insert the inbox entry for deduplication (INSERT) </description>
	/// </item>
	/// </list>
	/// Both operations succeed or fail together, so a redelivered message is skipped rather than reprocessed (effectively-once processing).
	/// Delivery itself remains at-least-once; handlers must be idempotent.
	/// </para>
	/// </remarks>
	public async ValueTask<bool> TryMarkSentAndReceivedAsync(
		string messageId,
		InboxEntry inboxEntry,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(inboxEntry);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		// Check if same-database transactional completion is possible
		if (!CanUseTransactionalCompletion())
		{
			_logger.LogDebug(
				"Transactional completion not available for message {MessageId}: inbox options not configured or different database",
				messageId);
			RecordOperation("mark_sent_and_received", result, stopwatch.Elapsed);
			return false;
		}

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Use ReadCommitted isolation level per AD-223-3
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
			IsolationLevel.ReadCommitted,
			cancellationToken).ConfigureAwait(false);

		try
		{
			// Step 1: Mark outbox message as sent
			var markSentSql = $"""
			                   UPDATE {_options.Tables.QualifiedOutboxTableName}
			                   SET Status = 2, SentAt = @SentAt, LastError = NULL
			                   WHERE Id = @MessageId
			                   """;

			var markSentCommand = new CommandDefinition(
				markSentSql,
				new { MessageId = messageId, SentAt = DateTimeOffset.UtcNow },
				transaction,
				_options.Processing.CommandTimeoutSeconds,
				cancellationToken: cancellationToken);

			var affected = await connection.ExecuteAsync(markSentCommand).ConfigureAwait(false);
			if (affected == 0)
			{
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				result = WriteStoreTelemetry.Results.NotFound;
				throw new InvalidOperationException($"Message {messageId} not found or already sent.");
			}

			// Step 2: Insert inbox entry for deduplication
			var insertInboxSql = $"""
			                      INSERT INTO {_inboxOptions!.QualifiedTableName}
			                      	(MessageId, HandlerType, MessageType, Payload, Metadata, ReceivedAt, ProcessedAt, Status, RetryCount, CorrelationId, TenantId, Source)
			                      VALUES
			                      	(@MessageId, @HandlerType, @MessageType, @Payload, @Metadata, @ReceivedAt, @ProcessedAt, @Status, @RetryCount, @CorrelationId, @TenantId, @Source)
			                      """;

			var insertInboxCommand = new CommandDefinition(
				insertInboxSql,
				new
				{
					inboxEntry.MessageId,
					inboxEntry.HandlerType,
					inboxEntry.MessageType,
					inboxEntry.Payload,
					Metadata = SerializeMetadataForInbox(inboxEntry.Metadata),
					inboxEntry.ReceivedAt,
					ProcessedAt = inboxEntry.ProcessedAt ?? DateTimeOffset.UtcNow,
					Status = (int)inboxEntry.Status,
					inboxEntry.RetryCount,
					inboxEntry.CorrelationId,
					inboxEntry.TenantId,
					inboxEntry.Source
				},
				transaction,
				_options.Processing.CommandTimeoutSeconds,
				cancellationToken: cancellationToken);

			_ = await connection.ExecuteAsync(insertInboxCommand).ConfigureAwait(false);

			// Commit both operations atomically
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Transactional completion succeeded for message {MessageId} with inbox entry for handler {HandlerType}",
				messageId,
				inboxEntry.HandlerType);

			return true;
		}
		catch (SqlException ex) when (ex.Number is 2627 or 2601) // Unique constraint violation on inbox
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			result = WriteStoreTelemetry.Results.Conflict;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"mark_sent_and_received",
				messageId,
				inboxEntry.CorrelationId);
			_logger.LogWarning(
				"Transactional completion failed for message {MessageId}: inbox entry already exists for handler {HandlerType}",
				messageId,
				inboxEntry.HandlerType);
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{inboxEntry.HandlerType}'.", ex);
		}
		catch (Exception ex)
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"mark_sent_and_received",
				messageId,
				inboxEntry.CorrelationId);
			_logger.LogError(ex, "Transactional completion failed for message {MessageId}", messageId);
			throw;
		}
		finally
		{
			RecordOperation("mark_sent_and_received", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask MarkFailedAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var rowsAffected = await connection.ResolveAsync(
					new Requests.MarkMessageFailedRequest(
						_options.Tables.QualifiedOutboxTableName,
						messageId,
						errorMessage,
						retryCount,
						_options.ProcessorId,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken,
						floorSeconds: _options.Processing.FailureBackoffFloorSeconds))
				.ConfigureAwait(false);

			// See the sibling overload: no rows means this attempt no longer owns the message, or the message
			// was already delivered. Reporting it as a completed transition would hide exactly the case an
			// operator is trying to explain.
			if (rowsAffected == 0)
			{
				_logger.LogWarning(
					"MarkFailed for message {MessageId} matched no rows: it was delivered, or a peer holds the "
					+ "lease. No state change was made and this attempt no longer owns the message.",
					messageId);
			}
			else
			{
				_logger.LogWarning("Marked message {MessageId} as failed: {Error}", messageId, errorMessage);
			}
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_failed", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var rowsAffected = await connection.ResolveAsync(
					new Requests.MarkMessageFailedRequest(
						_options.Tables.QualifiedOutboxTableName,
						messageId,
						errorMessage,
						retryCount,
						_options.ProcessorId,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken,
						nextAttemptAt))
				.ConfigureAwait(false);

			// A zero-row mark is NOT a success. The statement is guarded on ownership and on the message not
			// already being Sent, so no rows means either a peer re-claimed this message or it was delivered
			// while this attempt was in flight. Logging the success line regardless would assert a state
			// transition that did not happen, and this path is precisely where an operator looks to explain
			// a redelivery.
			if (rowsAffected == 0)
			{
				_logger.LogWarning(
					"MarkFailed for message {MessageId} matched no rows: it was delivered, or a peer holds the "
					+ "lease. No state change was made and this attempt no longer owns the message.",
					messageId);
			}
			else
			{
				_logger.LogWarning(
					"Marked message {MessageId} as failed with backoff (next attempt at {NextAttemptAt:O}): {Error}",
					messageId, nextAttemptAt, errorMessage);
			}
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_failed_with_backoff", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ResolveAsync(
					new Requests.MarkMessageDeadLetteredRequest(
						_options.Tables.QualifiedOutboxTableName,
						messageId,
						reason,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			_logger.LogWarning("Marked message {MessageId} as dead-lettered (terminal): {Reason}", messageId, reason);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_dead_lettered", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var rows = await connection.ResolveAsync(
					new Requests.GetFailedMessagesRequest(
						_options.Tables.QualifiedOutboxTableName,
						maxRetries,
						olderThan,
						batchSize,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			return rows.Select(MapRowToMessage);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_failed", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var rows = await connection.ResolveAsync(
					new Requests.GetScheduledMessagesRequest(
						_options.Tables.QualifiedOutboxTableName,
						scheduledBefore,
						batchSize,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			return rows.Select(MapRowToMessage);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_scheduled", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask<int> CleanupAllTenantsSentMessagesAsync(
		DateTimeOffset olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Delete transport deliveries first
			_ = await connection.ResolveAsync(
					new Requests.CleanupTransportDeliveriesRequest(
						_options.Tables.QualifiedOutboxTableName,
						_options.Tables.QualifiedTransportsTableName,
						olderThan,
						batchSize,
						transaction,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			// Then delete messages
			var deleted = await connection.ResolveAsync(
					new Requests.CleanupSentMessagesRequest(
						_options.Tables.QualifiedOutboxTableName,
						olderThan,
						batchSize,
						transaction,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Cleaned up {Count} sent messages older than {OlderThan}", deleted, olderThan);

			return deleted;
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}
		finally
		{
			RecordOperation("cleanup_sent", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return await connection.ResolveAsync(
					new Requests.GetOutboxStatisticsRequest(
						_options.Tables.QualifiedOutboxTableName,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_stats", result, stopwatch.Elapsed);
		}
	}

	private static Func<SqlConnection> CreateConnectionFactory(SqlServerOutboxOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Apply ApplicationName for connection pool isolation
		var connectionString = options.ConnectionString;
		if (!string.IsNullOrWhiteSpace(options.ApplicationName))
		{
			var builder = new SqlConnectionStringBuilder(connectionString)
			{
				ApplicationName = options.ApplicationName,
			};
			connectionString = builder.ConnectionString;
		}

		return () => new SqlConnection(connectionString);
	}

	private static void RecordOperation(string operation, string result, TimeSpan duration)
	{
		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.OutboxStore,
			WriteStoreTelemetry.Providers.SqlServer,
			operation,
			result,
			duration);
	}

	/// <summary>
	/// Determines whether transactional outbox+inbox completion is available.
	/// </summary>
	/// <returns> <see langword="true" /> if inbox options are configured and connection strings match; otherwise, <see langword="false" />. </returns>
	private bool CanUseTransactionalCompletion()
	{
		if (_inboxOptions is null)
		{
			return false;
		}

		// Compare connection strings to detect same-database scenario Use case-insensitive comparison as connection string keys are case-insensitive
		return string.Equals(
			_options.ConnectionString,
			_inboxOptions.ConnectionString,
			StringComparison.OrdinalIgnoreCase);
	}

	private string SerializeMetadataForInbox(IDictionary<string, object> metadata)
	{
#pragma warning disable IL2026, IL3050 // JsonSerializer with Type parameter requires unreferenced code
		return JsonSerializer.Serialize(metadata, _jsonOptions);
#pragma warning restore IL2026, IL3050
	}

	#region Per-Transport Methods

	/// <summary>
	/// Marks a specific transport delivery as sent.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="transportName"> The transport name. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	public async Task MarkTransportSentAsync(
		string messageId,
		string transportName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ResolveAsync(
					new Requests.MarkTransportSentRequest(
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						transportName,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			// Update aggregate status
			_ = await connection.ResolveAsync(
					new Requests.UpdateAggregateStatusRequest(
						_options.Tables.QualifiedOutboxTableName,
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Marked transport {TransportName} as sent for message {MessageId}", transportName, messageId);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_transport_sent", result, stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Marks a specific transport delivery as failed.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="transportName"> The transport name. </param>
	/// <param name="errorMessage"> The error message. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	public async Task MarkTransportFailedAsync(
		string messageId,
		string transportName,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ResolveAsync(
					new Requests.MarkTransportFailedRequest(
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						transportName,
						errorMessage,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			// Update aggregate status
			_ = await connection.ResolveAsync(
					new Requests.UpdateAggregateStatusRequest(
						_options.Tables.QualifiedOutboxTableName,
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			_logger.LogWarning("Marked transport {TransportName} as failed for message {MessageId}: {Error}",
				transportName, messageId, errorMessage);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_transport_failed", result, stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Gets pending transport deliveries for a specific transport.
	/// </summary>
	/// <param name="transportName"> The transport name. </param>
	/// <param name="batchSize"> Maximum number of deliveries to retrieve. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Collection of pending transport deliveries with their parent messages. </returns>
	public async Task<IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)>> GetPendingTransportDeliveriesAsync(
		string transportName,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		var sql = $"""
		           SELECT TOP (@BatchSize)
		           	m.Id, m.MessageType, m.Payload, m.Headers, m.Destination, m.CreatedAt, m.ScheduledAt, m.SentAt,
		           	m.Status, m.RetryCount, m.LastError, m.LastAttemptAt, m.CorrelationId, m.CausationId,
		           	m.TenantId, m.Priority, m.TargetTransports, m.IsMultiTransport,
		           	m.PartitionKey, m.GroupKey, m.SequenceNumber,
		           	t.Id AS TransportId, t.MessageId, t.TransportName, t.Destination AS TransportDestination,
		           	t.Status AS TransportStatus, t.CreatedAt AS TransportCreatedAt, t.AttemptedAt, t.SentAt AS TransportSentAt,
		           	t.RetryCount AS TransportRetryCount, t.LastError AS TransportLastError, t.TransportMetadata
		           FROM {_options.Tables.QualifiedOutboxTableName} m
		           INNER JOIN {_options.Tables.QualifiedTransportsTableName} t ON m.Id = t.MessageId
		           WHERE t.TransportName = @TransportName
		           	AND t.Status IN (0, 3) -- Pending, Failed
		           	AND t.RetryCount < @MaxRetries
		           ORDER BY m.PartitionKey, m.SequenceNumber ASC, m.Priority DESC, m.CreatedAt ASC
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { TransportName = transportName, BatchSize = batchSize, MaxRetries = _options.Processing.MaxRetryCount },
			commandTimeout: _options.Processing.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			var results = new List<(OutboundMessage, OutboundMessageTransport)>();

			var rows = await connection
				.QueryAsync<Requests.OutboxMessageRow, TransportDeliveryRow, (Requests.OutboxMessageRow, TransportDeliveryRow)>(
					command,
					(messageRow, transportRow) => (messageRow, transportRow),
					splitOn: "TransportId").ConfigureAwait(false);

			foreach (var (messageRow, transportRow) in rows)
			{
				var message = MapRowToMessage(messageRow);
				var transport = MapRowToTransport(transportRow);
				results.Add((message, transport));
			}

			return results;
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_pending_transports", result, stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Stages a message with multiple transport delivery records atomically.
	/// </summary>
	/// <param name="message"> The outbound message to stage. </param>
	/// <param name="transports"> The transport delivery records to create. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	public async Task StageMessageWithTransportsAsync(
		OutboundMessage message,
		IEnumerable<OutboundMessageTransport> transports,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transports);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var transportsList = transports.ToList();
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await InsertMessageAsync(connection, transaction, message, cancellationToken).ConfigureAwait(false);

			foreach (var transport in transportsList)
			{
				await InsertTransportDeliveryAsync(connection, transaction, transport, message.TenantId, cancellationToken).ConfigureAwait(false);
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Staged message {MessageId} with {TransportCount} transports", message.Id, transportsList.Count);
		}
		catch (Exception ex) when (TryGetDuplicateKeyViolation(ex, out var duplicateEx))
		{
			result = WriteStoreTelemetry.Results.Conflict;
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogWarning(duplicateEx, "Duplicate outbox message detected for {MessageId}", message.Id);
			throw new InvalidOperationException($"Outbox message '{message.Id}' already exists.", ex);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}
		finally
		{
			RecordOperation("stage_with_transports", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Stages a message within an externally owned transaction. The caller is responsible
	/// for committing or rolling back the transaction. This enables atomic consistency
	/// between event store appends and outbox writes in event sourcing scenarios.
	/// </remarks>
	public async ValueTask StageMessageAsync(
		OutboundMessage message,
		System.Data.IDbTransaction transaction,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transaction);

		if (transaction is not SqlTransaction sqlTransaction)
		{
			throw new ArgumentException(
				$"Expected SqlTransaction but received {transaction.GetType().Name}. " +
				"The SQL Server outbox store requires a SqlTransaction for transactional staging.",
				nameof(transaction));
		}

		var connection = sqlTransaction.Connection
			?? throw new InvalidOperationException("The transaction's connection is null or has been disposed.");

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			await InsertMessageAsync(connection, sqlTransaction, message, cancellationToken).ConfigureAwait(false);

			if (message.IsMultiTransport && message.TransportDeliveries.Count > 0)
			{
				foreach (var delivery in message.TransportDeliveries)
				{
					await InsertTransportDeliveryAsync(connection, sqlTransaction, delivery, message.TenantId, cancellationToken).ConfigureAwait(false);
				}
			}

			_logger.LogDebug("Staged outbox message {MessageId} within external transaction", message.Id);
		}
		catch (Exception ex) when (TryGetDuplicateKeyViolation(ex, out var duplicateEx))
		{
			result = WriteStoreTelemetry.Results.Conflict;
			_logger.LogWarning(duplicateEx, "Duplicate outbox message detected for {MessageId}", message.Id);
			throw new InvalidOperationException($"Outbox message '{message.Id}' already exists.", ex);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			_logger.LogError(ex, "Failed to stage outbox message {MessageId} within external transaction", message.Id);
			throw;
		}
		finally
		{
			RecordOperation("stage_transactional", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<OutboundMessageTransport>> GetTransportDeliveriesAsync(
		string messageId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return await connection.ResolveAsync(
					new Requests.GetTransportDeliveriesRequest(
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						KeyedTenantPartition.FromContext(_tenantContext),
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_transports", result, stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Marks a specific transport delivery as skipped.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="transportName"> The transport name. </param>
	/// <param name="reason"> Optional reason for skipping. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	public async Task MarkTransportSkippedAsync(
		string messageId,
		string transportName,
		string? reason,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ResolveAsync(
					new Requests.MarkTransportSkippedRequest(
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						transportName,
						reason,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			// Update aggregate status
			_ = await connection.ResolveAsync(
					new Requests.UpdateAggregateStatusRequest(
						_options.Tables.QualifiedOutboxTableName,
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Marked transport {TransportName} as skipped for message {MessageId}: {Reason}",
				transportName, messageId, reason ?? "No reason provided");
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("mark_transport_skipped", result, stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Gets failed transport deliveries that are eligible for retry.
	/// </summary>
	/// <param name="transportName"> The transport name to query. </param>
	/// <param name="maxRetries"> Maximum number of retry attempts to consider. </param>
	/// <param name="olderThan"> Only return deliveries that failed before this timestamp. </param>
	/// <param name="batchSize"> Maximum number of deliveries to retrieve. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of failed transport deliveries eligible for retry. </returns>
	public async Task<IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)>> GetFailedTransportDeliveriesAsync(
		string transportName,
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		var sql = $"""
		           SELECT TOP (@BatchSize)
		           	m.Id, m.MessageType, m.Payload, m.Headers, m.Destination, m.CreatedAt, m.ScheduledAt, m.SentAt,
		           	m.Status, m.RetryCount, m.LastError, m.LastAttemptAt, m.CorrelationId, m.CausationId,
		           	m.TenantId, m.Priority, m.TargetTransports, m.IsMultiTransport,
		           	m.PartitionKey, m.GroupKey, m.SequenceNumber,
		           	t.Id AS TransportId, t.MessageId, t.TransportName, t.Destination AS TransportDestination,
		           	t.Status AS TransportStatus, t.CreatedAt AS TransportCreatedAt, t.AttemptedAt, t.SentAt AS TransportSentAt,
		           	t.RetryCount AS TransportRetryCount, t.LastError AS TransportLastError, t.TransportMetadata
		           FROM {_options.Tables.QualifiedOutboxTableName} m
		           INNER JOIN {_options.Tables.QualifiedTransportsTableName} t ON m.Id = t.MessageId
		           WHERE t.TransportName = @TransportName
		           	AND t.Status = 3 -- Failed
		           	AND t.RetryCount < @MaxRetries
		           	AND (@OlderThan IS NULL OR t.AttemptedAt < @OlderThan)
		           ORDER BY t.RetryCount ASC, t.AttemptedAt ASC
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { TransportName = transportName, MaxRetries = maxRetries, OlderThan = olderThan, BatchSize = batchSize },
			commandTimeout: _options.Processing.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			var results = new List<(OutboundMessage, OutboundMessageTransport)>();

			var rows = await connection
				.QueryAsync<Requests.OutboxMessageRow, TransportDeliveryRow, (Requests.OutboxMessageRow, TransportDeliveryRow)>(
					command,
					(messageRow, transportRow) => (messageRow, transportRow),
					splitOn: "TransportId").ConfigureAwait(false);

			foreach (var (messageRow, transportRow) in rows)
			{
				var message = MapRowToMessage(messageRow);
				var transport = MapRowToTransport(transportRow);
				results.Add((message, transport));
			}

			return results;
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_failed_transports", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task UpdateAggregateStatusAsync(
		string messageId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ResolveAsync(
					new Requests.UpdateAggregateStatusRequest(
						_options.Tables.QualifiedOutboxTableName,
						_options.Tables.QualifiedTransportsTableName,
						messageId,
						_options.Processing.CommandTimeoutSeconds,
						cancellationToken))
				.ConfigureAwait(false);

			_logger.LogDebug("Updated aggregate status for message {MessageId}", messageId);
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("update_aggregate_status", result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<TransportDeliveryStatistics> GetTransportStatisticsAsync(
		string? transportName,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		var sql = transportName == null
			? $"""
			   SELECT
			   	SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS PendingCount,
			   	SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS SentCount,
			   	SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS FailedCount,
			   	SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END) AS SkippedCount,
			   	MIN(CASE WHEN Status = 0 THEN CreatedAt END) AS OldestPendingCreatedAt
			   FROM {_options.Tables.QualifiedTransportsTableName}
			   """
			: $"""
			   SELECT
			   	SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS PendingCount,
			   	SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS SentCount,
			   	SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS FailedCount,
			   	SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END) AS SkippedCount,
			   	MIN(CASE WHEN Status = 0 THEN CreatedAt END) AS OldestPendingCreatedAt
			   FROM {_options.Tables.QualifiedTransportsTableName}
			   WHERE TransportName = @TransportName
			   """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { TransportName = transportName },
			commandTimeout: _options.Processing.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			var row = await connection.QuerySingleOrDefaultAsync<TransportStatisticsRow>(command).ConfigureAwait(false);

			return new TransportDeliveryStatistics
			{
				PendingCount = row?.PendingCount ?? 0,
				SentCount = row?.SentCount ?? 0,
				FailedCount = row?.FailedCount ?? 0,
				SkippedCount = row?.SkippedCount ?? 0,
				OldestPendingAge = row?.OldestPendingCreatedAt != null
					? DateTimeOffset.UtcNow - row.OldestPendingCreatedAt
					: null,
				TransportName = transportName
			};
		}
		catch
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			RecordOperation("get_transport_stats", result, stopwatch.Elapsed);
		}
	}

	#endregion Per-Transport Methods

	#region Private Methods

	/// <summary>
	/// Checks if a byte value is a valid serializer ID (1-254).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Legacy detection heuristic: JSON payloads typically start with '{' (0x7B) or '[' (0x5B), which are in the valid range. However, IDs
	/// 1-4 are reserved for built-in serializers, and IDs 5-199 are framework reserved. Custom IDs are 200-254.
	/// </para>
	/// <para>
	/// Since legacy JSON payloads start with 0x7B (123) or 0x5B (91), and these fall within the framework reserved range (5-199), we need
	/// to check if the ID is actually registered. For simplicity, we only consider bytes 1-4 as definite magic bytes (built-in serializers).
	/// </para>
	/// </remarks>
	private static bool IsValidSerializerId(byte id)
	{
		// Built-in serializer IDs: 1=MemoryPack, 2=SystemTextJson, 3=MessagePack, 4=Protobuf For legacy detection, we only trust the first
		// 4 IDs as definite magic bytes. JSON typically starts with '{' (0x7B=123) or '[' (0x5B=91), which would fall in the framework
		// reserved range and be misidentified without this check.
		return id is (>= 1 and <= 4) or (>= 200 and <= 254);
	}

	private static OutboundMessageTransport MapRowToTransport(TransportDeliveryRow row)
	{
		return new OutboundMessageTransport
		{
			Id = row.Id ?? row.TransportId ?? string.Empty,
			MessageId = row.MessageId,
			TransportName = row.TransportName,
			Destination = row.Destination ?? row.TransportDestination,
			Status = (TransportDeliveryStatus)(row.Status ?? row.TransportStatus ?? 0),
			CreatedAt = row.CreatedAt ?? row.TransportCreatedAt ?? DateTimeOffset.UtcNow,
			AttemptedAt = row.AttemptedAt,
			SentAt = row.SentAt ?? row.TransportSentAt,
			RetryCount = row.RetryCount ?? row.TransportRetryCount ?? 0,
			LastError = row.LastError ?? row.TransportLastError,
			TransportMetadata = row.TransportMetadata
		};
	}

	/// <summary>
	/// Serializes a message payload using the configured serializer or fallback to System.Text.Json.
	/// </summary>
	/// <typeparam name="T"> The message type. </typeparam>
	/// <param name="message"> The message to serialize. </param>
	/// <returns> The serialized payload bytes with magic byte header (if using IPayloadSerializer). </returns>
	private byte[] SerializePayload<T>(T message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (_payloadSerializer != null)
		{
			// Use the actual runtime type for serialization to support binary serializers (MemoryPack, MessagePack) which require concrete
			// types, not interfaces
			var runtimeType = message.GetType();
			return _payloadSerializer.SerializeObject(message, runtimeType);
		}

		// Fallback to System.Text.Json for backward compatibility
#pragma warning disable IL2026, IL3050 // JsonSerializer with Type parameter requires unreferenced code
		return JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), _jsonOptions);
#pragma warning restore IL2026, IL3050
	}

	/// <summary>
	/// Deserializes a message payload using the configured serializer with legacy detection.
	/// </summary>
	/// <typeparam name="T"> The target message type. </typeparam>
	/// <param name="payload"> The serialized payload bytes. </param>
	/// <returns> The deserialized message. </returns>
	/// <remarks>
	/// <para>
	/// This method supports both new payloads with magic byte headers and legacy payloads without magic bytes. Legacy detection works as follows:
	/// </para>
	/// <list type="bullet">
	/// <item> If the first byte is a valid serializer ID (1-254), use IPayloadSerializer </item>
	/// <item> Otherwise, assume System.Text.Json legacy format (no magic byte) </item>
	/// </list>
	/// </remarks>
	private T DeserializePayload<T>(byte[] payload)
	{
		ArgumentNullException.ThrowIfNull(payload);

		if (payload.Length == 0)
		{
			throw new InvalidOperationException("Cannot deserialize empty payload.");
		}

		if (_payloadSerializer != null)
		{
			// Check if payload has a valid magic byte
			var firstByte = payload[0];
			if (IsValidSerializerId(firstByte))
			{
				return _payloadSerializer.Deserialize<T>(payload);
			}

			// Legacy detection: No valid magic byte, assume System.Text.Json
			_logger.LogDebug(
				"Detected legacy payload without magic byte (first byte: 0x{FirstByte:X2}). " +
				"Using System.Text.Json fallback.",
				firstByte);
		}

		// Fallback to System.Text.Json for legacy payloads
#pragma warning disable IL2026, IL3050 // JsonSerializer with generic type requires unreferenced code
		return JsonSerializer.Deserialize<T>(payload, _jsonOptions)
			   ?? throw new InvalidOperationException($"Deserialization returned null for type {typeof(T).Name}.");
#pragma warning restore IL2026, IL3050
	}

	/// <summary>
	/// Determines whether <paramref name="exception"/> represents a SQL Server unique-constraint /
	/// duplicate-key violation (error <c>2627</c> or <c>2601</c>), whether it surfaces directly as a
	/// <see cref="SqlException"/> or is wrapped by the data-request layer — <c>ResolveAsync</c> rethrows
	/// the underlying <see cref="SqlException"/> as the inner exception of an
	/// <c>OperationFailedException</c>. This lets every stage entry point honor the cross-provider
	/// contract of throwing <see cref="InvalidOperationException"/> on a duplicate stage, matching the
	/// in-memory, MongoDB, and PostgreSQL stores.
	/// </summary>
	private static bool TryGetDuplicateKeyViolation(Exception exception, [NotNullWhen(true)] out SqlException? sqlException)
	{
		sqlException = exception as SqlException ?? exception.InnerException as SqlException;
		if (sqlException is { Number: 2627 or 2601 })
		{
			return true;
		}

		sqlException = null;
		return false;
	}

	private async Task InsertMessageAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		OutboundMessage message,
		CancellationToken cancellationToken)
	{
		_ = await connection.ResolveAsync(
				new Requests.InsertOutboxMessageRequest(
					_options.Tables.QualifiedOutboxTableName,
					message,
					transaction,
					_options.Processing.CommandTimeoutSeconds,
					cancellationToken))
			.ConfigureAwait(false);
	}

	private async Task InsertTransportDeliveryAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		OutboundMessageTransport delivery,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		_ = await connection.ResolveAsync(
				new Requests.InsertTransportDeliveryRequest(
					_options.Tables.QualifiedTransportsTableName,
					delivery,
					tenantId,
					transaction,
					_options.Processing.CommandTimeoutSeconds,
					cancellationToken))
			.ConfigureAwait(false);
	}

	private async Task<IEnumerable<OutboundMessageTransport>> GetTransportDeliveriesInternalAsync(
		SqlConnection connection,
		string messageId,
		CancellationToken cancellationToken)
	{
		return await connection.ResolveAsync(
				new Requests.GetTransportDeliveriesRequest(
					_options.Tables.QualifiedTransportsTableName,
					messageId,
					KeyedTenantPartition.FromContext(_tenantContext),
					_options.Processing.CommandTimeoutSeconds,
					cancellationToken))
			.ConfigureAwait(false);
	}

	private OutboundMessage MapRowToMessage(Requests.OutboxMessageRow row)
	{
		var message = new OutboundMessage
		{
			Id = row.Id,
			MessageType = row.MessageType,
			Payload = row.Payload,
#pragma warning disable IL2026, IL3050
			Headers = string.IsNullOrEmpty(row.Headers)
				? new Dictionary<string, object>(StringComparer.Ordinal)
				: JsonSerializer.Deserialize<Dictionary<string, object>>(row.Headers, _jsonOptions)
				  ?? new Dictionary<string, object>(StringComparer.Ordinal),
#pragma warning restore IL2026, IL3050
			Destination = row.Destination,
			CreatedAt = row.CreatedAt,
			ScheduledAt = row.ScheduledAt,
			SentAt = row.SentAt,
			Status = (OutboxStatus)row.Status,
			RetryCount = row.RetryCount,
			LastError = row.LastError,
			LastAttemptAt = row.LastAttemptAt,
			CorrelationId = row.CorrelationId,
			CausationId = row.CausationId,
			TenantId = row.TenantId,
			Priority = row.Priority,
			TargetTransports = row.TargetTransports,
			IsMultiTransport = row.IsMultiTransport,
			PartitionKey = row.PartitionKey,
			GroupKey = row.GroupKey,
			SequenceNumber = row.SequenceNumber
		};

		return message;
	}

	#endregion Private Methods

	#region Row Types

	private sealed class TransportDeliveryRow
	{
		// Direct query columns
		public string? Id { get; set; }

		public string MessageId { get; set; } = string.Empty;
		public string TransportName { get; set; } = string.Empty;
		public string? Destination { get; set; }
		public int? Status { get; set; }
		public DateTimeOffset? CreatedAt { get; set; }
		public DateTimeOffset? AttemptedAt { get; set; }
		public DateTimeOffset? SentAt { get; set; }
		public int? RetryCount { get; set; }
		public string? LastError { get; set; }
		public string? TransportMetadata { get; set; }

		// Aliased columns from join query
		public string? TransportId { get; set; }

		public string? TransportDestination { get; set; }
		public int? TransportStatus { get; set; }
		public DateTimeOffset? TransportCreatedAt { get; set; }
		public DateTimeOffset? TransportSentAt { get; set; }
		public int? TransportRetryCount { get; set; }
		public string? TransportLastError { get; set; }
	}

	private sealed class TransportStatisticsRow
	{
		public int PendingCount { get; set; }
		public int SentCount { get; set; }
		public int FailedCount { get; set; }
		public int SkippedCount { get; set; }
		public DateTimeOffset? OldestPendingCreatedAt { get; set; }
	}

	#endregion Row Types
}
