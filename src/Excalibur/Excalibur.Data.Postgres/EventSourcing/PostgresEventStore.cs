// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text.Json;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Postgres implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic event appends with optimistic concurrency control using Postgres's UNIQUE constraint.
/// Uses database transactions to ensure consistency.
/// </para>
/// <para>
/// Concurrency control is implemented via a UNIQUE constraint on (aggregate_id, aggregate_type, version).
/// When a version conflict occurs, Postgres error code 23505 (unique_violation) is caught and
/// translated to a concurrency conflict result.
/// </para>
/// <para>
/// Supports pluggable serialization via <see cref="IPayloadSerializer"/> for event payloads,
/// with backward compatibility for existing JSON-serialized events.
/// </para>
/// </remarks>
public sealed class PostgresEventStore : IEventStore
{
	// Postgres error code for unique constraint violation
	private const string UniqueViolationCode = "23505";

	// Format markers for envelope detection (ADR-058)
	private const byte EnvelopeFormatMarker = 0x01;

	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly PostgresEventStoreOptions _options;
	private readonly ILogger<PostgresEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly IInternalSerializer? _internalSerializer;
	private readonly IPayloadSerializer? _payloadSerializer;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStore"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresEventStore(
		string connectionString,
		IOptions<PostgresEventStoreOptions> options,
		ILogger<PostgresEventStore> logger)
		: this(CreateConnectionFactory(connectionString), options, logger, null, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStore"/> class with optional serializers.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="internalSerializer">Optional internal serializer for envelope support.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	public PostgresEventStore(
		string connectionString,
		IOptions<PostgresEventStoreOptions> options,
		ILogger<PostgresEventStore> logger,
		IInternalSerializer? internalSerializer,
		IPayloadSerializer? payloadSerializer)
		: this(CreateConnectionFactory(connectionString), options, logger, internalSerializer, payloadSerializer)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStore"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">Factory function that creates Postgres connections.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="internalSerializer">Optional internal serializer for envelope support.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	public PostgresEventStore(
		Func<NpgsqlConnection> connectionFactory,
		IOptions<PostgresEventStoreOptions> options,
		ILogger<PostgresEventStore> logger,
		IInternalSerializer? internalSerializer = null,
		IPayloadSerializer? payloadSerializer = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		_internalSerializer = internalSerializer;
		_payloadSerializer = payloadSerializer;
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		return await LoadAsync(aggregateId, aggregateType, -1, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var loadedEvents = await connection.ResolveAsync(
					new LoadEventsRequest(
						aggregateId,
						aggregateType,
						_options.SchemaName,
						_options.EventsTableName,
						fromVersion,
						cancellationToken))
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
				WriteStoreTelemetry.Providers.Postgres,
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
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var eventList = events.ToList();
		var correlationId = ExtractCorrelationId(eventList);
		var messageId = ExtractEventId(eventList);
		if (eventList.Count == 0)
		{
			RecordAppendTelemetry(stopwatch, result);
			return AppendResult.CreateSuccess(expectedVersion, 0);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Use RepeatableRead isolation level to prevent phantom reads while allowing
		// concurrent appends to different aggregates. Optimistic concurrency is enforced
		// via UNIQUE constraint on (aggregate_id, aggregate_type, version).
		await using var transaction =
			await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
		try
		{
			var appendResult = await ExecuteAppendTransactionAsync(
				connection,
				transaction,
				aggregateId,
				aggregateType,
				eventList,
				expectedVersion,
				activity,
				cancellationToken).ConfigureAwait(false);

			if (appendResult.status == AppendStatus.ConcurrencyConflict)
			{
				result = WriteStoreTelemetry.Results.Conflict;
			}

			return appendResult.result;
		}
		catch (PostgresException ex) when (ex.SqlState == UniqueViolationCode)
		{
			// Handle any remaining unique violations
			// Note: Transaction may already be rolled back by Postgres on constraint violation
			await TryRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);

			result = WriteStoreTelemetry.Results.Conflict;
			_logger.LogWarning(ex,
				"Concurrency conflict detected for {AggregateType}/{AggregateId}",
				aggregateType, aggregateId);

			var actualVersion = await GetCurrentVersionAsync(connection, aggregateId, aggregateType, cancellationToken)
				.ConfigureAwait(false);

			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			return AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);
		}
		catch (Exception ex)
		{
			// Note: Transaction may already be rolled back by Postgres
			await TryRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
			result = WriteStoreTelemetry.Results.Failure;
			LogAppendFailure(ex, aggregateType, aggregateId, messageId, correlationId, activity);
			return AppendResult.CreateFailure(GetFullExceptionMessage(ex));
		}
		finally
		{
			RecordAppendTelemetry(stopwatch, result);
		}
	}

	private async Task<(AppendResult result, AppendStatus status)> ExecuteAppendTransactionAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction transaction,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> eventList,
		long expectedVersion,
		System.Diagnostics.Activity? activity,
		CancellationToken cancellationToken)
	{
		// Check current version using optimistic concurrency
		var currentVersion = await connection.ResolveAsync(
				new GetCurrentVersionRequest(
					aggregateId,
					aggregateType,
					_options.SchemaName,
					_options.EventsTableName,
					transaction,
					cancellationToken))
			.ConfigureAwait(false);

		if (currentVersion != expectedVersion)
		{
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			return (AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion), AppendStatus.ConcurrencyConflict);
		}

		// Insert events
		var (version, firstPosition, conflictResult) = await InsertEventsAsync(
			connection,
			transaction,
			aggregateId,
			aggregateType,
			eventList,
			currentVersion,
			expectedVersion,
			activity,
			cancellationToken).ConfigureAwait(false);

		if (conflictResult != null)
		{
			return (conflictResult, AppendStatus.ConcurrencyConflict);
		}

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Appended {Count} events to {AggregateType}/{AggregateId} at version {Version}",
			eventList.Count, aggregateType, aggregateId, version);

		_ = (activity?.SetTag(EventSourcingTags.Version, version));
		activity.SetOperationResult(EventSourcingTagValues.Success);
		return (AppendResult.CreateSuccess(version, firstPosition), AppendStatus.Success);
	}

	private async Task<(long version, long firstPosition, AppendResult? conflictResult)> InsertEventsAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction transaction,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> eventList,
		long currentVersion,
		long expectedVersion,
		System.Diagnostics.Activity? activity,
		CancellationToken cancellationToken)
	{
		long firstPosition = 0;
		var version = currentVersion;

		foreach (var @event in eventList)
		{
			version++;
			var eventData = SerializeEventWithEnvelopeSupport(@event, aggregateId, aggregateType, version);
			var metadata = @event.Metadata != null ? SerializeMetadata(@event.Metadata) : null;
			var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

			try
			{
				var position = await connection.ResolveAsync(
						new InsertEventRequest(
							@event.EventId,
							aggregateId,
							aggregateType,
							eventTypeName,
							eventData,
							metadata,
							version,
							@event.OccurredAt,
							_options.SchemaName,
							_options.EventsTableName,
							transaction,
							cancellationToken))
					.ConfigureAwait(false);

				if (firstPosition == 0)
				{
					firstPosition = position;
				}
			}
			catch (PostgresException ex) when (ex.SqlState == UniqueViolationCode)
			{
				// Optimistic concurrency violation - version conflict detected
				// Note: Transaction may already be rolled back by Postgres on constraint violation
				await TryRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);

				_logger.LogWarning(
					"Concurrency conflict detected for {AggregateType}/{AggregateId} at version {Version}",
					aggregateType, aggregateId, version);

				// Re-read current version to report accurate conflict
				var actualVersion = await GetCurrentVersionAsync(connection, aggregateId, aggregateType, cancellationToken)
					.ConfigureAwait(false);

				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return (version, firstPosition, AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion));
			}
		}

		return (version, firstPosition, null);
	}

	private async Task<long> GetCurrentVersionAsync(
		NpgsqlConnection connection,
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		return await connection.ResolveAsync(
				new GetCurrentVersionRequest(
					aggregateId,
					aggregateType,
					_options.SchemaName,
					_options.EventsTableName,
					null,
					cancellationToken))
			.ConfigureAwait(false);
	}

	private void RecordAppendTelemetry(ValueStopwatch stopwatch, string result)
	{
		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.EventStore,
			WriteStoreTelemetry.Providers.Postgres,
			"append",
			result,
			stopwatch.Elapsed);
	}

	private void LogAppendFailure(
		Exception ex,
		string aggregateType,
		string aggregateId,
		string? messageId,
		string? correlationId,
		System.Diagnostics.Activity? activity)
	{
		using var scope = WriteStoreTelemetry.BeginLogScope(
			_logger,
			WriteStoreTelemetry.Stores.EventStore,
			WriteStoreTelemetry.Providers.Postgres,
			"append",
			messageId,
			correlationId);
		_logger.LogError(ex, "Failed to append events to {AggregateType}/{AggregateId}", aggregateType, aggregateId);
		activity.RecordException(ex);
		activity.SetOperationResult(EventSourcingTagValues.Failure);
	}

	private enum AppendStatus
	{
		Success,
		ConcurrencyConflict,
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartGetUndispatchedActivity(batchSize);

		try
		{
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var undispatchedEvents = await connection.ResolveAsync(
					new GetUndispatchedEventsRequest(
						batchSize,
						_options.SchemaName,
						_options.EventsTableName,
						cancellationToken))
				.ConfigureAwait(false);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, undispatchedEvents.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return undispatchedEvents;
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
				WriteStoreTelemetry.Providers.Postgres,
				"get_undispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity(eventId);

		try
		{
			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			_ = await connection.ResolveAsync(
					new MarkEventDispatchedRequest(
						eventId,
						_options.SchemaName,
						_options.EventsTableName,
						cancellationToken))
				.ConfigureAwait(false);

			activity.SetOperationResult(EventSourcingTagValues.Success);
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
				WriteStoreTelemetry.Providers.Postgres,
				"mark_dispatched",
				result,
				stopwatch.Elapsed);
		}
	}

	private static Func<NpgsqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return () => new NpgsqlConnection(connectionString);
	}

	/// <summary>
	/// Gets the full exception message chain for better error diagnostics.
	/// </summary>
	private static string GetFullExceptionMessage(Exception ex)
	{
		var messages = new List<string>();
		var current = ex;
		while (current != null)
		{
			messages.Add(current.Message);
			current = current.InnerException;
		}

		return string.Join(" -> ", messages);
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
	/// Attempts to rollback a transaction, ignoring errors if the transaction
	/// has already been completed (committed or rolled back by the database).
	/// </summary>
	/// <param name="transaction">The transaction to rollback.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// Postgres with SERIALIZABLE isolation may automatically abort a transaction
	/// when a constraint violation or serialization failure occurs. In such cases,
	/// attempting to explicitly rollback throws InvalidOperationException.
	/// This method safely handles that scenario.
	/// </remarks>
	private async Task TryRollbackAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken)
	{
		try
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Transaction was already completed (committed or rolled back by database)
			// This is expected in some error scenarios with SERIALIZABLE isolation
			_logger.LogDebug("Transaction was already completed, rollback skipped");
		}
	}

	/// <summary>
	/// Serializes a domain event using the configured serializer.
	/// Uses <see cref="IPayloadSerializer"/> when available,
	/// otherwise falls back to System.Text.Json.
	/// </summary>
	private byte[] SerializeEvent(IDomainEvent @event)
	{
		if (_payloadSerializer != null)
		{
			return _payloadSerializer.Serialize(@event);
		}

		// Fallback to System.Text.Json for backward compatibility
		return JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);
	}

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
			return SerializeEvent(@event);
		}

		// Create envelope with event data
		var eventBytes = SerializeEvent(@event);

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

		var envelopeData = _internalSerializer.Serialize(envelope);

		// Prepend format marker
		var result = new byte[envelopeData.Length + 1];
		result[0] = EnvelopeFormatMarker;
		envelopeData.CopyTo(result, 1);
		return result;
	}
}
