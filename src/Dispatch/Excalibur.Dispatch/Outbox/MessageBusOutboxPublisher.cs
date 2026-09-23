// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Globalization;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Outbox;

/// <summary>
/// Outbox publisher that stages messages for reliable delivery via a message bus. Supports multi-transport publishing with parallel
/// delivery and per-transport status tracking.
/// </summary>
/// <remarks>
/// This implementation uses the transactional outbox pattern to ensure messages are reliably delivered even in the face of failures.
/// Messages are first staged to the outbox store within the same transaction as the business operation, then published asynchronously by
/// background processors.
/// </remarks>
public sealed partial class MessageBusOutboxPublisher : IOutboxPublisher
{
	private readonly IOutboxStore _outboxStore;
	private readonly IMultiTransportOutboxStore? _multiTransportStore;
	private readonly IMultiTransportOutboxStoreAdmin? _multiTransportStoreAdmin;
	private readonly IPayloadSerializer _serializer;
	private readonly IMessageBusAdapter? _messageBus;
	private readonly ITransportRegistry? _transportRegistry;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<MessageBusOutboxPublisher> _logger;

	// Leadership fencing. The store capability is resolved through the store's own service-provider seam
	// (never a cast -- a cast sees only the outermost decorator), and the gate through the provider, so
	// presenting a token adds nothing to this type's public construction surface.
	private readonly IFencedOutboxStore? _fencedStore;

	/// <summary>
	/// The store's claim-scoped completion capability, when it offers one.
	/// </summary>
	/// <remarks>
	/// Discovered through the store's own service-provider seam like every other capability here, never by
	/// casting: a cast sees only the outermost decorator and reports a capability absent that the store
	/// beneath it implements.
	/// </remarks>
	private readonly IClaimScopedOutboxStore? _claimScopedStore;

	/// <summary>
	/// The store's combined fenced-and-claim-scoped completion capability, when it offers one.
	/// </summary>
	/// <remarks>
	/// A third probe rather than a cast or a type test, for the same reason as the two above: a cast sees
	/// only the outermost decorator. This is the INTERSECTION of two capabilities, so a store answering to
	/// each of the others separately does not necessarily answer to this one.
	/// </remarks>
	private readonly IFencedClaimScopedOutboxStore? _fencedClaimScopedStore;
	private readonly ILeaderProcessingGate? _leaderGate;
	private readonly bool _fencingActive;

	// Outbox-read DoS guard limit: from OutboxDeliveryOptions; bounded 4 MiB when unconfigured
	// (never inert), null = explicit opt-out.
	private readonly int? _maxPayloadBytes;

