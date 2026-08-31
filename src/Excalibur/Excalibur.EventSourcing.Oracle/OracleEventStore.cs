// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Security.Cryptography;

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
/// <see cref="IsolationLevel.Serializable"/> transaction before the append (per the persistence seam
/// ruling). A mismatch yields <see cref="AppendResult.CreateConcurrencyConflict(long, long)"/>.
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


	// Oracle SERIALIZABLE transactions can fail with ORA-08177 ("can't serialize access for this
	// transaction") when concurrent transactions touch the same table and the database cannot serialize
	// them — even for writes to *different* aggregates. The append is retried a bounded number of times:
	// each retry re-runs the whole read-version → check → insert unit in a fresh SERIALIZABLE transaction,
	// so a genuine competing writer surfaces as a ConcurrencyConflict (the re-read sees the advanced
	// version) while a non-conflicting serialization artifact simply succeeds. A batch of concurrent
	// writers is de-correlated with full-jitter exponential backoff so it drains rather than retrying in
	// lockstep.
	private const int MaxSerializableRetries = 10;

	// Base backoff (ms) for the first serialization retry; doubles per attempt (capped) with full jitter.
	private const int SerializableRetryBaseDelayMs = 5;
	private const int SerializableRetryMaxDelayMs = 250;

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
			var appendResult = await ExecuteWithSerializableRetryAsync(
					ct => ExecuteAppendTransactionAsync(
						aggregateId, aggregateType, eventList, expectedVersion, activity, ct),
					cancellationToken)
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
			var appendResult = await ExecuteWithSerializableRetryAsync(
					ct => ExecuteAppendWithOutboxTransactionAsync(
						aggregateId, aggregateType, eventList, expectedVersion, stageOutbox, activity, ct),
					cancellationToken)
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
				IsolationLevel.Serializable, cancellationToken)
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
				IsolationLevel.Serializable, cancellationToken)
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
	/// Executes an append unit of work with a bounded retry on Oracle ORA-08177 ("can't serialize
	/// access"). Each attempt runs the entire read-version → concurrency-check → insert sequence inside a
	/// fresh <see cref="IsolationLevel.Serializable"/> transaction, so isolation is preserved and a genuine
	/// concurrency conflict is surfaced as a <see cref="AppendResult.CreateConcurrencyConflict(long, long)"/>
	/// by the re-read (never a lost update). ORA-08177 caused by a non-conflicting concurrent writer (e.g.
	/// a different aggregate on the same table) is resolved by simply re-attempting.
	/// </summary>
	private static async ValueTask<AppendResult> ExecuteWithSerializableRetryAsync(
		Func<CancellationToken, ValueTask<AppendResult>> execute,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				return await execute(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (IsSerializationFailure(ex) && attempt < MaxSerializableRetries)
			{
				// Full-jitter exponential backoff before re-running the whole SERIALIZABLE unit of work. The
				// re-read of the current version on the next attempt either surfaces a genuine
				// ConcurrencyConflict (a competing writer advanced the stream) or succeeds (a non-conflicting
				// serialization artifact). SERIALIZABLE isolation is preserved on every attempt. Full jitter
				// de-correlates a batch of concurrent writers so they drain instead of colliding in lockstep.
				var ceilingMs = Math.Min(
					SerializableRetryMaxDelayMs, SerializableRetryBaseDelayMs << Math.Min(attempt, 6));
				var delayMs = RandomNumberGenerator.GetInt32(ceilingMs + 1);
				await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Determines whether an exception (or any exception it wraps) is an Oracle ORA-08177 serialization
	/// failure ("can't serialize access for this transaction"), which is retryable under SERIALIZABLE
	/// isolation. The data-access layer wraps provider exceptions (e.g. in an
	/// <see cref="Excalibur.Data.OperationFailedException"/>), so the whole inner-exception chain is walked.
	/// </summary>
	private static bool IsSerializationFailure(Exception? ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			if (current is OracleException { Number: 8177 })
			{
				return true;
			}
		}

		return false;
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
		foreach (var @event in eventList)
		{
			version++;
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			var eventData = SerializeEvent(@event, aggregateId, aggregateType);
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
