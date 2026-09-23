// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Data;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Serialization;
using Excalibur.EventSourcing.Observability;
using Excalibur.EventSourcing.Oracle.Requests;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.Logging;

using global::Oracle.ManagedDataAccess.Client;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>
/// Oracle Database implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic event appends with optimistic concurrency control. Because Oracle has no rowversion,
/// concurrency is enforced by reading the current stream version and comparing it inside a
/// <see cref="IsolationLevel.ReadCommitted"/> transaction before the append; a mismatch yields
/// <see cref="AppendResult.CreateConcurrencyConflict(long, long)"/> directly. The pre-check narrows the race
/// window but is not itself atomic under ReadCommitted, so the shipped <c>UNIQUE(AGGREGATEID,
/// AGGREGATETYPE, VERSION, TENANTID)</c> constraint (per aggregate stream) is the actual backstop: a loser
/// that slips past the pre-check hits ORA-00001 on INSERT, which <see cref="IsStreamUniqueViolation"/> /
/// <see cref="IsLostRace"/> classify as the same <see cref="AppendResult.CreateConcurrencyConflict(long, long)"/>.
/// Converges Oracle onto the same ReadCommitted + UNIQUE-constraint pattern Postgres and SQL Server already
/// use, retiring the SERIALIZABLE isolation and its ORA-08177 bounded-retry loop this store carried
/// previously — atomicity of a multi-row append comes from the transaction boundary, not the isolation
/// level, so neither depended on SERIALIZABLE in the first place.
/// </para>
/// <para>
/// Supports pluggable serialization via <see cref="IPayloadSerializer"/> for event payloads, with a
/// System.Text.Json fallback for backward compatibility.
/// </para>
/// </remarks>
public sealed class OracleEventStore : IEventStore, IEventStoreErasure, ITransactionalEventStore
{
	private readonly Func<OracleConnection> _connectionFactory;
	private readonly ILogger<OracleEventStore> _logger;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly string _schema;
	private readonly string _table;
	private readonly ITenantContext _tenantContext;

	// Cached rather than rebuilt per call: JsonSerializerOptions is expensive to construct, and the
	// host's optional type-info resolver must be attached to ONE instance for the reflection-free path
	// to be reachable at all.
	private readonly System.Text.Json.JsonSerializerOptions _jsonOptions =
		EventSerializationDefaults.CreateCanonicalOptions();

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free serialization
	/// path. Decided once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);