	private ValueStopwatch _operationStopwatch = ValueStopwatch.Empty;
	private long _totalOperations;
	private long _totalPublished;
	private long _totalFailed;
	private DateTimeOffset? _lastOperationAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageBusOutboxPublisher" /> class.
	/// </summary>
	/// <param name="outboxStore"> The outbox store for message persistence. </param>
	/// <param name="serializer"> The payload serializer for message body serialization with pluggable format support. </param>
	/// <param name="messageBus"> The message bus adapter for publishing (for single-transport mode). </param>
	/// <param name="serviceProvider"> The service provider for creating message contexts. </param>
	/// <param name="logger"> The logger instance. </param>
	public MessageBusOutboxPublisher(
		IOutboxStore outboxStore,
		IPayloadSerializer serializer,
		IMessageBusAdapter messageBus,
		IServiceProvider serviceProvider,
		ILogger<MessageBusOutboxPublisher> logger)
	{
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		// Capabilities are resolved through the store's own service-provider seam, never by casting it.
		// A cast sees only the outermost decorator's type, so a decorated store silently reports that it
		// lacks capabilities the underlying store implements -- multi-transport rows then never dispatch.
		_multiTransportStore = outboxStore.GetService(typeof(IMultiTransportOutboxStore)) as IMultiTransportOutboxStore;
		_multiTransportStoreAdmin = outboxStore.GetService(typeof(IMultiTransportOutboxStoreAdmin)) as IMultiTransportOutboxStoreAdmin;
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = ResolveMaxPayloadBytes(serviceProvider);
		_fencedStore = outboxStore.GetService(typeof(IFencedOutboxStore)) as IFencedOutboxStore;
		_claimScopedStore = outboxStore.GetService(typeof(IClaimScopedOutboxStore)) as IClaimScopedOutboxStore;
		_fencedClaimScopedStore =
			outboxStore.GetService(typeof(IFencedClaimScopedOutboxStore)) as IFencedClaimScopedOutboxStore;
		_leaderGate = ResolveLeaderGate(serviceProvider);
		_fencingActive = ResolveFencingActive(serviceProvider, _leaderGate);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageBusOutboxPublisher" /> class with multi-transport support.
	/// </summary>
	/// <param name="outboxStore"> The outbox store for message persistence. </param>
	/// <param name="serializer"> The payload serializer for message body serialization with pluggable format support. </param>
	/// <param name="transportRegistry"> The transport registry for multi-transport publishing. </param>
	/// <param name="serviceProvider"> The service provider for creating message contexts. </param>
	/// <param name="logger"> The logger instance. </param>
	public MessageBusOutboxPublisher(
		IOutboxStore outboxStore,
		IPayloadSerializer serializer,
		ITransportRegistry transportRegistry,
		IServiceProvider serviceProvider,
		ILogger<MessageBusOutboxPublisher> logger)
	{
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		// Capabilities are resolved through the store's own service-provider seam, never by casting it.
		// A cast sees only the outermost decorator's type, so a decorated store silently reports that it
		// lacks capabilities the underlying store implements -- multi-transport rows then never dispatch.
		_multiTransportStore = outboxStore.GetService(typeof(IMultiTransportOutboxStore)) as IMultiTransportOutboxStore;
		_multiTransportStoreAdmin = outboxStore.GetService(typeof(IMultiTransportOutboxStoreAdmin)) as IMultiTransportOutboxStoreAdmin;
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = ResolveMaxPayloadBytes(serviceProvider);
		_fencedStore = outboxStore.GetService(typeof(IFencedOutboxStore)) as IFencedOutboxStore;
		_claimScopedStore = outboxStore.GetService(typeof(IClaimScopedOutboxStore)) as IClaimScopedOutboxStore;
		_fencedClaimScopedStore =
			outboxStore.GetService(typeof(IFencedClaimScopedOutboxStore)) as IFencedClaimScopedOutboxStore;
		_leaderGate = ResolveLeaderGate(serviceProvider);
		_fencingActive = ResolveFencingActive(serviceProvider, _leaderGate);
	}

	// Bounded-by-default (4 MiB) unless OutboxDeliveryOptions is registered with an explicit value/opt-out.
	// Uses the non-generic GetService + a safe cast rather than GetService<T>() (which hard-casts and would
	// throw on a service provider that returns a non-IOptions instance for the type).
	private static int? ResolveMaxPayloadBytes(IServiceProvider serviceProvider) =>
		serviceProvider.GetService(typeof(IOptions<OutboxDeliveryOptions>)) is IOptions<OutboxDeliveryOptions> options
			? options.Value.MaxPayloadBytes
			: PayloadSizeGuard.DefaultMaxPayloadBytes;

	// Resolved from the provider rather than taken as a constructor parameter, so presenting the token costs
	// no change to this type's public construction surface.
	private static ILeaderProcessingGate? ResolveLeaderGate(IServiceProvider serviceProvider) =>
		serviceProvider.GetService(typeof(ILeaderProcessingGate)) as ILeaderProcessingGate;

	// A leadership tenure only fences when the deployment actually elects one. SingleActiveWriter is the
	// consumer's declaration that exactly one writer exists by construction, which is the one configuration
	// where an unfenced drain is legitimate.
	private static bool ResolveFencingActive(IServiceProvider serviceProvider, ILeaderProcessingGate? gate) =>
		gate is not null
		&& !(serviceProvider.GetService(typeof(IOptions<OutboxDeliveryOptions>)) is IOptions<OutboxDeliveryOptions> options
			&& options.Value.SingleActiveWriter);

	/// <summary>
	/// Claims a batch, presenting the leadership token when both a tenure and a fencing-capable store exist.
	/// </summary>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private ValueTask<IEnumerable<OutboundMessage>> ClaimBatchAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (_fencingActive && _fencedStore is { } fenced && _leaderGate?.FencingToken is { } token)
		{
			return fenced.GetUnsentMessagesAsync(batchSize, token, cancellationToken);
		}

		GuardActiveGateHasFencingToken();
		return _outboxStore.GetUnsentMessagesAsync(batchSize, cancellationToken);
	}

	/// <summary>
	/// Marks a message sent, presenting the leadership token when both a tenure and a fencing-capable store exist.
	/// </summary>
	private ValueTask MarkSentFencedAsync(string messageId, CancellationToken cancellationToken)
	{
		if (_fencingActive && _fencedStore is { } fenced && _leaderGate?.FencingToken is { } token)
		{
			return fenced.MarkSentAsync(messageId, token, cancellationToken);
		}

		GuardActiveGateHasFencingToken();
		return _outboxStore.MarkSentAsync(messageId, cancellationToken);
	}

	/// <summary>
	/// Composition guard: a leader gate that is <em>present</em> but yields no fencing token MUST fail closed
	/// rather than fall through to the unfenced members.
	/// </summary>
	/// <remarks>
	/// Checking leadership and then draining is check-then-act, not a fence: a dispatcher that loses its
	/// tenure while paused resumes, observes its own stale snapshot, and writes. The token is what the store
	/// compares against its durable high-water, so a drain that cannot present one under an active tenure has
	/// no way to be refused and must refuse itself. With no election configured a null token is the
	/// legitimate unfenced path and drains normally.
	/// </remarks>
	/// <remarks>
	/// <b>CALL THIS ONLY FROM A <c>try</c> WHOSE <c>catch</c> HANDLES
	/// <see cref="OutboxFenceRefusedException"/>.</b> It refuses by THROWING, and a throw raised from
	/// inside a <c>catch</c> clause is not eligible for any sibling <c>catch</c> on the same <c>try</c> --
	/// it propagates out of the whole construct and abandons every remaining message in the batch. The
	/// completion paths that run inside the drain's exception handling therefore REPORT the same refusal
	/// instead of calling this; see the refusal blocks in the failure and dead-letter members. That
	/// asymmetry is deliberate and this is the reason for it.
	/// </remarks>
	private void GuardActiveGateHasFencingToken()
	{
		if (_fencingActive && _leaderGate?.FencingToken is null)
		{
			throw new OutboxFencingTokenUnavailableException(
				"The outbox leader gate is active but no fencing token is available for the current tenure; " +
				"refusing to drain unfenced. A fenced leader election must present a monotonic fencing token. " +
				"Draining without one would let a superseded leader publish messages a live leader has claimed.");
		}
	}

