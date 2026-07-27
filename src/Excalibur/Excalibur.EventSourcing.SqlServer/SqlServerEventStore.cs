// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Data;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.EventSourcing.Observability;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic event appends with optimistic concurrency control.
/// Uses database transactions to ensure consistency.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Connection string for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// <para>
/// Supports pluggable serialization via <see cref="IPayloadSerializer"/> for event payloads,
/// with backward compatibility for existing JSON-serialized events.
/// </para>
/// </remarks>
public sealed class SqlServerEventStore : IEventStore, IEventStoreErasure, ITransactionalEventStore, IEventStoreArchive
{
	// Format markers for envelope detection (ADR-058)
	private const byte EnvelopeFormatMarker = 0x01;

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ISerializer? _internalSerializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly string _schema;
	private readonly string _table;
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Clock used to evaluate age-based archive policy. Injectable so a conformance test can drive
	/// <see cref="ArchivePolicy.MaxAge"/> deterministically instead of racing wall-clock time; internal
	/// because the production path always uses the system clock.
	/// </summary>
	internal TimeProvider TimeProvider { get; init; } = TimeProvider.System;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, ISerializer, IPayloadSerializer, string, string, ITenantContext)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(string connectionString, ILogger<SqlServerEventStore> logger)
		: this(CreateConnectionFactory(connectionString), logger, internalSerializer: null, payloadSerializer: null, schema: "dbo", table: "EventStoreEvents")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStore"/> class with optional internal serializer.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="internalSerializer">Optional internal serializer for high-performance binary envelope serialization.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, ISerializer, IPayloadSerializer, string, string, ITenantContext)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(
		string connectionString,
		ILogger<SqlServerEventStore> logger,
		ISerializer? internalSerializer)
		: this(CreateConnectionFactory(connectionString), logger, internalSerializer, payloadSerializer: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStore"/> class with optional serializers.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="internalSerializer">Optional internal serializer for high-performance binary envelope serialization.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, ISerializer, IPayloadSerializer, string, string, ITenantContext)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(
		string connectionString,
		ILogger<SqlServerEventStore> logger,
		ISerializer? internalSerializer,
		IPayloadSerializer? payloadSerializer)
		: this(CreateConnectionFactory(connectionString), logger, internalSerializer, payloadSerializer)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="internalSerializer">Optional internal serializer for high-performance binary envelope serialization.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	/// <param name="tenantContext">
	/// Optional ambient tenant context. When supplied and a tenant is resolved, every query is scoped to the
	/// current tenant (row-level <c>TenantId</c> discriminator) in the same atomic statement. When
	/// <see langword="null"/> (the default, non-multi-tenant path) no tenant scoping is applied and behavior
	/// is unchanged. Fail-closed enforcement (throwing when a tenant is required but absent) is provided by
	/// the tenant-scoping store decorator registered by the multi-tenancy composition, not by this base store.
	/// </param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Multi-database setups with marker interfaces (e.g., IDomainDb, IEventStoreDb)</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with <see cref="IDb"/> abstraction</description></item>
	/// </list>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// new SqlServerEventStore(
	///     () => (SqlConnection)domainDb.Connection,
	///     logger,
	///     internalSerializer,
	///     payloadSerializer);
	/// </code>
	/// </para>
	/// </remarks>
	public SqlServerEventStore(
		Func<SqlConnection> connectionFactory,
		ILogger<SqlServerEventStore> logger,
		ISerializer? internalSerializer = null,
		IPayloadSerializer? payloadSerializer = null,
		string schema = "dbo",
		string table = "EventStoreEvents",
		ITenantContext? tenantContext = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_jsonOptions = Excalibur.Dispatch.EventSerializationDefaults.CreateCanonicalOptions();
		_internalSerializer = internalSerializer;
		_payloadSerializer = payloadSerializer;
		_schema = schema;
		_table = table;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		return await LoadAsync(aggregateId, aggregateType, -1, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var loadedEvents = await connection.ResolveAsync(
					new LoadEventsRequest(aggregateId, aggregateType, fromVersion, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, loadedEvents.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return loadedEvents;
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.SqlServer,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(events);
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		// Performance optimization: AD-250-1 - avoid ToList() when possible
		// If already a collection with Count, use directly; otherwise materialize once
		var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();

		if (eventList.Count == 0)
		{
			RecordAppendTelemetry(result, stopwatch.Elapsed);
			return AppendResult.CreateSuccess(expectedVersion, firstEventPosition: null);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		try
		{
			var appendResult = await ExecuteAppendTransactionAsync(
					aggregateId, aggregateType, eventList, expectedVersion, activity, cancellationToken)
				.ConfigureAwait(false);

			if (appendResult.IsConcurrencyConflict)
			{
				result = WriteStoreTelemetry.Results.Conflict;
			}

			return appendResult;
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			LogAppendFailure(ex, aggregateId, aggregateType, eventList);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			return AppendResult.CreateFailure(GetFullExceptionMessage(ex));
		}
		finally
		{
			RecordAppendTelemetry(result, stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<AppendResult> AppendWithOutboxStagingAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stageOutbox);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();

		if (eventList.Count == 0)
		{
			// No events means no integration messages to stage; nothing to do atomically.
			RecordAppendTelemetry(result, stopwatch.Elapsed);
			return AppendResult.CreateSuccess(expectedVersion, firstEventPosition: null);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		try
		{
			var appendResult = await ExecuteAppendWithOutboxTransactionAsync(
					aggregateId, aggregateType, eventList, expectedVersion, stageOutbox, activity, cancellationToken)
				.ConfigureAwait(false);

			if (appendResult.IsConcurrencyConflict)
			{
				result = WriteStoreTelemetry.Results.Conflict;
			}

			return appendResult;
		}
		catch (Exception ex)
		{
			// Unlike the plain append (which returns a failure result), the transactional path surfaces
			// the real failure to the caller so the repository can propagate it. The transaction has
			// already rolled back atomically — neither events nor outbox rows persist.
			result = WriteStoreTelemetry.Results.Failure;
			LogAppendFailure(ex, aggregateId, aggregateType, eventList);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			RecordAppendTelemetry(result, stopwatch.Elapsed);
		}
	}

	private async ValueTask<AppendResult> ExecuteAppendWithOutboxTransactionAsync(
		string aggregateId,
		string aggregateType,
		IReadOnlyCollection<IDomainEvent> eventList,
		long expectedVersion,
		Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
		System.Diagnostics.Activity? activity,
		CancellationToken cancellationToken)
	{
		// The store owns ONE connection and ONE transaction for the whole unit of work. The append and
		// the outbox staging both run on this same SqlConnection/SqlTransaction, so a two-connection or
		// two-transaction split (the atomicity bug this seam closes) is structurally impossible.
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// READ COMMITTED, deliberately. The UNIQUE constraint is the concurrency control.
		//
		// This was Serializable, which bought range locks to prevent a phantom that
		// UQ_EventStoreEvents_Stream (AggregateId, AggregateType, Version, TenantId) already makes
		// UNWRITABLE -- and charged deadlocks for it. Measured on an empty table: of ten concurrent appends
		// to ten DISTINCT aggregates, eight were chosen as deadlock victims, because with no row yet for any
		// of them every transaction range-locked the same key gap and then needed to upgrade it. A fresh
		// deployment is exactly that empty-table case.
		//
		// Serializable was not carrying anything else. Outbox atomicity comes from transaction SCOPE, not
		// isolation level. Global Position ordering was never protected by it: Position is IDENTITY, and
		// IDENTITY values are allocated outside transaction scope with no guarantee of committing in
		// allocation order, which is why the tailing consumers use watermarks rather than trusting
		// monotonicity. Tenant isolation is carried by the predicate and by TenantId being in the unique key.
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
				IsolationLevel.ReadCommitted, cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// Optimistic concurrency check. This read is ADVISORY: at READ COMMITTED a concurrent writer can
			// claim the same version between this SELECT and the INSERT below. That is expected, not a hole
			// -- the INSERT then violates the unique constraint and is translated to a conflict below.
			var currentVersion = await connection.ResolveAsync(
					new GetCurrentVersionRequest(aggregateId, aggregateType, transaction, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			if (currentVersion != expectedVersion)
			{
				// Roll back immediately. Do NOT invoke stageOutbox on a conflict — nothing must be staged
				// when the append is rejected (EC-K.2).
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			var (version, firstPosition) = await InsertEventsAsync(
					connection, transaction, aggregateId, aggregateType, eventList, currentVersion, cancellationToken)
				.ConfigureAwait(false);

			// Stage outbox messages on the SAME connection + SAME transaction. A throw here rolls the
			// whole unit of work back (events included) via the catch below.
			await stageOutbox(transaction, cancellationToken).ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Appended {Count} events and staged outbox for {AggregateType}/{AggregateId} at version {Version}",
				eventList.Count, aggregateType, aggregateId, version);

			_ = (activity?.SetTag(EventSourcingTags.Version, version));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return AppendResult.CreateSuccess(version, firstPosition);
		}
		catch (SqlException ex) when (IsStreamUniqueViolation(ex))
		{
			// This IS the concurrency control firing, so it is a conflict result and not an error.
			//
			// A concurrent writer claimed this (aggregate, version, tenant) between the advisory read above
			// and this INSERT. The unique constraint refused the duplicate, which is the guarantee working
			// exactly as intended -- the second writer is told it lost, and nothing was written or staged.
			await RollbackQuietlyAsync(transaction).ConfigureAwait(false);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);

			return AppendResult.CreateConcurrencyConflict(
				expectedVersion,
				await ReadCurrentVersionAfterConflictAsync(
					connection, aggregateId, aggregateType, expectedVersion, cancellationToken).ConfigureAwait(false));
		}
		catch
		{
			await RollbackQuietlyAsync(transaction).ConfigureAwait(false);

			throw;
		}
	}

	/// <summary>
	/// Determines whether the exception is the stream unique-constraint violation used for optimistic
	/// concurrency.
	/// </summary>
	/// <param name="ex"> The SQL exception to classify. </param>
	/// <returns> <see langword="true"/> when the error is a unique-key violation. </returns>
	/// <remarks>
	/// 2627 is a unique CONSTRAINT violation and 2601 a unique INDEX violation; the same logical collision
	/// is reported under either number depending on how the uniqueness was declared, so both are treated as
	/// a concurrency conflict.
	/// </remarks>
	private static bool IsStreamUniqueViolation(SqlException ex) =>
		ex.Number is 2627 or 2601;

	/// <summary>Rolls back without letting a rollback failure mask the original fault.</summary>
	/// <param name="transaction"> The transaction to roll back. </param>
	private static async Task RollbackQuietlyAsync(SqlTransaction transaction)
	{
		try
		{
			// Uncancellable so cleanup completes even when the failure was a cancellation; the transaction
			// must not be left to a deferred dispose.
			await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch
		{
			// A rollback failure must not mask the original exception.
		}
	}

	/// <summary>
	/// Re-reads the persisted version after a constraint conflict so the caller is told what it lost to.
	/// </summary>
	/// <param name="connection"> The open connection, whose transaction has already been rolled back. </param>
	/// <param name="aggregateId"> The aggregate whose stream conflicted. </param>
	/// <param name="aggregateType"> The aggregate type whose stream conflicted. </param>
	/// <param name="expectedVersion"> The version this caller attempted to write against. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The current persisted version, or <paramref name="expectedVersion"/> if it cannot be read. </returns>
	/// <remarks>
	/// Runs outside a transaction (the conflicting one is rolled back) and only on the conflict path, so it
	/// costs a round trip precisely when the caller has to reload anyway. Reporting the winner's version
	/// rather than echoing the expected one back is what makes the conflict actionable; if the re-read
	/// itself fails, the conflict is still reported rather than converted into a hard error.
	/// </remarks>
	private async Task<long> ReadCurrentVersionAfterConflictAsync(
		SqlConnection connection,
		string aggregateId,
		string aggregateType,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		try
		{
			return await connection.ResolveAsync(
					new GetCurrentVersionRequest(
						aggregateId,
						aggregateType,
						transaction: null,
						TenantScope.FromContext(_tenantContext),
						cancellationToken,
						_schema,
						_table))
				.ConfigureAwait(false);
		}
		catch (SqlException)
		{
			return expectedVersion;
		}
	}

	private async ValueTask<AppendResult> ExecuteAppendTransactionAsync(
		string aggregateId,
		string aggregateType,
		IReadOnlyCollection<IDomainEvent> eventList,
		long expectedVersion,
		System.Diagnostics.Activity? activity,
		CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// READ COMMITTED for the same reason as the outbox-staging path above: the unique constraint on
		// (AggregateId, AggregateType, Version, TenantId) already makes a duplicate unwritable, so
		// Serializable was paying deadlocks to prevent a phantom that cannot occur.
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
				IsolationLevel.ReadCommitted, cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// Advisory read — see the sibling path. A racing writer is caught by the constraint, not here.
			var currentVersion = await connection.ResolveAsync(
					new GetCurrentVersionRequest(aggregateId, aggregateType, transaction, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			if (currentVersion != expectedVersion)
			{
				// Explicit rollback rather than waiting for DisposeAsync.
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			var (version, firstPosition) = await InsertEventsAsync(
					connection, transaction, aggregateId, aggregateType, eventList, currentVersion, cancellationToken)
				.ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Appended {Count} events to {AggregateType}/{AggregateId} at version {Version}",
				eventList.Count, aggregateType, aggregateId, version);

			_ = (activity?.SetTag(EventSourcingTags.Version, version));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return AppendResult.CreateSuccess(version, firstPosition);
		}
		catch (SqlException ex) when (IsStreamUniqueViolation(ex))
		{
			// The constraint refused a duplicate (aggregate, version, tenant): a lost race, not an error.
			await RollbackQuietlyAsync(transaction).ConfigureAwait(false);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);

			return AppendResult.CreateConcurrencyConflict(
				expectedVersion,
				await ReadCurrentVersionAfterConflictAsync(
					connection, aggregateId, aggregateType, expectedVersion, cancellationToken).ConfigureAwait(false));
		}
		catch
		{
			await RollbackQuietlyAsync(transaction).ConfigureAwait(false);

			throw;
		}
	}

	private async ValueTask<(long Version, long FirstPosition)> InsertEventsAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string aggregateId,
		string aggregateType,
		IReadOnlyCollection<IDomainEvent> eventList,
		long currentVersion,
		CancellationToken cancellationToken)
	{
		var version = currentVersion;

		// Build all event rows up front (assigning sequential versions), then insert them with one
		// multi-row INSERT ... OUTPUT per chunk inside the caller's transaction — replacing the former
		// per-event round-trip loop. The whole append remains atomic (single transaction), now with far
		// fewer round-trips.
		var rows = new List<EventInsertRow>(eventList.Count);
		foreach (var @event in eventList)
		{
			version++;
			var eventData = SerializeEventWithEnvelopeSupport(@event, aggregateId, aggregateType, version);
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			var metadata = @event.Metadata != null ? SerializeMetadata(@event.Metadata) : null;
#pragma warning restore IL2026, IL3050
			var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

			rows.Add(new EventInsertRow(
				@event.EventId,
				aggregateId,
				aggregateType,
				eventTypeName,
				eventData,
				metadata,
				version,
				@event.OccurredAt));
		}

		// The position of the lowest-version event in this append is the append's first position. OUTPUT
		// row order is not guaranteed, so positions are matched to events by version, not by row index.
		// Use a nullable sentinel (0n4m8q): a magic 0 default is ambiguous when the Position IDENTITY column
		// is seeded at 0, since a legitimately-returned position of 0 would be indistinguishable from
		// "not yet found". null means "no OUTPUT row matched the first version" — a real invariant breach.
		var firstVersion = currentVersion + 1;
		long? firstPosition = null;

		for (var offset = 0; offset < rows.Count; offset += InsertEventsBatchRequest.MaxEventsPerStatement)
		{
			var count = Math.Min(InsertEventsBatchRequest.MaxEventsPerStatement, rows.Count - offset);
			var chunk = rows.GetRange(offset, count);

			var inserted = await connection.ResolveAsync(
					new InsertEventsBatchRequest(chunk, transaction, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			foreach (var row in inserted)
			{
				if (row.Version == firstVersion)
				{
					firstPosition = row.Position;
				}
			}
		}

		if (firstPosition is null)
		{
			throw new InvalidOperationException(
				$"Event store append inserted {rows.Count} event(s) but the INSERT ... OUTPUT returned no position " +
				$"for the first event (version {firstVersion}); the append cannot report a valid first position.");
		}

		return (version, firstPosition.Value);
	}

	private static void RecordAppendTelemetry(string result, TimeSpan elapsed)
	{
		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.EventStore,
			WriteStoreTelemetry.Providers.SqlServer,
			"append",
			result,
			elapsed);
	}

	private void LogAppendFailure(
		Exception ex,
		string aggregateId,
		string aggregateType,
		IReadOnlyCollection<IDomainEvent> eventList)
	{
		var correlationId = ExtractCorrelationId(eventList);
		var messageId = ExtractEventId(eventList);

		using var scope = WriteStoreTelemetry.BeginLogScope(
			_logger,
			WriteStoreTelemetry.Stores.EventStore,
			WriteStoreTelemetry.Providers.SqlServer,
			"append",
			messageId,
			correlationId);
		_logger.LogError(ex, "Failed to append events to {AggregateType}/{AggregateId}", aggregateType, aggregateId);
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return () => new SqlConnection(connectionString);
	}

	/// <summary>
	/// Gets the full exception message chain for better error diagnostics.
	/// </summary>
	private static string GetFullExceptionMessage(Exception ex)
	{
		// Performance optimization: AD-250-1 - use StringBuilder to avoid List allocation
		// Most exception chains are short (1-3 levels), so this is efficient
		var current = ex;
		if (current.InnerException == null)
		{
			return current.Message;
		}

		var sb = new System.Text.StringBuilder(current.Message);
		current = current.InnerException;
		while (current != null)
		{
			_ = sb.Append(" -> ");
			_ = sb.Append(current.Message);
			current = current.InnerException;
		}

		return sb.ToString();
	}

	private static string? ExtractCorrelationId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (@event.Metadata == null)
			{
				continue;
			}

			if (@event.Metadata.TryGetValue("CorrelationId", out var correlationId) ||
				@event.Metadata.TryGetValue("correlationId", out correlationId))
			{
				return correlationId?.ToString();
			}
		}

		return null;
	}

	private static string? ExtractEventId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (!string.IsNullOrWhiteSpace(@event.EventId))
			{
				return @event.EventId;
			}
		}

		return null;
	}

	/// <summary>
	/// Serializes a domain event using the configured serializer.
	/// Uses <see cref="IPayloadSerializer"/> when available,
	/// otherwise falls back to System.Text.Json.
	/// </summary>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeEvent(IDomainEvent @event)
	{
		if (_payloadSerializer != null)
		{
			return _payloadSerializer.Serialize(@event);
		}

		// Fallback to System.Text.Json for backward compatibility
		return JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);

	/// <summary>
	/// Serializes an event with envelope support if internal serializer is available.
	/// Falls back to JSON serialization if serializer is not configured.
	/// </summary>
	private byte[] SerializeEventWithEnvelopeSupport(
		IDomainEvent @event,
		string aggregateId,
		string aggregateType,
		long version)
	{
		var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

		if (_internalSerializer is null)
		{
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			return SerializeEvent(@event);
#pragma warning restore IL2026, IL3050
		}

		// Create envelope with event data
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
		var eventBytes = SerializeEvent(@event);
#pragma warning restore IL2026, IL3050

		var envelope = new EventEnvelope
		{
			EventId = Guid.TryParse(@event.EventId, out var guid) ? guid : Guid.NewGuid(),
			AggregateId = Guid.TryParse(aggregateId, out var aggGuid) ? aggGuid : Guid.NewGuid(),
			AggregateType = aggregateType,
			EventType = eventTypeName,
			Version = version,
			Payload = eventBytes,
			OccurredAt = @event.OccurredAt,
			Metadata = @event.Metadata?.ToDictionary(
				kvp => kvp.Key,
				kvp => kvp.Value?.ToString() ?? string.Empty,
				StringComparer.OrdinalIgnoreCase),
			SchemaVersion = 1,
		};

		var envelopeData = _internalSerializer.SerializeToBytes(envelope);

		// Prepend format marker
		var result = new byte[envelopeData.Length + 1];
		result[0] = EnvelopeFormatMarker;
		envelopeData.CopyTo(result, 1);
		return result;
	}

	/// <inheritdoc/>
	public async Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
			new Requests.EraseEventsRequest(aggregateId, aggregateType, erasureRequestId, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
			new Requests.IsErasedRequest(aggregateId, aggregateType, TenantScope.FromContext(_tenantContext), cancellationToken, _schema, _table))
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Discovery is intentionally cross-tenant: the archive service makes one pass over every tenant, so a
	/// tenant-scoped enumeration would stall archival for all but one. The tenant is projected onto each
	/// candidate instead, and the destructive leg below consumes it explicitly.
	/// </remarks>
	public async Task<IReadOnlyList<ArchiveCandidate>> GetArchiveCandidatesAsync(
		ArchivePolicy policy,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(policy);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
			new Requests.GetArchiveCandidatesRequest(
				policy, batchSize, TimeProvider.GetUtcNow(), cancellationToken, _schema, _table))
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// The tenant term is taken from the caller, never from ambient context. This runs under the archive
	/// service's all-tenant pass, where no ambient tenant exists; resolving one here would delete under an
	/// arbitrary term while the cold write was confirmed under another.
	/// </remarks>
	public async Task<int> DeleteEventsUpToVersionAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		long toVersion,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
			new Requests.DeleteEventsUpToVersionRequest(
				tenant, aggregateId, aggregateType, toVersion, cancellationToken, _schema, _table))
			.ConfigureAwait(false);
	}
}
