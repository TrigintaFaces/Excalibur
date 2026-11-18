// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Globalization;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Resilience;
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
[SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling",
	Justification = "Coordinates the existing serialization, transport, store, and retry-owner contracts for transactional publication; these dependencies belong to that composition boundary.")]
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
	private readonly OutboxTenureAdmission _tenureAdmission;

	// Outbox-read DoS guard limit: from OutboxDeliveryOptions; bounded 4 MiB when unconfigured
	// (never inert), null = explicit opt-out.
	private readonly int? _maxPayloadBytes;

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
		_tenureAdmission = new OutboxTenureAdmission(_fencingActive, _leaderGate);
		EnsureClaimCompletionCapability();
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
		_tenureAdmission = new OutboxTenureAdmission(_fencingActive, _leaderGate);
		EnsureClaimCompletionCapability();
	}

	private void EnsureClaimCompletionCapability()
	{
		if (_fencingActive ? _fencedStore is null || _fencedClaimScopedStore is null : _claimScopedStore is null)
		{
			throw new InvalidOperationException($"The configured outbox publisher requires " +
				(_fencingActive ? "IFencedOutboxStore and IFencedClaimScopedOutboxStore" : "IClaimScopedOutboxStore") +
				$" from '{_outboxStore.GetType().FullName}'. Register a store implementing atomic acquisition-scoped completion; " +
				"legacy direct completion methods cannot safely complete a framework worker's lease.");
		}
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
	private ValueTask<IEnumerable<OutboundMessage>> ClaimBatchAsync(int batchSize, long? capturedToken, CancellationToken cancellationToken)
	{
		if (_fencingActive && _fencedStore is { } fenced && capturedToken is { } token)
		{
			return fenced.GetUnsentMessagesAsync(batchSize, token, cancellationToken);
		}

		GuardCapturedToken(capturedToken);
		return _outboxStore.GetUnsentMessagesAsync(batchSize, cancellationToken);
	}

	/// <summary>
	/// Marks a message sent, presenting the leadership token when both a tenure and a fencing-capable store exist.
	/// </summary>
	private ValueTask<OutboxCompletionOutcome> MarkSentFencedAsync(OutboxClaimEvidence claim, long? capturedToken, CancellationToken cancellationToken)
	{
		if (_fencingActive)
		{
			if (capturedToken is not { } token) { throw new OutboxFencingTokenUnavailableException(); }
			return _fencedClaimScopedStore!.MarkSentAsync(claim.MessageId,
				new OutboxWriteAuthority(token, claim.ClaimIdentity), cancellationToken);
		}
		return _claimScopedStore!.MarkSentAsync(claim.MessageId, claim.ClaimIdentity, cancellationToken);
	}

	private void GuardCapturedToken(long? capturedToken)
	{
		if (_fencingActive && capturedToken is null)
		{
			throw new OutboxFencingTokenUnavailableException("An admitted fenced drain must retain its captured token.");
		}
	}

	private static PublishingResult IncompleteResult(int successes, int failures, int skipped,
		IReadOnlyList<PublishingError> errors, TimeSpan duration,
		PublishingOutcome outcome = PublishingOutcome.TenureRefused) => new()
	{
		SuccessCount = successes,
		FailureCount = failures,
		SkippedCount = skipped,
		Errors = errors,
		Duration = duration,
		Outcome = outcome,
	};

	private void ObserveRefusal(long? capturedToken, OutboxFenceRefusedException? error = null)
	{
		// An unavailable-token guard is not evidence of a durable high-water mark.
		if (error is not OutboxFencingTokenUnavailableException)
		{
			_tenureAdmission.Reject(capturedToken);
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
		if (message is not IDispatchMessage)
		{
			throw new ArgumentException("Outbox messages must implement IDispatchMessage.", nameof(message));
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var runtimeType = message.GetType();
		var payload = _serializer.SerializeObject(message, runtimeType);
		var messageType = OutboxMessageTypeResolver.Register(runtimeType);

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
		if (message is not IDispatchMessage)
		{
			throw new ArgumentException("Outbox messages must implement IDispatchMessage.", nameof(message));
		}

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
		var messageType = OutboxMessageTypeResolver.Register(runtimeType);

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
	public Task<PublishingResult> PublishPendingMessagesAsync(CancellationToken cancellationToken) =>
		DrainClaimedBatchAsync(cancellationToken);

	/// <inheritdoc />
	public Task<PublishingResult> PublishScheduledMessagesAsync(CancellationToken cancellationToken) =>
		DrainClaimedBatchAsync(cancellationToken);

	/// <inheritdoc />
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
	/// Claims a batch of due messages and publishes it. All pending, scheduled and retry row drains share this path.
	/// The per-transport drain independently uses the same tenure admission state.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every row-drain entry point funnels through here, so a claimed row reaches a transport only after this
	/// dispatcher has won a lease on it. The store's claim is an atomic read-decide-write, so two concurrent
	/// dispatchers receive disjoint batches and neither can publish a row the other holds.
	/// </para>
	/// <para>
	/// The claim is also what decides <em>which</em> rows are due, and it already admits the two categories
	/// that once had their own loops: a scheduled row becomes claimable when its scheduled time has arrived,
	/// and a failed row becomes claimable when the next-attempt floor written by the failure path has
	/// elapsed. Selecting those categories with a separate plain read was check-then-act â€” the read observed
	/// a row as free without conditionally taking it, so two dispatchers could both publish it, and the
	/// failed read consulted no floor at all and re-published a message the same drain had just deferred.
	/// Routing them through the claim removes both, and needs no second reservation mechanism.
	/// </para>
	/// <para>
	/// Fenced drain: where a leadership tenure and a fencing-capable store are both present, the claim and the
	/// mark-sent carry the tenure's token, so a superseded leader is refused by the store's durable high-water
	/// rather than admitted. Checking leadership before draining is not a substitute â€” that is check-then-act,
	/// and a dispatcher paused past the end of its tenure resumes and writes on a stale observation. Where no
	/// election is configured the unfenced members are the legitimate path; where a tenure is active but no
	/// token can be presented, the drain refuses rather than proceeding unfenced.
	/// </para>
	/// </remarks>
	private async Task<PublishingResult> DrainClaimedBatchAsync(CancellationToken cancellationToken)
	{
		var capturedToken = _tenureAdmission.Capture();
		if (!_tenureAdmission.TryAdmit(capturedToken))
		{
			return IncompleteResult(0, 0, 0, [], TimeSpan.Zero);
		}

		IEnumerable<OutboundMessage> messages;
		try
		{
			messages = await ClaimBatchAsync(100, capturedToken, cancellationToken).ConfigureAwait(false);
		}
		catch (OutboxFenceRefusedException error)
		{
			ObserveRefusal(capturedToken, error);
			return IncompleteResult(0, 0, 0, [], TimeSpan.Zero);
		}

		return await PublishMessagesAsync(AsReadOnlyList(messages), capturedToken, cancellationToken).ConfigureAwait(false);
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

		var capturedToken = _tenureAdmission.Capture();
		if (!_tenureAdmission.TryAdmit(capturedToken))
		{
			return IncompleteResult(0, 0, 0, [], TimeSpan.Zero);
		}

		var adapter = _transportRegistry.GetTransportAdapter(transportName)
					  ?? throw new InvalidOperationException(
						  string.Format(
							  CultureInfo.CurrentCulture,
							  Resources.MessageBusOutboxPublisher_NoTransportAdapter,
							  transportName));

		var stopwatch = ValueStopwatch.StartNew();
		_ = Interlocked.Increment(ref _totalOperations);
		_lastOperationAt = DateTimeOffset.UtcNow;

		var deliveries = await _multiTransportStoreAdmin.GetPendingTransportDeliveriesAsync(transportName, batchSize, cancellationToken)
			.ConfigureAwait(false);

		var tenureRefused = false;
		var tasks = deliveries switch
		{
			ICollection<(OutboundMessage Message, OutboundMessageTransport Transport)> collection =>
				new List<Task<TransportPublishResult>>(collection.Count),
			_ => new List<Task<TransportPublishResult>>()
		};

		var admittedIds = new List<string>();
		Exception? admissionFailure = null;
		try
		{
			foreach (var delivery in deliveries)
			{
				if (!_tenureAdmission.TryAdmit(capturedToken))
				{
					tenureRefused = true;
					break;
				}
				tasks.Add(PublishToTransportAsync(delivery.Message, delivery.Transport, adapter, cancellationToken));
				admittedIds.Add(delivery.Message.Id);
			}
		}
		catch (Exception error)
		{
			admissionFailure = error;
		}

		if (tasks.Count == 0 && !tenureRefused && admissionFailure is null)
		{
			return PublishingResult.Success(0, 0, stopwatch.Elapsed);
		}

		var joined = await JoinAdmittedDeliveriesAsync(tasks, admittedIds, admissionFailure, string.Empty, cancellationToken).ConfigureAwait(false);
		var results = joined.Results;

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
			(long)stopwatch.ElapsedMilliseconds);


		var errors = new List<PublishingError>(failureCount + joined.InfrastructureErrors.Count);
		errors.AddRange(joined.InfrastructureErrors);
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

		if (tenureRefused)
		{
			return IncompleteResult(successCount, failureCount, 0, errors, stopwatch.Elapsed);
		}

		if (joined.InfrastructureErrors.Count > 0)
		{
			return IncompleteResult(successCount, failureCount, 0, errors, stopwatch.Elapsed, PublishingOutcome.Interrupted);
		}

		if (failureCount == 0)
		{
			return PublishingResult.Success(successCount, 0, stopwatch.Elapsed);
		}

		return PublishingResult.WithFailures(
			successCount,
			failureCount,
			errors,
			stopwatch.Elapsed);
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

	[LoggerMessage(EventId = 2411, Level = LogLevel.Warning,
		Message = "Fenced mark-sent for message {MessageId} was refused: this tenure's fencing token is stale (a newer leader has taken over). Aborting with no further store write; the message is left claimed for the current leader to resolve.")]
	private partial void LogFencedMarkSentRefusedOutcome(string messageId);

	[LoggerMessage(EventId = 2412, Level = LogLevel.Warning,
		Message = "Mark-sent for message {MessageId} was not applied: the store reported {Outcome}. The message was published; only the record of it did not land, so it becomes claimable again when its reservation ages out. This concerns only this message.")]
	private partial void LogMarkSentNotApplied(string messageId, string outcome);

	[LoggerMessage(EventId = 2410, Level = LogLevel.Information,
		Message = "Failure report for outbox message {MessageId} wrote nothing ({Outcome}); continuing with the rest of the batch")]
	private partial void LogFailureReportDeclined(string messageId, string outcome);

	[LoggerMessage(EventId = 2413, Level = LogLevel.Error,
		Message = "Transport accepted outbox message {MessageId}, but its durable completion failed. Stopping this operation without a delivery-failure report; the commit outcome may be unknown.")]
	private partial void LogCompletionFailedAfterAcceptance(string messageId, Exception exception);

	private async Task<TransportPublishResult> PublishToTransportAsync(
		OutboundMessage message,
		OutboundMessageTransport transport,
		ITransportAdapter adapter,
		CancellationToken cancellationToken)
	{
		try
		{
			// Outbox-read DoS guard: reject an oversized stored payload before handing it to the
			// transport. Fail-closed â€” PayloadTooLargeException propagates to the surrounding catch, which
			// marks the message failed (rejected), never dispatches an over-limit body.
			PayloadSizeGuard.EnsureWithinLimit(message.Payload.Length, _maxPayloadBytes);

			var wrappedMessage = RestoreMessage(message);
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

			using (RetryOwnershipScope.Enter(wrappedMessage, context))
			{
				await adapter.SendAsync(wrappedMessage, destination, context, cancellationToken).ConfigureAwait(false);
			}

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
			// Adapters can wrap cancellation or return it as failure. Caller cancellation takes
			// precedence over charging a new durable failure, regardless of exception shape.
			cancellationToken.ThrowIfCancellationRequested();
			if (_multiTransportStore is not null)
			{
				await _multiTransportStore.MarkTransportFailedAsync(
					message.Id, transport.TransportName, ex.Message, cancellationToken).ConfigureAwait(false);
			}

			LogFailedToPublishToTransport(message.Id, transport.TransportName, ex);

			return TransportPublishResult.Failure(message.Id, transport.TransportName, ex.Message, ex);
		}
	}

	// W3C propagator (BCL primitive) â€” used to restore the staged trace context on the outbox hop
	// instead of a hand-rolled header copy (microsoft-first: never reimplement the propagation primitive).
	private static readonly DistributedContextPropagator W3CPropagator =
		DistributedContextPropagator.CreateW3CPropagator();

	/// <summary>
	/// Restores the staged W3C <c>traceparent</c>/<c>tracestate</c> from the outbox message headers onto the
	/// rebuilt publish context so the transport-serialized envelope carries it and the distributed trace
	/// continues producer â†’ consumer. Mirrors the inbound restore in <c>DispatchContextInitializer</c>.
	/// </summary>
	/// <remarks>
	/// The trace context is <strong>captured at enqueue time</strong> (persisted in <see cref="OutboundMessage.Headers"/>)
	/// and restored here from those stored headers â€” <em>not</em> from <see cref="Activity.Current"/> at flush â€” so a
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
			// No staged trace context, or a malformed value â€” skip (best-effort, fail-open).
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
	/// <c>baggage.{name}</c> â€” the exact slot the capture (<c>DispatchContextInitializer</c>) and stage-write read.
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
				// No key, or empty key â€” malformed member, skip (best-effort).
				continue;
			}

			var name = Uri.UnescapeDataString(member[..separator]);
			var baggageValue = Uri.UnescapeDataString(member[(separator + 1)..]);
			context.Items[$"baggage.{name}"] = baggageValue;
		}
	}

	/// <summary>Reports a failure using the evidence captured before any delivery callback.</summary>
	private async Task<OutboxCompletionOutcome> ReportFailureForClaimAsync(
		OutboxClaimEvidence claim, int attempt, string errorMessage, long? capturedToken, CancellationToken cancellationToken)
	{
		OutboxCompletionOutcome outcome;
		if (_fencingActive)
		{
			if (capturedToken is not { } token) { return OutboxCompletionOutcome.FenceRefused; }
			outcome = await _fencedClaimScopedStore!.MarkFailedAsync(claim.MessageId, errorMessage, attempt, null,
				new OutboxWriteAuthority(token, claim.ClaimIdentity), cancellationToken).ConfigureAwait(false);
			if (outcome is OutboxCompletionOutcome.FenceRefused) { ObserveRefusal(capturedToken); }
		}
		else
		{
			outcome = await _claimScopedStore!.MarkFailedAsync(claim.MessageId, errorMessage, attempt,
				claim.ClaimIdentity, cancellationToken).ConfigureAwait(false);
		}
		if (outcome is not OutboxCompletionOutcome.Applied) { LogFailureReportDeclined(claim.MessageId, outcome.ToString()); }
		return outcome;
	}

	private async Task<PublishingResult> PublishMessagesAsync(
		IReadOnlyList<OutboundMessage> messages,
		long? capturedToken,
		CancellationToken cancellationToken)
	{
		var claims = new (OutboxClaimEvidence Claim, int Attempt)[messages.Count];
		for (var i = 0; i < messages.Count; i++)
		{
			claims[i] = (new OutboxClaimEvidence(messages[i].Id, messages[i].DispatcherId!), messages[i].RetryCount + 1);
		}
		var stopwatch = ValueStopwatch.StartNew();
		_ = Interlocked.Increment(ref _totalOperations);
		_lastOperationAt = DateTimeOffset.UtcNow;

		var successCount = 0;
		var failureCount = 0;
		var errors = new List<PublishingError>();
		var publishedDelta = 0L;
		var failedDelta = 0L;
		var tenureRefused = false;
		var operationOutcome = PublishingOutcome.Completed;
		var skippedCount = 0;

		try
		{
			for (var index = 0; index < messages.Count; index++)
			{
				var message = messages[index];
				var claim = claims[index].Claim;
				var attempt = claims[index].Attempt;
				cancellationToken.ThrowIfCancellationRequested();
				if (!_tenureAdmission.TryAdmit(capturedToken))
				{
					tenureRefused = true;
					skippedCount = messages.Count - index;
					break;
				}
	
				try
				{
					if (message.IsMultiTransport && _multiTransportStore is not null && _transportRegistry is not null)
					{
						// Multi-transport publishing: publish to all transports in parallel
						var result = await PublishMultiTransportMessageAsync(message, capturedToken, cancellationToken).ConfigureAwait(false);
	
						if (result.Outcome != PublishingOutcome.Completed)
						{
							operationOutcome = result.Outcome;
							tenureRefused = result.IsTenureRefused;
							skippedCount = messages.Count - index - 1;
							errors.AddRange(result.Errors);
							// A partially admitted fanout is not a fully published message.
							if (result.SuccessCount > 0) { successCount++; publishedDelta++; }
							else if (result.FailureCount > 0) { failureCount++; failedDelta++; }
							break;
						}
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
							errors.AddRange(result.Errors);
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
						var completion = await PublishSingleTransportMessageAsync(message, claim, capturedToken, cancellationToken).ConfigureAwait(false);
						var markOutcome = completion.Outcome;
						// The transport accepted this row even if its durable completion was refused.
						successCount++;
						publishedDelta++;

						if (completion.Failure is { } completionFailure)
						{
							operationOutcome = PublishingOutcome.Interrupted;
							skippedCount = messages.Count - index - 1;
							errors.Add(new PublishingError(claim.MessageId, completionFailure.Message, completionFailure));
							LogCompletionFailedAfterAcceptance(claim.MessageId, completionFailure);
							break;
						}

						if (markOutcome is OutboxCompletionOutcome.FenceRefused)
						{
							ObserveRefusal(capturedToken, completion.Refusal);
							tenureRefused = true;
							if (completion.Refusal is { } refusal) { LogFencedMarkSentRefused(message.Id, refusal); }
							else { LogFencedMarkSentRefusedOutcome(message.Id); }
							tenureRefused = true;
							skippedCount = messages.Count - index - 1;
							break;
						}
	
						// A ROW-scoped refusal is different: the transport accepted the message, so it WAS
						// published and the counters are honest. Only the record of it did not land, which the
						// reservation ageing out resolves. Recorded, not fatal, and the batch continues.
						if (markOutcome is not (OutboxCompletionOutcome.Applied or OutboxCompletionOutcome.ClaimLost
							or OutboxCompletionOutcome.AlreadyTerminal or OutboxCompletionOutcome.MessageNotFound))
						{
							var invalidOutcome = new InvalidOperationException($"Unsupported outbox completion outcome '{markOutcome}'. The durable commit outcome is unknown.");
							operationOutcome = PublishingOutcome.Interrupted;
							skippedCount = messages.Count - index - 1;
							errors.Add(new PublishingError(claim.MessageId, invalidOutcome.Message, invalidOutcome));
							LogCompletionFailedAfterAcceptance(claim.MessageId, invalidOutcome);
							break;
						}

						if (markOutcome is { } rowOutcome && rowOutcome is not OutboxCompletionOutcome.Applied)
						{
							LogMarkSentNotApplied(claim.MessageId, rowOutcome.ToString());
						}
					}
	
					LogPublishedMessageToDestination(message.Id, message.Destination);
				}
				catch (OutboxFenceRefusedException ex)
				{
					ObserveRefusal(capturedToken, ex);
					LogFencedMarkSentRefused(message.Id, ex);
					tenureRefused = true;
					skippedCount = messages.Count - index - 1;
					break;
				}
				catch (Exception ex)
				{
					if (tenureRefused) { throw; }
					cancellationToken.ThrowIfCancellationRequested();
					failureCount++;
					failedDelta++;
					errors.Add(new PublishingError(claim.MessageId, ex.Message, ex));
					try
					{
						var outcome = await ReportFailureForClaimAsync(claim, attempt, ex.Message, capturedToken, cancellationToken).ConfigureAwait(false);
						if (outcome is OutboxCompletionOutcome.FenceRefused)
						{
							ObserveRefusal(capturedToken);
							tenureRefused = true;
							skippedCount = messages.Count - index - 1;
							break;
						}
					}
					catch (OutboxFenceRefusedException refusal)
					{
						ObserveRefusal(capturedToken, refusal);
						tenureRefused = true;
						skippedCount = messages.Count - index - 1;
						break;
					}
	
					LogFailedToPublishToDestination(message.Id, message.Destination, ex);
				}
			}
		}
		finally
		{
			if (publishedDelta != 0)
			{
				_ = Interlocked.Add(ref _totalPublished, publishedDelta);
			}
	
			if (failedDelta != 0)
			{
				_ = Interlocked.Add(ref _totalFailed, failedDelta);
			}
		}

		return tenureRefused || operationOutcome != PublishingOutcome.Completed
			? IncompleteResult(successCount, failureCount, skippedCount, errors, stopwatch.Elapsed,
				tenureRefused ? PublishingOutcome.TenureRefused : operationOutcome)
			: failureCount > 0
			? PublishingResult.WithFailures(successCount, failureCount, errors, stopwatch.Elapsed)
			: new PublishingResult { SuccessCount = successCount, Errors = errors, Duration = stopwatch.Elapsed };
	}

	/// <summary>
	/// Publishes one message through the single-transport path and marks it sent.
	/// </summary>
	/// <returns>
	/// The completion outcome, authority refusal or ordinary infrastructure failure after transport acceptance.
	/// Acceptance is counted once. A refused tenure stops tenure admissions; an ordinary completion fault
	/// interrupts only this operation and preserves its original error without a synthetic delivery-failure
	/// report. The durable commit outcome may be unknown. Already admitted work remains joined.
	/// </returns>
	private async Task<(OutboxCompletionOutcome? Outcome, OutboxFenceRefusedException? Refusal, Exception? Failure)> PublishSingleTransportMessageAsync(
		OutboundMessage message,
		OutboxClaimEvidence claim,
		long? capturedToken,
		CancellationToken cancellationToken)
	{
		if (_messageBus is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_SingleTransportRequiresAdapter);
		}

		// Outbox-read DoS guard: reject an oversized stored payload before handing it to the
		// transport. Fail-closed â€” PayloadTooLargeException propagates to PublishMessagesAsync's catch,
		// which marks the message failed (rejected), never dispatches an over-limit body.
		PayloadSizeGuard.EnsureWithinLimit(message.Payload.Length, _maxPayloadBytes);

		var wrappedMessage = RestoreMessage(message);
		var context = new MessageContext(wrappedMessage, _serviceProvider)
		{
			MessageId = claim.MessageId,
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

		IMessageResult result;
		using (RetryOwnershipScope.Enter(wrappedMessage, context))
		{
			result = await _messageBus.PublishAsync(wrappedMessage, context, cancellationToken).ConfigureAwait(false);
		}
		if (!result.Succeeded)
		{
			throw new InvalidOperationException(result.ErrorMessage ?? result.ProblemDetails?.Detail ?? "Outbox message publishing failed.");
		}
		try
		{
			return (await MarkSentFencedAsync(claim, capturedToken, cancellationToken).ConfigureAwait(false), null, null);
		}
		catch (OutboxFenceRefusedException refusal)
		{
			return (OutboxCompletionOutcome.FenceRefused, refusal, null);
		}
		catch (Exception failure)
		{
			// Transport acceptance is irreversible here; an unknown durable commit is not a failed send.
			return (null, null, failure);
		}
	}

	private async Task<PublishingResult> PublishMultiTransportMessageAsync(
		OutboundMessage message,
		long? capturedToken,
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
		var tenureRefused = false;

		var admittedIds = new List<string>();
		Exception? admissionFailure = null;
		try
		{
			foreach (var delivery in deliveries)
			{
				if (delivery.Status != TransportDeliveryStatus.Pending)
				{
					continue;
				}
	
				if (!_tenureAdmission.TryAdmit(capturedToken))
				{
					tenureRefused = true;
					break;
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
				admittedIds.Add(message.Id);
			}
		}
		catch (Exception error)
		{
			admissionFailure = error;
		}

		if (tasks.Count == 0 && !tenureRefused && admissionFailure is null)
		{
			return PublishingResult.Success(0, 0, TimeSpan.Zero);
		}

		var joined = await JoinAdmittedDeliveriesAsync(tasks, admittedIds, admissionFailure, message.Id, cancellationToken).ConfigureAwait(false);
		var results = joined.Results;

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

		// Aggregate bookkeeping is separate from the already-settled transport effects.
		// Preserve those effects if admission observation or the store itself fails.
		try
		{
			if (!_tenureAdmission.TryAdmit(capturedToken)) { tenureRefused = true; }
			if (!tenureRefused && joined.InfrastructureErrors.Count == 0 && _multiTransportStoreAdmin is not null)
			{
				await _multiTransportStoreAdmin.UpdateAggregateStatusAsync(message.Id, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception error)
		{
			cancellationToken.ThrowIfCancellationRequested();
			joined.InfrastructureErrors.Add(new PublishingError(message.Id, error.Message, error));
		}


		var errors = new List<PublishingError>(failureCount + joined.InfrastructureErrors.Count);
		errors.AddRange(joined.InfrastructureErrors);
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

		if (tenureRefused)
		{
			return IncompleteResult(successCount, failureCount, 0, errors, TimeSpan.Zero);
		}

		if (joined.InfrastructureErrors.Count > 0)
		{
			return IncompleteResult(successCount, failureCount, 0, errors, TimeSpan.Zero, PublishingOutcome.Interrupted);
		}

		if (failureCount == 0)
		{
			return PublishingResult.Success(successCount, 0, TimeSpan.Zero);
		}

		return PublishingResult.WithFailures(
			successCount,
			failureCount,
			errors,
			TimeSpan.Zero);
	}

	private static async Task<(TransportPublishResult[] Results, List<PublishingError> InfrastructureErrors)> JoinAdmittedDeliveriesAsync(
		List<Task<TransportPublishResult>> tasks, List<string> messageIds, Exception? admissionFailure,
		string admissionMessageId, CancellationToken cancellationToken)
	{
		try
		{
			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// All admitted tasks have settled. Inspect each outcome below without abandoning siblings.
		}

		cancellationToken.ThrowIfCancellationRequested();
		var results = new List<TransportPublishResult>(tasks.Count);
		var errors = new List<PublishingError>();
		for (var index = 0; index < tasks.Count; index++)
		{
			var task = tasks[index];
			if (task.IsCompletedSuccessfully)
			{
				results.Add(await task.ConfigureAwait(false));
			}
			else if (task.Exception is { } taskFailure)
			{
				foreach (var error in taskFailure.InnerExceptions)
				{
					errors.Add(new PublishingError(messageIds[index], error.Message, error));
				}
			}
			else
			{
				try { await task.ConfigureAwait(false); }
				catch (OperationCanceledException error)
				{
					errors.Add(new PublishingError(messageIds[index], error.Message, error));
				}
			}
		}
		if (admissionFailure is not null)
		{
			errors.Add(new PublishingError(admissionMessageId, admissionFailure.Message, admissionFailure));
		}
		return (results.ToArray(), errors);
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

	private IDispatchMessage RestoreMessage(OutboundMessage message)
		=> OutboxMessageTypeResolver.Restore(message, _serializer);
}