	/// <inheritdoc />
	public async Task<OutboundMessage> PublishAsync(
		object message,
		string destination,
		DateTimeOffset? scheduledAt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = _serializer.SerializeObject(message, message.GetType());
		var messageType = message.GetType().FullName ?? message.GetType().Name;

		var outboundMessage = new OutboundMessage(messageType, payload, destination) { ScheduledAt = scheduledAt };

		await _outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);

		LogStagedMessage(outboundMessage.Id, messageType, destination);

		return outboundMessage;
	}

	/// <summary>
	/// Publishes a message to multiple transports with per-transport delivery tracking.
	/// </summary>
	/// <param name="message"> The message to publish. </param>
	/// <param name="transports"> The transport delivery configurations. </param>
	/// <param name="scheduledAt"> Optional scheduled delivery time. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The staged outbound message. </returns>
	public async Task<OutboundMessage> PublishMultiTransportAsync(
		object message,
		IEnumerable<OutboundMessageTransport> transports,
		CancellationToken cancellationToken,
		DateTimeOffset? scheduledAt = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transports);

		if (_multiTransportStore is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_MultiTransportRequiresStore);
		}

		var transportList = AsReadOnlyList(transports);
		if (transportList.Count == 0)
		{
			throw new ArgumentException(
				Resources.MessageBusOutboxPublisher_AtLeastOneTransportRequired,
				nameof(transports));
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var runtimeType = message.GetType();
		var payload = _serializer.SerializeObject(message, runtimeType);
		var messageType = runtimeType.FullName ?? runtimeType.Name;

		var outboundMessage = new OutboundMessage(messageType, payload, transportList[0].Destination ?? string.Empty)
		{
			ScheduledAt = scheduledAt,
			IsMultiTransport = true,
			TargetTransports = BuildTargetTransportsCsv(transportList)
		};

		// Initialize transport deliveries with the message ID
		foreach (var transport in transportList)
		{
			transport.MessageId = outboundMessage.Id;
			outboundMessage.TransportDeliveries.Add(transport);
		}

		await _multiTransportStore.StageMessageWithTransportsAsync(outboundMessage, transportList, cancellationToken)
			.ConfigureAwait(false);

		LogStagedMultiTransportMessage(
			outboundMessage.Id,
			messageType,
			transportList.Count,
			outboundMessage.TargetTransports);

		return outboundMessage;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<PublishingResult> PublishPendingMessagesAsync(CancellationToken cancellationToken) =>
		DrainClaimedBatchAsync(cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<PublishingResult> PublishScheduledMessagesAsync(CancellationToken cancellationToken) =>
		DrainClaimedBatchAsync(cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<PublishingResult> RetryFailedMessagesAsync(
		int maxRetries,
		CancellationToken cancellationToken)
	{
		if (maxRetries < 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maxRetries),
				Resources.MessageBusOutboxPublisher_MaxRetriesNonNegative);
		}

		return DrainClaimedBatchAsync(cancellationToken);
	}

	/// <summary>
	/// Claims a batch of due messages and publishes it. This is the <b>only</b> path in this publisher that
	/// hands a message to a transport.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every drain entry point funnels through here, so a message reaches a transport only after this
	/// dispatcher has won a lease on it. The store's claim is an atomic read-decide-write, so two concurrent
	/// dispatchers receive disjoint batches and neither can publish a row the other holds.
	/// </para>
	/// <para>
	/// The claim is also what decides <em>which</em> rows are due, and it already admits the two categories
	/// that once had their own loops: a scheduled row becomes claimable when its scheduled time has arrived,
	/// and a failed row becomes claimable when the next-attempt floor written by the failure path has
	/// elapsed. Selecting those categories with a separate plain read was check-then-act — the read observed
	/// a row as free without conditionally taking it, so two dispatchers could both publish it, and the
	/// failed read consulted no floor at all and re-published a message the same drain had just deferred.
	/// Routing them through the claim removes both, and needs no second reservation mechanism.
	/// </para>
	/// <para>
	/// Fenced drain: where a leadership tenure and a fencing-capable store are both present, the claim and the
	/// mark-sent carry the tenure's token, so a superseded leader is refused by the store's durable high-water
	/// rather than admitted. Checking leadership before draining is not a substitute — that is check-then-act,
	/// and a dispatcher paused past the end of its tenure resumes and writes on a stale observation. Where no
	/// election is configured the unfenced members are the legitimate path; where a tenure is active but no
	/// token can be presented, the drain refuses rather than proceeding unfenced.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<PublishingResult> DrainClaimedBatchAsync(CancellationToken cancellationToken)
	{
		var messages = await ClaimBatchAsync(100, cancellationToken).ConfigureAwait(false);
		return await PublishMessagesAsync(AsReadOnlyList(messages), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public PublisherStatistics GetStatistics()
	{
		var total = _totalPublished + _totalFailed;
		var successRate = total > 0 ? (_totalPublished * 100.0 / total) : 100.0;

		return new PublisherStatistics
		{
			TotalOperations = _totalOperations,
			TotalMessagesPublished = _totalPublished,
			TotalMessagesFailed = _totalFailed,
			CurrentSuccessRate = successRate,
			LastOperationAt = _lastOperationAt,
			CapturedAt = DateTimeOffset.UtcNow
		};
	}

	/// <summary>
	/// Publishes pending deliveries for a specific transport in parallel.
	/// </summary>
	/// <param name="transportName"> The transport name to process. </param>
	/// <param name="batchSize"> Maximum number of deliveries to process. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The publishing result. </returns>
	public async Task<PublishingResult> PublishPendingTransportDeliveriesAsync(
		string transportName,
		CancellationToken cancellationToken,
		int batchSize = 100)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		if (_multiTransportStoreAdmin is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_PerTransportRequiresStore);
		}

		if (_transportRegistry is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_PerTransportRequiresRegistry);
		}

		var adapter = _transportRegistry.GetTransportAdapter(transportName)
					  ?? throw new InvalidOperationException(
						  string.Format(
							  CultureInfo.CurrentCulture,
							  Resources.MessageBusOutboxPublisher_NoTransportAdapter,
							  transportName));

		_operationStopwatch = ValueStopwatch.StartNew();
		_totalOperations++;
		_lastOperationAt = DateTimeOffset.UtcNow;

		var deliveries = await _multiTransportStoreAdmin.GetPendingTransportDeliveriesAsync(transportName, batchSize, cancellationToken)
			.ConfigureAwait(false);

		var tasks = deliveries switch
		{
			ICollection<(OutboundMessage Message, OutboundMessageTransport Transport)> collection =>
				new List<Task<TransportPublishResult>>(collection.Count),
			_ => new List<Task<TransportPublishResult>>()
		};

		foreach (var delivery in deliveries)
		{
			tasks.Add(PublishToTransportAsync(delivery.Message, delivery.Transport, adapter, cancellationToken));
		}

		if (tasks.Count == 0)
		{
			return PublishingResult.Success(0, 0, _operationStopwatch.Elapsed);
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var successCount = 0;
		var failureCount = 0;
		for (var i = 0; i < results.Length; i++)
		{
			var result = results[i];
			if (result.IsSuccess)
			{
				successCount++;
			}
			else
			{
				failureCount++;
			}
		}

		_ = Interlocked.Add(ref _totalPublished, successCount);
		_ = Interlocked.Add(ref _totalFailed, failureCount);

		LogTransportPublishSummary(
			successCount,
			transportName,
			failureCount,
			(long)_operationStopwatch.ElapsedMilliseconds);

		if (failureCount == 0)
		{
			return PublishingResult.Success(successCount, 0, _operationStopwatch.Elapsed);
		}

		var errors = new List<PublishingError>(failureCount);
		for (var i = 0; i < results.Length; i++)
		{
			var result = results[i];
			if (result.IsSuccess)
			{
				continue;
			}

			errors.Add(new PublishingError(
				result.MessageId,
				result.ErrorMessage ?? Resources.MessageBusOutboxPublisher_UnknownError,
				result.Exception));
		}

		return PublishingResult.WithFailures(
			successCount,
			failureCount,
			errors,
			_operationStopwatch.Elapsed);
	}

	[LoggerMessage(EventId = 2400, Level = LogLevel.Debug,
		Message = "Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogStagedMessage(string messageId, string messageType, string destination);

	[LoggerMessage(EventId = 2401, Level = LogLevel.Debug,
		Message = "Staged multi-transport message {MessageId} of type {MessageType} to {TransportCount} transports: {Transports}")]
	private partial void LogStagedMultiTransportMessage(
		string messageId,
		string messageType,
		int transportCount,
		string transports);

	[LoggerMessage(EventId = 2402, Level = LogLevel.Information,
		Message = "Published {SuccessCount} messages to transport {TransportName}, {FailureCount} failed in {Duration}ms")]
	private partial void LogTransportPublishSummary(
		int successCount,
		string transportName,
		int failureCount,
		long duration);

	[LoggerMessage(EventId = 2403, Level = LogLevel.Debug,
		Message = "Published message {MessageId} to transport {TransportName} at {Destination}")]
	private partial void LogPublishedMessageToTransport(
		string messageId,
		string transportName,
		string destination);

	[LoggerMessage(EventId = 2404, Level = LogLevel.Warning,
		Message = "Failed to publish message {MessageId} to transport {TransportName}")]
	private partial void LogFailedToPublishToTransport(
		string messageId,
		string transportName,
		Exception ex);

	[LoggerMessage(EventId = 2405, Level = LogLevel.Warning,
		Message = "Message {MessageId} partially delivered: {SuccessCount} succeeded, {FailureCount} failed")]
	private partial void LogMessagePartiallyDelivered(string messageId, int successCount, int failureCount);

	[LoggerMessage(EventId = 2406, Level = LogLevel.Debug,
		Message = "Published message {MessageId} to {Destination}")]
	private partial void LogPublishedMessageToDestination(string messageId, string destination);

	[LoggerMessage(EventId = 2407, Level = LogLevel.Warning,
		Message = "Failed to publish message {MessageId} to {Destination}")]
	private partial void LogFailedToPublishToDestination(string messageId, string destination, Exception ex);

	[LoggerMessage(EventId = 2408, Level = LogLevel.Warning,
		Message = "No transport deliveries found for multi-transport message {MessageId}")]
	private partial void LogNoTransportDeliveries(string messageId);

	[LoggerMessage(EventId = 2409, Level = LogLevel.Warning,
		Message = "Fenced mark-sent for message {MessageId} was refused: this tenure's fencing token is stale (a newer leader has taken over). Aborting with no further store write; the message is left claimed for the current leader to resolve.")]
	private partial void LogFencedMarkSentRefused(string messageId, Exception ex);

	[LoggerMessage(EventId = 2410, Level = LogLevel.Information,
		Message = "Failure report for outbox message {MessageId} wrote nothing ({Outcome}); continuing with the rest of the batch")]
	private partial void LogFailureReportDeclined(string messageId, string outcome);

	private async Task<TransportPublishResult> PublishToTransportAsync(
		OutboundMessage message,
		OutboundMessageTransport transport,
		ITransportAdapter adapter,
		CancellationToken cancellationToken)
	{
		try
		{
			// Outbox-read DoS guard: reject an oversized stored payload before handing it to the
			// transport. Fail-closed — PayloadTooLargeException propagates to the surrounding catch, which
			// marks the message failed (rejected), never dispatches an over-limit body.
			PayloadSizeGuard.EnsureWithinLimit(message.Payload.Length, _maxPayloadBytes);

			var wrappedMessage = new OutboxDispatchMessage(message.Payload);
			var destination = transport.Destination ?? message.Destination;

			var context = new MessageContext(wrappedMessage, _serviceProvider)
			{
				MessageId = message.Id,
				CorrelationId = message.CorrelationId,
				CausationId = message.CausationId,
			};

			// Propagate the persisted tenant onto the rebuilt context, mirroring the inbox restore side
			// (InboxProcessor metadata restore). Only set when present so a null tenant stays absent rather
			// than creating an empty identity feature.
			if (message.TenantId is not null)
			{
				context.GetOrCreateIdentityFeature().TenantId = message.TenantId;
			}

			RestoreTraceParent(context, message);
			RestoreBaggage(context, message);

			if (message.PartitionKey is not null)
			{
				context.GetOrCreateRoutingFeature().PartitionKey = message.PartitionKey;
			}

			await adapter.SendAsync(wrappedMessage, destination, context, cancellationToken).ConfigureAwait(false);

			if (_multiTransportStore is not null)
			{
				await _multiTransportStore.MarkTransportSentAsync(message.Id, transport.TransportName, cancellationToken)
					.ConfigureAwait(false);
			}

			LogPublishedMessageToTransport(
				message.Id,
				transport.TransportName,
				destination);

			return TransportPublishResult.Success(message.Id, transport.TransportName);
		}
		catch (Exception ex)
		{
			if (_multiTransportStore is not null)
			{
				await _multiTransportStore.MarkTransportFailedAsync(
					message.Id, transport.TransportName, ex.Message, cancellationToken).ConfigureAwait(false);
			}

			LogFailedToPublishToTransport(message.Id, transport.TransportName, ex);

			return TransportPublishResult.Failure(message.Id, transport.TransportName, ex.Message, ex);
		}
	}

	// W3C propagator (BCL primitive) — used to restore the staged trace context on the outbox hop
	// instead of a hand-rolled header copy (microsoft-first: never reimplement the propagation primitive).
	private static readonly DistributedContextPropagator W3CPropagator =
		DistributedContextPropagator.CreateW3CPropagator();

	/// <summary>
	/// Restores the staged W3C <c>traceparent</c>/<c>tracestate</c> from the outbox message headers onto the
	/// rebuilt publish context so the transport-serialized envelope carries it and the distributed trace
	/// continues producer → consumer. Mirrors the inbound restore in <c>DispatchContextInitializer</c>.
	/// </summary>
	/// <remarks>
	/// The trace context is <strong>captured at enqueue time</strong> (persisted in <see cref="OutboundMessage.Headers"/>)
	/// and restored here from those stored headers — <em>not</em> from <see cref="Activity.Current"/> at flush — so a
	/// store-and-forward message keeps its originating trace. Parsing is delegated to the BCL
	/// <see cref="DistributedContextPropagator"/> rather than hand-formatted; a malformed stored value is skipped
	/// (best-effort propagation, never throws).
	/// </remarks>
	private static void RestoreTraceParent(MessageContext context, OutboundMessage message)
	{
		W3CPropagator.ExtractTraceIdAndState(
			message.Headers,
			static (object? carrier, string fieldName, out string? fieldValue, out IEnumerable<string>? fieldValues) =>
			{
				fieldValues = null;
				fieldValue = null;
				if (carrier is IReadOnlyDictionary<string, object> headers &&
					headers.TryGetValue(fieldName, out var value) &&
					value is string stringValue)
				{
					fieldValue = stringValue;
				}
			},
			out var traceParent,
			out var traceState);

		if (string.IsNullOrEmpty(traceParent) ||
			!ActivityContext.TryParse(traceParent, traceState, out _))
		{
			// No staged trace context, or a malformed value — skip (best-effort, fail-open).
			return;
		}

		var identity = context.GetOrCreateIdentityFeature();
		identity.TraceParent = traceParent;

		// Restore the staged W3C tracestate symmetric with the traceparent above, into the same context item
		// slot the consumer reads (W3CTraceContextMiddleware). Only when present so an absent tracestate stays
		// absent (best-effort propagation).
		if (!string.IsNullOrEmpty(traceState))
		{
			context.SetItem("tracestate", traceState);
		}
	}

	/// <summary>
	/// Restores the staged W3C <c>baggage</c> from the outbox message headers back onto the rebuilt publish
	/// context, symmetric with <see cref="RestoreTraceParent" />. Without this the producer's
	/// baggage is written into the staged envelope (<c>OutboxStagingMiddleware.GetBaggageHeader</c>) but silently
	/// dropped on the outbox hop, so it never reaches the next consumer. Each <c>name=value</c> member is
	/// percent-decoded (the W3C baggage percent-encoding is folded back) and written into the context items keyed
	/// <c>baggage.{name}</c> — the exact slot the capture (<c>DispatchContextInitializer</c>) and stage-write read.
	/// Malformed members are skipped rather than throwing (best-effort propagation).
	/// </summary>
	private static void RestoreBaggage(MessageContext context, OutboundMessage message)
	{
		if (!message.Headers.TryGetValue("baggage", out var value) ||
			value is not string baggage || string.IsNullOrEmpty(baggage))
		{
			return;
		}

		foreach (var rawMember in baggage.Split(','))
		{
			var member = rawMember.Trim();
			if (member.Length == 0)
			{
				continue;
			}

			var separator = member.IndexOf('=', StringComparison.Ordinal);
			if (separator <= 0)
			{
				// No key, or empty key — malformed member, skip (best-effort).
				continue;
			}

			var name = Uri.UnescapeDataString(member[..separator]);
			var baggageValue = Uri.UnescapeDataString(member[(separator + 1)..]);
			context.Items[$"baggage.{name}"] = baggageValue;
		}
	}

	/// <summary>
	/// Records a delivery failure against the CLAIM this publisher holds, rather than against the process
	/// it runs in.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The sibling handler seven lines above already refuses an unfenced write, and this one used to
	/// perform it.</b> That handler aborts with no store write when the fence refuses a mark-sent,
	/// reasoning in its own comment that the plain completion carries no fence term. The general handler
	/// beneath it then called exactly that member. The reasoning was right and only one of the two paths
	/// acted on it.
	/// </para>
	/// <para>
	/// <b>The identity is on the message and has always been.</b> This drain reads messages straight from
	/// the store with no conversion step between the claim and the completion, so the token the claim
	/// stamped is still on the row it was handed. It was never lost here; it was never presented.
	/// </para>
	/// <para>
	/// <b>Depth follows the stamp, and this call cannot make it deeper.</b> The completion discriminates
	/// exactly as finely as the value the claim wrote: presenting a token minted per acquisition refuses a
	/// report from a different claim, while presenting a bare process identity refuses nothing, because
	/// every cycle of one process presents the same string. A store whose claim does not mint a per-claim
	/// token gets no benefit from this route until it does.
	/// </para>
	/// <para>
	/// <b>Falls back rather than failing.</b> A store without the capability keeps the pre-existing
	/// unscoped completion; that write is unrefusable, which is a known gap and not a regression
	/// introduced here.
	/// </para>
	/// </remarks>
	private async Task ReportFailureForClaimAsync(
		OutboundMessage message,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		var attempt = message.RetryCount + 1;

		// THE FENCED ROUTE IS PREFERRED, and it needs all three facts together: the combined capability, an
		// active tenure holding a token, and the claim this message was handed under. Missing any one of them
		// means this caller cannot make the stronger assertion, so it falls to the route that asserts what it
		// can rather than to one that would assert more than it knows.
		if (message.DispatcherId is { Length: > 0 } claim
			&& _fencingActive
			&& _leaderGate?.FencingToken is { } fencingToken
			&& _fencedClaimScopedStore is { } fencedScoped)
		{
			var fencedOutcome = await fencedScoped.MarkFailedAsync(
					message.Id,
					errorMessage,
					attempt,
					// This drain computes no backoff schedule of its own, so the visibility floor the
					// statement applies is the store's configured one.
					nextAttemptAt: null,
					new OutboxWriteAuthority(fencingToken, claim),
					cancellationToken)
				.ConfigureAwait(false);

			if (fencedOutcome is not OutboxCompletionOutcome.Applied)
			{
				LogFailureReportDeclined(message.Id, fencedOutcome.ToString());
			}

			return;
		}

		// A MISSING TOKEN IS A REFUSAL, NEVER A DOWNGRADE. The guard above conjoins the token, so it is false
		// either because fencing is off -- the legitimate unfenced drain -- or because fencing is ON and the
		// token has gone, which under an active gate means THIS TENURE HAS BEEN SUPERSEDED. Both routes below
		// write without a token, and the claim term the second one carries does not cover this: the
		// dispatcher identity is fixed for the process and survives losing and regaining leadership, so it
		// refuses a different dispatcher and never a stale tenure of the same one.
		//
		// Returned, never thrown, for the reason given below on the declined report: this runs inside a catch
		// block, so an exception here would abandon every remaining message this publisher legitimately holds.
		if (_fencingActive && _leaderGate?.FencingToken is null)
		{
			LogFailureReportDeclined(message.Id, "FencingTokenUnavailable");
			return;
		}

		if (_claimScopedStore is null || message.DispatcherId is not { Length: > 0 } claimIdentity)
		{
			await _outboxStore.MarkFailedAsync(message.Id, errorMessage, attempt, cancellationToken)
				.ConfigureAwait(false);
			return;
		}

		// Returned, never thrown. This runs inside a catch block, where a sibling catch clause on the same
		// try cannot run -- so a refusal raised as an exception would escape the loop and abandon every
		// remaining message this publisher legitimately holds. A declined report concerns one row.
		var outcome = await _claimScopedStore
			.MarkFailedAsync(message.Id, errorMessage, attempt, claimIdentity, cancellationToken)
			.ConfigureAwait(false);

		if (outcome is not OutboxCompletionOutcome.Applied)
		{
			LogFailureReportDeclined(message.Id, outcome.ToString());
		}
	}

	private async Task<PublishingResult> PublishMessagesAsync(
		IReadOnlyList<OutboundMessage> messages,
		CancellationToken cancellationToken)
	{
		_operationStopwatch = ValueStopwatch.StartNew();
		_totalOperations++;
		_lastOperationAt = DateTimeOffset.UtcNow;

		var successCount = 0;
		var failureCount = 0;
		var errors = new List<PublishingError>();
		var publishedDelta = 0L;
		var failedDelta = 0L;

		foreach (var message in messages)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				if (message.IsMultiTransport && _multiTransportStore is not null && _transportRegistry is not null)
				{
					// Multi-transport publishing: publish to all transports in parallel
					var result = await PublishMultiTransportMessageAsync(message, cancellationToken).ConfigureAwait(false);

					if (result.FailureCount == 0)
					{
						successCount++;
						publishedDelta++;
					}
					else if (result.SuccessCount == 0)
					{
						failureCount++;
						failedDelta++;
						errors.AddRange(result.Errors);
					}
					else
					{
						// Partial success - count as success but record errors
						successCount++;
						publishedDelta++;
						LogMessagePartiallyDelivered(
							message.Id,
							result.SuccessCount,
							result.FailureCount);
					}
				}
				else
				{
					// Single-transport publishing (legacy mode)
					await PublishSingleTransportMessageAsync(message, cancellationToken).ConfigureAwait(false);
					successCount++;
					publishedDelta++;
				}

				LogPublishedMessageToDestination(message.Id, message.Destination);
			}
			catch (OutboxFenceRefusedException ex)
			{
				// The fence refused this tenure's mark-sent because a newer leader has taken over. This
				// is NOT a delivery failure -- the message may already be delivered by the current leader
				// -- so it must not be marked failed by a superseded tenure (MarkFailedAsync carries no
				// fence term and would be an unfenced write). Abort this message with no further store
				// write, leaving it as claimed for the live leader to resolve.
				LogFencedMarkSentRefused(message.Id, ex);
			}
			catch (Exception ex)
			{
				await ReportFailureForClaimAsync(message, ex.Message, cancellationToken).ConfigureAwait(false);

				failureCount++;
				failedDelta++;
				errors.Add(new PublishingError(message.Id, ex.Message, ex));

				LogFailedToPublishToDestination(message.Id, message.Destination, ex);
			}
		}

		if (publishedDelta != 0)
		{
			_ = Interlocked.Add(ref _totalPublished, publishedDelta);
		}

		if (failedDelta != 0)
		{
			_ = Interlocked.Add(ref _totalFailed, failedDelta);
		}

		return failureCount > 0
			? PublishingResult.WithFailures(successCount, failureCount, errors, _operationStopwatch.Elapsed)
			: PublishingResult.Success(successCount, 0, _operationStopwatch.Elapsed);
	}

	private async Task PublishSingleTransportMessageAsync(
		OutboundMessage message,
		CancellationToken cancellationToken)
	{
		if (_messageBus is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_SingleTransportRequiresAdapter);
		}

		// Outbox-read DoS guard: reject an oversized stored payload before handing it to the
		// transport. Fail-closed — PayloadTooLargeException propagates to PublishMessagesAsync's catch,
		// which marks the message failed (rejected), never dispatches an over-limit body.
		PayloadSizeGuard.EnsureWithinLimit(message.Payload.Length, _maxPayloadBytes);

		var wrappedMessage = new OutboxDispatchMessage(message.Payload);
		var context = new MessageContext(wrappedMessage, _serviceProvider)
		{
			MessageId = message.Id,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
		};

		// Propagate the persisted tenant onto the rebuilt context, mirroring the inbox restore side
		// (InboxProcessor metadata restore). Only set when present so a null tenant stays absent rather
		// than creating an empty identity feature.
		if (message.TenantId is not null)
		{
			context.GetOrCreateIdentityFeature().TenantId = message.TenantId;
		}

		RestoreTraceParent(context, message);
		RestoreBaggage(context, message);

		if (message.PartitionKey is not null)
		{
			context.GetOrCreateRoutingFeature().PartitionKey = message.PartitionKey;
		}

		_ = await _messageBus.PublishAsync(wrappedMessage, context, cancellationToken).ConfigureAwait(false);
		await MarkSentFencedAsync(message.Id, cancellationToken).ConfigureAwait(false);
	}

	private async Task<PublishingResult> PublishMultiTransportMessageAsync(
		OutboundMessage message,
		CancellationToken cancellationToken)
	{
		if (_multiTransportStore is null || _multiTransportStoreAdmin is null || _transportRegistry is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_MultiTransportRequiresRegistry);
		}

		// Get transport deliveries for this message. This drain claims outbox rows across every tenant by
		// design, so the demand-load below deliberately goes through the estate-wide admin read rather than
		// the tenant-confined consumer read: the drain holds no ambient tenant to confine it to, and
		// confining it would return nothing for every tenanted message.
		var deliveries = message.TransportDeliveries.Count > 0
			? AsReadOnlyList(message.TransportDeliveries)
			: AsReadOnlyList(
				await _multiTransportStoreAdmin.GetAllTenantsTransportDeliveriesAsync(message.Id, cancellationToken)
					.ConfigureAwait(false));

		if (deliveries.Count == 0)
		{
			LogNoTransportDeliveries(message.Id);
			return PublishingResult.Success(0, 0, TimeSpan.Zero);
		}

		// Publish to all pending transports in parallel
		var tasks = new List<Task<TransportPublishResult>>(deliveries.Count);

		foreach (var delivery in deliveries)
		{
			if (delivery.Status != TransportDeliveryStatus.Pending)
			{
				continue;
			}

			var adapter = _transportRegistry.GetTransportAdapter(delivery.TransportName);
			if (adapter is null)
			{
				// Mark as skipped if transport adapter not found
				await _multiTransportStore.MarkTransportSkippedAsync(
						message.Id,
						delivery.TransportName,
						Resources.MessageBusOutboxPublisher_TransportAdapterNotFound,
						cancellationToken)
					.ConfigureAwait(false);
				continue;
			}

			tasks.Add(PublishToTransportAsync(message, delivery, adapter, cancellationToken));
		}

		if (tasks.Count == 0)
		{
			return PublishingResult.Success(0, 0, TimeSpan.Zero);
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var successCount = 0;
		var failureCount = 0;
		for (var i = 0; i < results.Length; i++)
		{
			var result = results[i];
			if (result.IsSuccess)
			{
				successCount++;
			}
			else
			{
				failureCount++;
			}
		}

		// Update aggregate message status
		if (_multiTransportStoreAdmin is not null)
		{
			await _multiTransportStoreAdmin.UpdateAggregateStatusAsync(message.Id, cancellationToken).ConfigureAwait(false);
		}

		if (failureCount == 0)
		{
			return PublishingResult.Success(successCount, 0, TimeSpan.Zero);
		}

		var errors = new List<PublishingError>(failureCount);
		for (var i = 0; i < results.Length; i++)
		{
			var result = results[i];
			if (result.IsSuccess)
			{
				continue;
			}

			errors.Add(new PublishingError(
				result.MessageId,
				result.ErrorMessage ?? Resources.MessageBusOutboxPublisher_UnknownError,
				result.Exception));
		}

		return PublishingResult.WithFailures(
			successCount,
			failureCount,
			errors,
			TimeSpan.Zero);
	}

	private static IReadOnlyList<OutboundMessage> AsReadOnlyList(IEnumerable<OutboundMessage> messages)
	{
		return MaterializeReadOnlyList(messages);
	}

	private static IReadOnlyList<OutboundMessageTransport> AsReadOnlyList(IEnumerable<OutboundMessageTransport> transports)
	{
		return MaterializeReadOnlyList(transports);
	}

	private static IReadOnlyList<T> MaterializeReadOnlyList<T>(IEnumerable<T> source)
	{
		if (source is IReadOnlyList<T> readOnlyList)
		{
			return readOnlyList;
		}

		return new List<T>(source);
	}

	private static string BuildTargetTransportsCsv(IReadOnlyList<OutboundMessageTransport> transports)
	{
		var count = transports.Count;
		if (count == 0)
		{
			return string.Empty;
		}

		if (count == 1)
		{
			return transports[0].TransportName;
		}

		var totalLength = count - 1;
		for (var i = 0; i < count; i++)
		{
			totalLength += transports[i].TransportName.Length;
		}

		return string.Create(totalLength, transports, static (buffer, state) =>
		{
			var offset = 0;
			for (var i = 0; i < state.Count; i++)
			{
				if (i > 0)
				{
					buffer[offset++] = ',';
				}

				var transportName = state[i].TransportName;
				transportName.AsSpan().CopyTo(buffer[offset..]);
				offset += transportName.Length;
			}
		});
	}

	/// <summary>
	/// Result of publishing to a single transport.
	/// </summary>
	private readonly struct TransportPublishResult
	{
		private TransportPublishResult(
			string messageId,
			string transportName,
			bool isSuccess,
			string? errorMessage,
			Exception? exception)
		{
			MessageId = messageId;
			TransportName = transportName;
			IsSuccess = isSuccess;
			ErrorMessage = errorMessage;
			Exception = exception;
		}

		public string MessageId { get; }
		public string TransportName { get; }
		public bool IsSuccess { get; }
		public string? ErrorMessage { get; }
		public Exception? Exception { get; }

		public static TransportPublishResult Success(string messageId, string transportName)
			=> new(messageId, transportName, true, null, null);

		public static TransportPublishResult Failure(string messageId, string transportName, string errorMessage,
			Exception? exception = null)
			=> new(messageId, transportName, false, errorMessage, exception);
	}

	/// <summary>
	/// Wrapper message for outbox payloads that implements IDispatchMessage.
	/// </summary>
	private sealed class OutboxDispatchMessage : IDispatchMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="OutboxDispatchMessage" /> class.
		/// </summary>
		/// <param name="payload"> The serialized message payload. </param>
		public OutboxDispatchMessage(byte[] payload)
		{
			Payload = payload ?? throw new ArgumentNullException(nameof(payload));
		}

		/// <summary>
		/// Gets the serialized message payload.
		/// </summary>
		public byte[] Payload { get; }
	}
}
