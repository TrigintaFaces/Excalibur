// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
public sealed class SqlServerEventStore : IEventStore, IEventStoreErasure, ITransactionalEventStore, IEventStoreArchive, IEventStoreVersionProbe
{
	// Format markers for envelope detection
	private const byte EnvelopeFormatMarker = 0x01;

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free serialization
	/// path. Decided once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;
	private readonly ISerializer? _internalSerializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly string _schema;
	private readonly string _table;

	/// <summary>
	/// The position counter table that orders this store's global stream. Named after the events table so
	/// that two event stores sharing a schema each get their own counter rather than silently sharing one
	/// sequence, which would interleave two unrelated streams into one position space.
	/// </summary>
	private readonly string _positionTable;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);


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
	/// <param name="tenantContext">The ambient tenant context. Required: this store resolves the tenant partition it reads and writes from here.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, ITenantContext, ISerializer, IPayloadSerializer, string, string, System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(string connectionString, ILogger<SqlServerEventStore> logger, ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString), logger, internalSerializer: null, payloadSerializer: null, schema: "dbo", table: "EventStoreEvents", tenantContext: tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStore"/> class with optional internal serializer.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">The ambient tenant context. Required: this store resolves the tenant partition it reads and writes from here.</param>
	/// <param name="internalSerializer">Optional internal serializer for high-performance binary envelope serialization.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, ITenantContext, ISerializer, IPayloadSerializer, string, string, System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(
		string connectionString,
		ILogger<SqlServerEventStore> logger,
		ITenantContext tenantContext,
		ISerializer? internalSerializer)
		: this(CreateConnectionFactory(connectionString), logger, tenantContext, internalSerializer, payloadSerializer: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStore"/> class with optional serializers.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">The ambient tenant context. Required: this store resolves the tenant partition it reads and writes from here.</param>
	/// <param name="internalSerializer">Optional internal serializer for high-performance binary envelope serialization.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, ITenantContext, ISerializer, IPayloadSerializer, string, string, System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(
		string connectionString,
		ILogger<SqlServerEventStore> logger,
		ITenantContext tenantContext,
		ISerializer? internalSerializer,
		IPayloadSerializer? payloadSerializer)
		: this(CreateConnectionFactory(connectionString), logger, tenantContext, internalSerializer, payloadSerializer)
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
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="eventTypeInfoResolver">
	/// An optional source-generated JSON type-info resolver covering the application's domain event types
	/// and the runtime types of the values it places in
	/// <see cref="Excalibur.Dispatch.IDomainEvent.Metadata"/>. Supplied, the store serializes without
	/// reflection, which is what a native-AOT host published with reflection-based serialization disabled
	/// requires. Omitted, the store serializes through the reflection-based serializer exactly as before, so
	/// an existing caller is unaffected. The stored wire format is byte-identical either way.
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
		ITenantContext tenantContext,
		ISerializer? internalSerializer = null,
		IPayloadSerializer? payloadSerializer = null,
		string schema = "dbo",
		string table = "EventStoreEvents",
		System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? eventTypeInfoResolver = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_jsonOptions = Excalibur.Dispatch.EventSerializationDefaults.CreateCanonicalOptions();
		_hasEventTypeInfoResolver = Excalibur.Dispatch.EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, eventTypeInfoResolver);
		_internalSerializer = internalSerializer;
		_payloadSerializer = payloadSerializer;
		_schema = schema;
		_table = table;
		_positionTable = table + "Position";
		ArgumentNullException.ThrowIfNull(tenantContext);
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

	/// <inheritdoc />
	/// <remarks>
	/// Reuses the same indexed <c>MAX(Version)</c> read the append path uses to resolve a concurrency
	/// conflict, which already returns <c>-1</c> for a stream with no events -- the value this capability
	/// is specified to return. Runs on its own connection, outside any append transaction.
	/// </remarks>
	public async ValueTask<long> GetMaxVersionAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return await connection.ResolveAsync(
				new GetCurrentVersionRequest(
					aggregateId, aggregateType, transaction: null, CurrentTenantScope, cancellationToken, _schema, _table))
			.ConfigureAwait(false);
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
					new LoadEventsRequest(aggregateId, aggregateType, fromVersion, CurrentTenantScope, cancellationToken, _schema, _table))
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
		// Performance optimization: - avoid ToList() when possible
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
		// NARROW BY DESIGN, and an ALLOW-LIST rather than an exclusion list. These are the two shapes a
		// store fault reaches this method in: the driver's own exception, and the data-request seam's
		// wrapper around it -- a closed set, because those are the only two layers between here and the
		// database. An exclusion list would instead enumerate what must escape, which is wrong by default
		// the first time something new appears and silently converts the newcomer into an ordinary append
		// failure. That is how a cancelled append came to be reported as a store fault and retried inside
		// a cancelled scope. Everything else -- cancellation, an event type the configured resolver does
		// not declare, a programming error -- propagates, because a returned failure means "this could
		// succeed if you try again" and none of those can.
		catch (Exception ex) when (ex is SqlException or OperationFailedException)
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
		// isolation level. Tenant isolation is carried by the predicate and by TenantId being in the unique
		// key.
		//
		// GLOBAL POSITION ORDERING IS NOT PROTECTED BY THE ISOLATION LEVEL EITHER, AND IT WAS NEVER
		// MEANT TO BE. It is protected by how the position is ALLOCATED.
		//
		// Position is taken from the EventStorePosition counter row inside this transaction, not from an
		// IDENTITY column. An IDENTITY (and every sequence) hands its number out at INSERT and lets it
		// escape the transaction, so two concurrent appends can COMMIT in the opposite order to their
		// positions -- and a tailing reader that has already passed the higher position never sees the
		// lower one. That is silent, permanent event loss for every projection, silent because nothing
		// downstream can detect it from the outside.
		//
		// The counter row's exclusive lock is released only at COMMIT and its increment rolls back with
		// the transaction, so no value is ever burned. Hence:
		//
		//   INVARIANT J: the set of committed global positions is always a contiguous prefix {1..k}.
		//
		// Allocate as LATE as possible. Every statement between the allocation and the COMMIT extends the
		// window in which all other appends are blocked on that row, and that window is the store's
		// sustained append throughput. Correctness does not depend on it being short; throughput does.
		//
		// This comment once ended "...which is why the tailing consumers use watermarks rather than
		// trusting monotonicity." No watermark existed, and the sentence is preserved here only so that a
		// reader who met the old wording elsewhere recognises it as superseded. Asserting a mitigation
		// that was never built is worse than saying nothing: it is the sentence that stops the next
		// reader from looking.
		//
		// Do not reintroduce IDENTITY, a sequence, or any allocator that yields a number before COMMIT,
		// and do not add a second counter row -- the CHECK constraint on EventStorePosition makes that
		// unrepresentable on purpose. Any of those silently restores the loss.
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
				IsolationLevel.ReadCommitted, cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// Optimistic concurrency check. This read is ADVISORY: at READ COMMITTED a concurrent writer can
			// claim the same version between this SELECT and the INSERT below. That is expected, not a hole
			// -- the INSERT then violates the unique constraint and is translated to a conflict below.
			var currentVersion = await connection.ResolveAsync(
					new GetCurrentVersionRequest(aggregateId, aggregateType, transaction, CurrentTenantScope, cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			if (currentVersion != expectedVersion)
			{
				// Roll back immediately. Do NOT invoke stageOutbox on a conflict — nothing must be staged
				// when the append is rejected (EC-K.2).
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

				// ASK WHETHER OUR OWN EVENTS LANDED, BEFORE CLASSIFYING ANYTHING.
				//
				// A moved version says somebody wrote here; it does not say WHO. A retry of an append whose
				// acknowledgement was lost arrives HERE, at the pre-check, because its own committed write is
				// what moved the version -- so the read-back further down, on the exception path, is never
				// reached on this path and cannot help. Reporting a conflict instead sends the caller to
				// reload-and-retry, which appends the same business event again at the NEXT version, where the
				// stream uniqueness key cannot catch it because the version differs.
				//
				// Sound outside the transaction: the store is append-only, so once a row carrying event id e
				// exists it exists in every later state. A stale read can only miss it, which yields the
				// conflict we would have reported anyway.
				var committedOnRetry = await ReadCommittedAppendOutcomeAsync(connection, eventList, cancellationToken)
					.ConfigureAwait(false);

				if (committedOnRetry is { CommittedCount: > 0 } retryLanded && retryLanded.LastVersion is { } retryVersion)
				{
					activity.SetOperationResult(EventSourcingTagValues.Success);
					return AppendResult.CreateSuccess(retryVersion, retryLanded.FirstPosition);
				}

				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			// Stage outbox messages on the SAME connection + SAME transaction. A throw here rolls the
			// whole unit of work back (events included) via the catch below.
			//
			// BEFORE the append, deliberately, and this ordering is load-bearing for throughput rather
			// than for correctness. Staging is one round trip PER integration event, and the append
			// allocates the global position from the counter row whose exclusive lock is then held until
			// COMMIT. Staging after the append therefore put every one of those round trips inside the
			// window in which all other appends, framework-wide, are blocked on that row -- which is
			// exactly what the allocate-as-late-as-possible note above says not to do.
			//
			// This is only legal because the callback cannot observe anything the append produces: it
			// receives the transaction and nothing else, and the position and version are assigned inside
			// InsertEventsAsync below. Keep it that way; a callback that needed either would force this
			// back inside the lock.
			//
			// Atomicity is unchanged -- same transaction, same all-or-nothing rollback. So is the rule
			// that nothing is staged when the append is rejected, because the version-conflict branch
			// above returns before reaching this line.
			await stageOutbox(transaction, cancellationToken).ConfigureAwait(false);

			var (version, firstPosition) = await InsertEventsAsync(
					connection, transaction, aggregateId, aggregateType, eventList, currentVersion, cancellationToken)
				.ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Appended {Count} events and staged outbox for {AggregateType}/{AggregateId} at version {Version}",
				eventList.Count, aggregateType, aggregateId, version);

			_ = (activity?.SetTag(EventSourcingTags.Version, version));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return AppendResult.CreateSuccess(version, firstPosition);
		}
		catch (Exception ex) when (ex is SqlException or OperationFailedException)
		{
			// SUPERSEDED COMMENT, quoted so anyone who absorbed it recognises it: "Nothing was written or
			// staged -- the transaction is rolled back before anything below runs." THAT IS FALSE when the
			// server commits and the acknowledgement is lost -- a connection reset, a command timeout, a
			// pause past the client timeout. The rollback below then does nothing, because there is
			// nothing left to roll back.
			await RollbackQuietlyAsync(transaction).ConfigureAwait(false);

			// ASK WHETHER OUR OWN EVENTS LANDED, BEFORE CLASSIFYING ANYTHING.
			//
			// The stream version cannot answer this. "Another writer took my version" and "I took it
			// myself and lost the acknowledgement" move the version identically, so a classifier reading
			// the version reports a durably committed append as a concurrency conflict -- and the
			// documented response to a conflict is reload-and-retry, which writes the same business event
			// again at the NEXT version. The stream uniqueness key cannot stop that, because the version
			// differs. The duplicate is then permanent, and every replay applies it twice.
			var committed = await ReadCommittedAppendOutcomeAsync(connection, eventList, cancellationToken)
				.ConfigureAwait(false);

			if (committed is { CommittedCount: > 0 } landed && landed.LastVersion is { } committedVersion)
			{
				_logger.LogWarning(
					"The append for {AggregateType}/{AggregateId} committed but its acknowledgement was "
					+ "lost; reporting success from the durable rows rather than a concurrency conflict.",
					aggregateType, aggregateId);

				activity.SetOperationResult(EventSourcingTagValues.Success);
				return AppendResult.CreateSuccess(committedVersion, landed.FirstPosition);
			}

			var currentVersion = await ReadCurrentVersionAfterConflictAsync(
				connection, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);

			if (IsLostRace(ex, currentVersion, expectedVersion))
			{
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);

				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion ?? expectedVersion);
			}

			throw;
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
	/// <param name="ex"> The exception to classify. </param>
	/// <returns> <see langword="true"/> when the error is a unique-key violation. </returns>
	/// <remarks>
	/// <para>
	/// 2627 is a unique CONSTRAINT violation and 2601 a unique INDEX violation; the same logical collision
	/// is reported under either number depending on how the uniqueness was declared, so both are treated as
	/// a concurrency conflict.
	/// </para>
	/// <para>
	/// The inner-exception chain is walked rather than the exception typed directly, because the insert
	/// runs through the data-request seam, which wraps whatever the driver raised in an
	/// <see cref="OperationFailedException"/>. Matching on the outermost type alone silently never
	/// matched: a lost race arrived wrapped, missed this classification, and was reported to the caller as
	/// an ordinary failure rather than the concurrency conflict it is -- so a caller's reload-and-retry
	/// policy, which keys on the conflict flag, never fired.
	/// </para>
	/// </remarks>
	private static bool IsStreamUniqueViolation(Exception? ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			if (current is SqlException { Number: 2627 or 2601 })
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether a failed append lost an optimistic-concurrency race, rather than failing on its
	/// own account.
	/// </summary>
	/// <param name="ex"> The exception that ended the append. </param>
	/// <param name="currentVersion"> The stream's version re-read after rollback, or <see langword="null"/> if it could not be read. </param>
	/// <param name="expectedVersion"> The version the append required the stream to be at. </param>
	/// <returns> <see langword="true"/> when the append is a concurrency conflict; otherwise <see langword="false"/>. </returns>
	/// <remarks>
	/// <para>
	/// Every loser of an optimistic-concurrency race is a concurrency conflict regardless of which error
	/// the engine happened to raise, and under contention SQL Server does not always raise the same one:
	/// a racing writer may be refused by the unique constraint, but it may equally be chosen as a deadlock
	/// victim or time out waiting on the winner's locks. Those are different error numbers for one logical
	/// outcome. Classifying by error number alone therefore reports most losers correctly and mislabels
	/// the rest, and a caller whose retry policy keys on the conflict flag does not reload and retry the
	/// ones it mislabels -- it surfaces an opaque failure for an ordinary, expected conflict.
	/// </para>
	/// <para>
	/// So the primary test is structural rather than a list of numbers: the append's transaction has been
	/// rolled back, so this writer wrote nothing; if the stream is no longer at the version this append
	/// required, the precondition was lost to another writer, whatever surfaced. That test needs no
	/// maintenance as engines and versions change, and it cannot over-report -- a stream still sitting at
	/// the expected version proves nothing else claimed it, so the failure is the append's own and is
	/// rethrown to be reported as one.
	/// </para>
	/// <para>
	/// <b>The re-read is authoritative whenever it succeeds, and the error number is only a FALLBACK for
	/// when it does not.</b> That ordering is load-bearing and it is not the obvious one, so it is worth
	/// stating why: the unique-constraint numbers are no longer a total discriminator. They were, while
	/// <c>Position</c> came from an identity column — the database chose it, so the stream key was the only
	/// unique constraint an append could possibly violate. <c>Position</c> is now a primary key whose value
	/// the APPLICATION supplies, from a counter row with an independent lifecycle, so a position collision
	/// (a counter restored from an older backup than the events table, a hand-seeded counter, two stores
	/// over one table with differently-named counters) raises error 2627 exactly like a lost race does.
	/// </para>
	/// <para>
	/// Reporting that as a concurrency conflict is not merely imprecise, it is harmful: the documented
	/// remedy for a conflict is reload-and-retry, the reloaded version still satisfies the precondition
	/// because the version was never the problem, and the caller retries into the same collision — or
	/// appends a duplicate once the counter passes the obstruction. The re-read separates the two cleanly:
	/// a position collision leaves the stream exactly where the append required it, a lost race does not.
	/// </para>
	/// <para>
	/// When the re-read itself fails there is nothing better than the error number, and the original
	/// reasoning still applies there — a lost race whose follow-up read also failed would otherwise be
	/// demoted to an ordinary failure. That residual window mis-reports a position collision, which is
	/// accepted because it requires both faults at once.
	/// </para>
	/// </remarks>
	private static bool IsLostRace(Exception ex, long? currentVersion, long expectedVersion) =>
		currentVersion is { } version
			? version != expectedVersion
			: IsStreamUniqueViolation(ex);

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
	/// Reads back whether THIS append's events are durably present, after its transaction failed to
	/// report success.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Returns <see langword="null"/> when the question could not be asked, so the caller falls through
	/// to the version comparison it used before this existed. This read can only improve the answer; it
	/// must never replace the original fault with a new one.
	/// </para>
	/// <para>
	/// <b>Its accuracy depends on the caller supplying event identifiers.</b> An event with no id
	/// contributes nothing to the lookup, so an append made entirely of unidentified events falls back to
	/// the ambiguous comparison. That is precisely why an identity constraint on (tenant, event id)
	/// belongs beside this: the read-back makes the REPORT correct, and the constraint makes the
	/// duplicate unwritable.
	/// </para>
	/// </remarks>
	/// <param name="connection">The open connection, whose transaction has already ended.</param>
	/// <param name="eventList">The events this append attempted to write.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>What was observed, or <see langword="null"/> when the question could not be asked.</returns>
	private async Task<CommittedAppendOutcome?> ReadCommittedAppendOutcomeAsync(
		SqlConnection connection,
		IReadOnlyCollection<IDomainEvent> eventList,
		CancellationToken cancellationToken)
	{
		var eventIds = eventList
			.Select(static e => e.EventId)
			.Where(static id => !string.IsNullOrWhiteSpace(id))
			.ToList();

		if (eventIds.Count == 0)
		{
			return null;
		}

		try
		{
			return await connection.ResolveAsync(
					new GetCommittedAppendOutcomeRequest(
						eventIds, CurrentTenantScope, cancellationToken, _schema, _table))
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is SqlException or OperationFailedException)
		{
			return null;
		}
	}

	/// <summary>
	/// Re-reads the persisted version after a constraint conflict so the caller is told what it lost to.
	/// </summary>
	/// <param name="connection"> The open connection, whose transaction has already been rolled back. </param>
	/// <param name="aggregateId"> The aggregate whose stream conflicted. </param>
	/// <param name="aggregateType"> The aggregate type whose stream conflicted. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The current persisted version, or <see langword="null"/> if it cannot be read. </returns>
	/// <remarks>
	/// <para>
	/// Runs outside a transaction (the conflicting one is rolled back) and only on the failure path, so it
	/// costs a round trip precisely when the caller has to reload anyway. Reporting the winner's version
	/// rather than echoing the expected one back is what makes the conflict actionable.
	/// </para>
	/// <para>
	/// Returns <see langword="null"/> rather than the expected version when the read fails, because the
	/// caller uses this value to decide whether the stream moved. Substituting the expected version there
	/// would read as "the stream did not move" - the classifier would conclude "no conflict" from a
	/// measurement that never happened.
	/// </para>
	/// <para>
	/// The wrapper is caught alongside the driver exception because this read goes through the data-request
	/// seam, which wraps whatever the driver raised. Catching the driver type alone never matched: the
	/// clause was unreachable, so a failed re-read escaped the conflict path entirely and the append was
	/// reported as an ordinary failure.
	/// </para>
	/// </remarks>
	private async Task<long?> ReadCurrentVersionAfterConflictAsync(
		SqlConnection connection,
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		try
		{
			return await connection.ResolveAsync(
					new GetCurrentVersionRequest(
						aggregateId,
						aggregateType,
						transaction: null,
						CurrentTenantScope,
						cancellationToken,
						_schema,
						_table))
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is SqlException or OperationFailedException)
		{
			return null;
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
					new GetCurrentVersionRequest(aggregateId, aggregateType, transaction, CurrentTenantScope, cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			if (currentVersion != expectedVersion)
			{
				// Explicit rollback rather than waiting for DisposeAsync.
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

				// ASK WHETHER OUR OWN EVENTS LANDED, BEFORE CLASSIFYING ANYTHING.
				//
				// A moved version says somebody wrote here; it does not say WHO. A retry of an append whose
				// acknowledgement was lost arrives HERE, at the pre-check, because its own committed write is
				// what moved the version -- so the read-back further down, on the exception path, is never
				// reached on this path and cannot help. Reporting a conflict instead sends the caller to
				// reload-and-retry, which appends the same business event again at the NEXT version, where the
				// stream uniqueness key cannot catch it because the version differs.
				//
				// Sound outside the transaction: the store is append-only, so once a row carrying event id e
				// exists it exists in every later state. A stale read can only miss it, which yields the
				// conflict we would have reported anyway.
				var committedOnRetry = await ReadCommittedAppendOutcomeAsync(connection, eventList, cancellationToken)
					.ConfigureAwait(false);

				if (committedOnRetry is { CommittedCount: > 0 } retryLanded && retryLanded.LastVersion is { } retryVersion)
				{
					activity.SetOperationResult(EventSourcingTagValues.Success);
					return AppendResult.CreateSuccess(retryVersion, retryLanded.FirstPosition);
				}

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
		catch (Exception ex) when (ex is SqlException or OperationFailedException)
		{
			// SUPERSEDED COMMENT, quoted so anyone who absorbed it recognises it: "Nothing was written or
			// staged -- the transaction is rolled back before anything below runs." THAT IS FALSE when the
			// server commits and the acknowledgement is lost -- a connection reset, a command timeout, a
			// pause past the client timeout. The rollback below then does nothing, because there is
			// nothing left to roll back.
			await RollbackQuietlyAsync(transaction).ConfigureAwait(false);

			// ASK WHETHER OUR OWN EVENTS LANDED, BEFORE CLASSIFYING ANYTHING.
			//
			// The stream version cannot answer this. "Another writer took my version" and "I took it
			// myself and lost the acknowledgement" move the version identically, so a classifier reading
			// the version reports a durably committed append as a concurrency conflict -- and the
			// documented response to a conflict is reload-and-retry, which writes the same business event
			// again at the NEXT version. The stream uniqueness key cannot stop that, because the version
			// differs. The duplicate is then permanent, and every replay applies it twice.
			var committed = await ReadCommittedAppendOutcomeAsync(connection, eventList, cancellationToken)
				.ConfigureAwait(false);

			if (committed is { CommittedCount: > 0 } landed && landed.LastVersion is { } committedVersion)
			{
				_logger.LogWarning(
					"The append for {AggregateType}/{AggregateId} committed but its acknowledgement was "
					+ "lost; reporting success from the durable rows rather than a concurrency conflict.",
					aggregateType, aggregateId);

				activity.SetOperationResult(EventSourcingTagValues.Success);
				return AppendResult.CreateSuccess(committedVersion, landed.FirstPosition);
			}

			var currentVersion = await ReadCurrentVersionAfterConflictAsync(
				connection, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);

			if (IsLostRace(ex, currentVersion, expectedVersion))
			{
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);

				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion ?? expectedVersion);
			}

			throw;
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
		foreach (var named in eventList.AsNamedEvents())
		{
			var (@event, eventTypeName) = named;
			version++;
			var eventData = SerializeEventWithEnvelopeSupport(named, aggregateId, aggregateType, version);
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			var metadata = @event.Metadata != null ? SerializeMetadata(@event.Metadata) : null;
#pragma warning restore IL2026, IL3050

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

		// Reserve this append's block of global positions. Deliberately the LAST thing done before the
		// rows are written: this takes the counter row's exclusive lock, which blocks every other appender
		// until this transaction commits. Everything above (serialization, metadata) runs outside that
		// window on purpose -- correctness does not depend on the window being short, but the store's
		// sustained append throughput is exactly one append per commit through it.
		// ...and the allocation travels WITH the first insert, in one command, rather than as a round trip
		// of its own. The counter's lock is held from that UPDATE until COMMIT, so a round trip issued
		// between them is paid by every blocked appender rather than only by this one. Measured against a
		// real SQL Server: 5.24x -> 3.94x slower than an identity column at 8 concurrent writers, and
		// 6.75x -> 4.90x at 32. Nothing about the guarantee changes; only the width of the window does.
		var firstChunkSize = Math.Min(InsertEventsBatchRequest.MaxEventsPerStatement, rows.Count);
		var firstPosition = await connection.ResolveAsync(
				new AllocateAndInsertEventsRequest(
					rows.GetRange(0, firstChunkSize),
					rows.Count,
					transaction,
					CurrentTenantScope,
					cancellationToken,
					_schema,
					_table,
					_positionTable))
			.ConfigureAwait(false);

		// Any REMAINING chunks — only for an append larger than one statement — are written with positions
		// derived from the block already reserved above, so the counter row is taken exactly once per
		// append no matter how many statements the append needs.
		//
		// The block is contiguous and ordered by version, so position i belongs to the i-th event. There is
		// nothing to match up afterwards: the previous implementation had to correlate OUTPUT rows back to
		// events by version because an IDENTITY column chose the numbers and OUTPUT row order is not
		// guaranteed. Choosing them here removes that whole correspondence problem.
		for (var offset = firstChunkSize; offset < rows.Count; offset += InsertEventsBatchRequest.MaxEventsPerStatement)
		{
			var count = Math.Min(InsertEventsBatchRequest.MaxEventsPerStatement, rows.Count - offset);
			var chunk = rows.GetRange(offset, count);

			for (var i = 0; i < chunk.Count; i++)
			{
				chunk[i] = chunk[i] with { Position = firstPosition + offset + i };
			}

			_ = await connection.ResolveAsync(
					new InsertEventsBatchRequest(chunk, transaction, CurrentTenantScope, cancellationToken, _schema, _table))
				.ConfigureAwait(false);
		}

		return (version, firstPosition);
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
		// Performance optimization: - use StringBuilder to avoid List allocation
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
		// Delegates to IDomainEvent.CorrelationId (checks OutboxHeaderNames.CorrelationId, the
		// framework declared key, then the legacy PascalCase/camelCase spellings) rather than
		// re-implementing the key-priority chain here.
		foreach (var @event in events)
		{
			if (@event.CorrelationId is { } correlationId)
			{
				return correlationId;
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
	private byte[] SerializeEvent(IDomainEvent @event, string? aggregateId, string? aggregateType)
	{
		if (_payloadSerializer != null)
		{
			return _payloadSerializer.Serialize(@event);
		}

		// Fallback to System.Text.Json for backward compatibility
		return _hasEventTypeInfoResolver
			? ResolvedEventPayload.Serialize(@event, _jsonOptions, aggregateId, aggregateType)
			: JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		_hasEventTypeInfoResolver
			? Excalibur.Dispatch.EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _jsonOptions)
			: JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);

	/// <summary>
	/// Serializes an event with envelope support if internal serializer is available.
	/// Falls back to JSON serialization if serializer is not configured.
	/// </summary>
	private byte[] SerializeEventWithEnvelopeSupport(
		NamedEvent named,
		string aggregateId,
		string aggregateType,
		long version)
	{
		var (@event, eventTypeName) = named;

		if (_internalSerializer is null)
		{
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			return SerializeEvent(@event, aggregateId, aggregateType);
#pragma warning restore IL2026, IL3050
		}

		// Create envelope with event data
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
		var eventBytes = SerializeEvent(@event, aggregateId, aggregateType);
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
			new Requests.EraseEventsRequest(aggregateId, aggregateType, erasureRequestId, CurrentTenantScope, cancellationToken, _schema, _table))
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
			new Requests.IsErasedRequest(aggregateId, aggregateType, CurrentTenantScope, cancellationToken, _schema, _table))
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
	public async Task<int> TombstoneArchivedEventsUpToVersionAsync(
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
			new Requests.TombstoneArchivedEventsRequest(
				tenant, aggregateId, aggregateType, toVersion, cancellationToken, _schema, _table))
			.ConfigureAwait(false);
	}
}
