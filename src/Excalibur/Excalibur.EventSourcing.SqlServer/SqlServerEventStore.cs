// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;
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
public sealed class SqlServerEventStore : IEventStore
{
	// Format markers for envelope detection (ADR-058)
	private const byte EnvelopeFormatMarker = 0x01;

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ILogger<SqlServerEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly IInternalSerializer? _internalSerializer;
	private readonly IPayloadSerializer? _payloadSerializer;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, IInternalSerializer?, IPayloadSerializer?)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(string connectionString, ILogger<SqlServerEventStore> logger)
		: this(connectionString, logger, internalSerializer: null, payloadSerializer: null)
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
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, IInternalSerializer?, IPayloadSerializer?)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(
		string connectionString,
		ILogger<SqlServerEventStore> logger,
		IInternalSerializer? internalSerializer)
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
	/// Use <see cref="SqlServerEventStore(Func{SqlConnection}, ILogger{SqlServerEventStore}, IInternalSerializer?, IPayloadSerializer?)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerEventStore(
		string connectionString,
		ILogger<SqlServerEventStore> logger,
		IInternalSerializer? internalSerializer,
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
		IInternalSerializer? internalSerializer = null,
		IPayloadSerializer? payloadSerializer = null)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
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
					new LoadEventsRequest(aggregateId, aggregateType, fromVersion, cancellationToken))
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
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		// Performance optimization: AD-250-1 - avoid ToList() when possible
		// If already a collection with Count, use directly; otherwise materialize once
		var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();

		if (eventList.Count == 0)
		{
			RecordAppendTelemetry(result, stopwatch.Elapsed);
			return AppendResult.CreateSuccess(expectedVersion, 0);
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

		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
				IsolationLevel.Serializable, cancellationToken)
			.ConfigureAwait(false);

		// Check current version using IDataRequest
		var currentVersion = await connection.ResolveAsync(
				new GetCurrentVersionRequest(aggregateId, aggregateType, transaction, cancellationToken))
			.ConfigureAwait(false);

		if (currentVersion != expectedVersion)
		{
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

	private async ValueTask<(long Version, long FirstPosition)> InsertEventsAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string aggregateId,
		string aggregateType,
		IReadOnlyCollection<IDomainEvent> eventList,
		long currentVersion,
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
						transaction,
						cancellationToken))
				.ConfigureAwait(false);

			if (firstPosition == 0)
			{
				firstPosition = position;
			}
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
					new GetUndispatchedEventsRequest(batchSize, cancellationToken))
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
				WriteStoreTelemetry.Providers.SqlServer,
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
					new MarkEventDispatchedRequest(eventId, cancellationToken))
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
				WriteStoreTelemetry.Providers.SqlServer,
				"mark_dispatched",
				result,
				stopwatch.Elapsed);
		}
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