	/// <summary>
	/// Initializes a new instance of the <see cref="OracleEventStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">The ambient tenant context. Required: this store resolves the tenant partition it reads and writes from here.</param>
	public OracleEventStore(string connectionString, ILogger<OracleEventStore> logger, ITenantContext tenantContext)
		: this(CreateConnectionFactory(connectionString), logger, payloadSerializer: null, schema: "EXCALIBUR", table: "EVENTSTOREEVENTS", tenantContext: tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleEventStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="OracleConnection"/> instances.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	/// <param name="schema">The schema name for the event store table. Default: "EXCALIBUR".</param>
	/// <param name="table">The event store table name. Default: "EVENTSTOREEVENTS".</param>
	/// <param name="eventTypeInfoResolver">
	/// An optional source-generated JSON type-info resolver covering the application's domain event types
	/// and the runtime types of the values it places in
	/// <see cref="Excalibur.Dispatch.IDomainEvent.Metadata"/>. Supplied, the store serializes without
	/// reflection, which is what a native-AOT host published with reflection-based serialization disabled
	/// requires. Omitted, the store serializes through the reflection-based serializer exactly as before, so
	/// an existing caller is unaffected. The stored wire format is byte-identical either way.
	/// </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public OracleEventStore(
		Func<OracleConnection> connectionFactory,
		ILogger<OracleEventStore> logger,
		ITenantContext tenantContext,
		IPayloadSerializer? payloadSerializer = null,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS",
		System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? eventTypeInfoResolver = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_payloadSerializer = payloadSerializer;
		_hasEventTypeInfoResolver = Excalibur.Dispatch.EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, eventTypeInfoResolver);
		_schema = schema;
		_table = table;
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
				WriteStoreTelemetry.Providers.Oracle,
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
		catch (Exception ex) when (ex is OracleException or OperationFailedException)
		{
			// Nothing was written: the append's transaction and connection are scoped to the method that
			// raised, so both are disposed -- and an uncommitted transaction rolled back -- while the
			// exception unwinds, before this body runs. The only question left is whether this append lost
			// its version precondition to another writer, which is a concurrency conflict, or failed for its
			// own reasons, which is not.
			var currentVersion = await ReadCurrentVersionAfterFailedAppendAsync(
				aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);

			if (IsLostRace(ex, currentVersion, expectedVersion))
			{
				result = WriteStoreTelemetry.Results.Conflict;
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);

				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion ?? expectedVersion);
			}

			result = WriteStoreTelemetry.Results.Failure;
			LogAppendFailure(ex, aggregateId, aggregateType);
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
			result = WriteStoreTelemetry.Results.Failure;
			LogAppendFailure(ex, aggregateId, aggregateType);
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
		// The store owns ONE connection and ONE transaction for the whole unit of work; the append and the
		// outbox staging both run on this same connection/transaction, so a two-connection split is
		// structurally impossible.
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var transaction = (OracleTransaction)await connection.BeginTransactionAsync(
				IsolationLevel.ReadCommitted, cancellationToken)
			.ConfigureAwait(false);

		try
		{
			var currentVersion = await connection.ResolveAsync(
					new GetCurrentVersionRequest(aggregateId, aggregateType, transaction, CurrentTenantScope, cancellationToken, _schema, _table))
				.ConfigureAwait(false);

			if (currentVersion != expectedVersion)
			{
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			var (version, firstPosition) = await InsertEventsAsync(
					connection, transaction, aggregateId, aggregateType, eventList, currentVersion, cancellationToken)
				.ConfigureAwait(false);

			await stageOutbox(transaction, cancellationToken).ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Appended {Count} events and staged outbox for {AggregateType}/{AggregateId} at version {Version}",
				eventList.Count, aggregateType, aggregateId, version);

			_ = (activity?.SetTag(EventSourcingTags.Version, version));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return AppendResult.CreateSuccess(version, firstPosition);
		}
		// Mirrors ExecuteAppendTransactionAsync's classification (surfaced through AppendAsync's
		// outer catch) and SqlServerEventStore.ExecuteAppendWithOutboxTransactionAsync. Before this, EVERY
		// exception here -- including a genuine concurrent race lost past the pre-check above -- rethrew
		// raw, so AppendWithOutboxStagingAsync could never report CreateConcurrencyConflict the way the
		// plain append does; a caller expecting the same contract on both paths got an unclassified
		// OracleException instead. A non-race failure still rethrows raw (deliberately, unchanged): the
		// transactional path surfaces the real failure to the caller rather than converting it to
		// AppendResult.CreateFailure, so the repository sees the original exception.
		catch (Exception ex) when (ex is OracleException or OperationFailedException)
		{
			try
			{
				await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch
			{
				// A rollback failure must not mask the original exception.
			}

			var currentVersion = await ReadCurrentVersionAfterFailedAppendAsync(
				aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);

			if (IsLostRace(ex, currentVersion, expectedVersion))
			{
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion ?? expectedVersion);
			}

			throw;
		}
		catch
		{
			try
			{
				await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch
			{
				// A rollback failure must not mask the original exception.
			}

			throw;
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

		await using var transaction = (OracleTransaction)await connection.BeginTransactionAsync(
				IsolationLevel.ReadCommitted, cancellationToken)
			.ConfigureAwait(false);

		var currentVersion = await connection.ResolveAsync(
				new GetCurrentVersionRequest(aggregateId, aggregateType, transaction, CurrentTenantScope, cancellationToken, _schema, _table))
			.ConfigureAwait(false);

		if (currentVersion != expectedVersion)
		{
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


	/// <summary>
	/// Determines whether an exception (or any exception it wraps) is an Oracle ORA-00001 unique-constraint
	/// violation, which on the event table can only be the stream key: a second writer claiming a version
	/// this stream already holds.
	/// </summary>
	private static bool IsStreamUniqueViolation(Exception? ex) => ContainsOracleErrorCode(ex, 1);

	/// <summary>
	/// Determines whether an exception (or any exception it wraps, or bundles) carries the given Oracle
	/// error number.
	/// </summary>
	/// <remarks>
	/// Walks the <see cref="Exception.InnerException"/> chain as before, but an array-bound INSERT
	/// (<see cref="Requests.InsertEventsBatchRequest"/>'s <c>ArrayBindCount</c> execution) reports a
	/// per-element failure differently: ODP.NET raises ONE <see cref="OracleException"/> numbered 24381
	/// ("error(s) in array DML") whose <see cref="OracleException.Errors"/> collection carries the actual
	/// per-row <see cref="OracleError"/> instances — an ORA-00001 unique-key collision on row K of an N-row
	/// array bind surfaces there, not as this exception's own <see cref="OracleException.Number"/> and not
	/// as an <see cref="Exception.InnerException"/>. Without unwrapping this, a genuine lost race inside an
	/// array-bound batch would not classify as a concurrency conflict (missed by
	/// <see cref="IsStreamUniqueViolation"/>/<see cref="IsLostRace"/>) — it would surface as an opaque
	/// ORA-24381 failure instead.
	/// </remarks>
	private static bool ContainsOracleErrorCode(Exception? ex, int code)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			if (current is not OracleException oracleEx)
			{
				continue;
			}

			if (oracleEx.Number == code)
			{
				return true;
			}

			if (oracleEx.Number == 24381)
			{
				foreach (var error in oracleEx.Errors)
				{
					if (error is OracleError { Number: var errorNumber } && errorNumber == code)
					{
						return true;
					}
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether a failed append lost an optimistic-concurrency race, rather than failing on its
	/// own account.
	/// </summary>
	/// <param name="ex"> The exception that ended the append. </param>
	/// <param name="currentVersion"> The stream version re-read after rollback, or <see langword="null"/> if it could not be read. </param>
	/// <param name="expectedVersion"> The version the append required the stream to be at. </param>
	/// <returns> <see langword="true"/> when the append is a concurrency conflict; otherwise <see langword="false"/>. </returns>
	/// <remarks>
	/// <para>
	/// Under ReadCommitted the version pre-check narrows the race window but is not itself atomic, so a
	/// loser can slip past it and fail at INSERT instead -- typically ORA-00001 on the stream's UNIQUE
	/// constraint, but the shape a lost race takes on failure is not a closed set (a unique-constraint
	/// violation, a deadlock victim, a session cancelled while waiting on the winner's locks all read the
	/// same way: this writer's transaction rolled back and the stream moved). Classifying on a single error
	/// code would report most losers correctly and mislabel the rest, and a caller whose retry policy keys
	/// on the conflict flag does not reload and retry the ones it mislabels -- it surfaces an opaque failure
	/// for an ordinary, expected outcome.
	/// </para>
	/// <para>
	/// So the primary test is structural rather than a list of codes: the append's transaction has been
	/// rolled back, so this writer wrote nothing; if the stream is no longer at the version this append
	/// required, the precondition was lost to another writer, whatever surfaced. That test needs no
	/// maintenance as engines and versions change, and it cannot over-report -- a stream still sitting at
	/// the expected version proves nothing else claimed it, so the failure is the append's own and is
	/// reported as one.
	/// </para>
	/// <para>
	/// The unique-constraint code is kept as a first branch because a violation of the stream key is a
	/// conflict on the error alone, and it stays decisive on the run where the re-read itself cannot be
	/// performed.
	/// </para>
	/// </remarks>
	private static bool IsLostRace(Exception ex, long? currentVersion, long expectedVersion) =>
		IsStreamUniqueViolation(ex) || (currentVersion is { } version && version != expectedVersion);

	/// <summary>
	/// Re-reads the stream's committed version on a fresh connection after an append failed.
	/// </summary>
	/// <param name="aggregateId"> The aggregate whose append failed. </param>
	/// <param name="aggregateType"> The aggregate type whose append failed. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The current persisted version, or <see langword="null"/> if it cannot be read. </returns>
	/// <remarks>
	/// <para>
	/// A fresh connection is required: the appending connection is scoped to the method that raised and has
	/// already been disposed, and reading through it before that point would have returned this writer's own
	/// uncommitted state. Runs only on the failure path, so it costs a round trip precisely when the caller
	/// has to reload anyway. Reporting the winner's version rather than echoing the expected one back is
	/// what makes the conflict actionable.
	/// </para>
	/// <para>
	/// Returns <see langword="null"/> rather than a substitute when the read fails, because the caller uses
	/// this value to decide whether the stream moved. Supplying the expected version there would read as
	/// "the stream did not move" and the classifier would conclude "no conflict" from a measurement that
	/// never happened; supplying an estimate would read as "the stream moved" and conclude the opposite from
	/// the same non-measurement.
	/// </para>
	/// <para>
	/// The seam's wrapper is caught alongside the driver exception because this read goes through the
	/// data-request seam, which wraps whatever the driver raised. Catching the driver type alone would leave
	/// the wrapper to escape the failure path entirely and replace an append's own diagnosis with the
	/// re-read's.
	/// </para>
	/// </remarks>
	private async Task<long?> ReadCurrentVersionAfterFailedAppendAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			return await connection.ResolveAsync(
					new GetCurrentVersionRequest(
						aggregateId, aggregateType, transaction: null, CurrentTenantScope, cancellationToken, _schema, _table))
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is OracleException or OperationFailedException)
		{
			_logger.LogDebug(ex,
				"Could not re-read current version after a failed append for {AggregateType}/{AggregateId}",
				aggregateType, aggregateId);
			return null;
		}
	}

	private async ValueTask<(long Version, long FirstPosition)> InsertEventsAsync(
		OracleConnection connection,
		OracleTransaction transaction,
		string aggregateId,
		string aggregateType,
		IReadOnlyCollection<IDomainEvent> eventList,
		long currentVersion,
		CancellationToken cancellationToken)
	{
		var version = currentVersion;

		var rows = new List<EventInsertRow>(eventList.Count);
		foreach (var (@event, eventTypeName) in eventList.AsNamedEvents())
		{
			version++;
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			var eventData = SerializeEvent(@event, aggregateId, aggregateType);
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

		// Positions are matched to events by version (not by row order) because the follow-up SELECT is
		// independent of INSERT ALL row order. A null sentinel means no row matched the first version —
		// a real invariant breach.
		var firstVersion = currentVersion + 1;
		long? firstPosition = null;

		for (var offset = 0; offset < rows.Count; offset += InsertEventsBatchRequest.MaxEventsPerStatement)
		{
			var count = Math.Min(InsertEventsBatchRequest.MaxEventsPerStatement, rows.Count - offset);
			var chunk = rows.GetRange(offset, count);

			var inserted = await connection.ResolveAsync(
					new InsertEventsBatchRequest(chunk, transaction, CurrentTenantScope, cancellationToken, _schema, _table))
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
				$"Event store append inserted {rows.Count} event(s) but no position was read back for the " +
				$"first event (version {firstVersion}); the append cannot report a valid first position.");
		}

		return (version, firstPosition.Value);
	}

	private static void RecordAppendTelemetry(string result, TimeSpan elapsed)
	{
		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.EventStore,
			WriteStoreTelemetry.Providers.Oracle,
			"append",
			result,
			elapsed);
	}

	private void LogAppendFailure(Exception ex, string aggregateId, string aggregateType)
	{
		_logger.LogError(ex, "Failed to append events to {AggregateType}/{AggregateId}", aggregateType, aggregateId);
	}

	private static Func<OracleConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentNullException.ThrowIfNull(connectionString);
		return () => new OracleConnection(connectionString);
	}

	private static string GetFullExceptionMessage(Exception ex)
	{
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

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeEvent(IDomainEvent @event, string? aggregateId, string? aggregateType)
	{
		if (_payloadSerializer != null)
		{
			return _payloadSerializer.Serialize(@event);
		}

		return _hasEventTypeInfoResolver
			? ResolvedEventPayload.Serialize(@event, _jsonOptions, aggregateId, aggregateType)
			: System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		_hasEventTypeInfoResolver
			? Excalibur.Dispatch.EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _jsonOptions)
			: System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);

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
			new EraseEventsRequest(aggregateId, aggregateType, erasureRequestId, CurrentTenantScope, cancellationToken, _schema, _table))
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
			new IsErasedRequest(aggregateId, aggregateType, CurrentTenantScope, cancellationToken, _schema, _table))
			.ConfigureAwait(false);
	}
}
