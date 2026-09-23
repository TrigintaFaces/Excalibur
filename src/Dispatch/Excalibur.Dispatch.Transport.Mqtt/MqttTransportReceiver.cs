// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;

using MQTTnet;

using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Subscribes to an MQTT topic and adapts the broker's push delivery to the pull-based
/// <see cref="ITransportReceiver"/> contract: received messages are buffered and drained by
/// <see cref="ReceiveAsync"/>. Manual acknowledgement is honored — <see cref="AcknowledgeAsync"/> sends the
/// MQTT PUBACK/PUBCOMP for QoS 1/2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both rejection outcomes are honored, and they are honored by different mechanisms.</b>
/// <see cref="RejectAsync"/> with <c>requeue: true</c> withholds the acknowledgement, so the broker
/// redelivers the unacknowledged QoS 1/2 message when the session resumes. With <c>requeue: false</c> it
/// sends the acknowledgement carrying a <i>failure</i> reason code: under MQTT 5 that completes the
/// delivery flow and transfers ownership of the message to this client, so the broker does not redeliver
/// it. This provider pins MQTT 5 (<see cref="MqttConnectionProvider"/>), so the reason code is always
/// available.
/// </para>
/// <para>
/// The two levels reach that outcome by different arguments, which matters when reading the protocol.
/// At QoS 1 a PUBACK completes the flow <i>regardless</i> of its reason code — the suppression comes from
/// acknowledging at all, and the code is what tells the broker why. At QoS 2 a PUBREC carrying a reason
/// code of 0x80 or above terminates the flow outright: the sender discards the message and never sends
/// PUBREL.
/// </para>
/// <para>
/// <b>What <c>requeue: false</c> does not promise.</b> Returning normally means the acknowledgement was
/// handed to the transport carrying a failure reason code. It does not mean the broker processed it: MQTT
/// has no acknowledgement of an acknowledgement. If the connection fails before the broker handles the
/// packet, the message stays outstanding in the session and is redelivered on resume, and no mechanism in
/// the protocol can tell that from success. Suppression is therefore best-effort, and a consumer that
/// dead-letters on rejection must deduplicate on an identity that survives redelivery. <b>That is not
/// <see cref="TransportReceivedMessage.Id"/></b>, which is per-delivery by necessity — it keys the pending
/// settlement map and must not collide between two messages a producer considers the same. Key on a
/// deduplication id carried in <see cref="TransportReceivedMessage.Properties"/>, which the broker
/// retransmits with the message.
/// </para>
/// <para>
/// <b>QoS 0 cannot settle at all</b> and is refused at registration rather than accepted and lied about.
/// There is no acknowledgement packet at that level, so acknowledging is a no-op, <c>requeue: false</c>
/// cannot suppress a redelivery that was never going to happen, and <c>requeue: true</c> cannot cause one
/// — which makes rejection under QoS 0 a silent discard of the message. Exactly-once end-to-end requires
/// QoS 2; ordering is not guaranteed under QoS 0/1.
/// </para>
/// </remarks>
internal sealed partial class MqttTransportReceiver : ITransportReceiver
{
	private const int BufferCapacity = 10_000;

	/// <summary>Bound applied to the MQTT 5 reason string carried on a rejection.</summary>
	private const int MaxReasonStringLength = 256;

	private readonly IMqttConnectionProvider _connectionProvider;
	private readonly MqttOptions _options;
	private readonly ILogger<MqttTransportReceiver> _logger;
	private readonly Channel<TransportReceivedMessage> _buffer =
		Channel.CreateBounded<TransportReceivedMessage>(new BoundedChannelOptions(BufferCapacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
		});

	private readonly ConcurrentDictionary<string, MqttApplicationMessageReceivedEventArgs> _pendingAcks =
		new(StringComparer.Ordinal);

	private readonly SemaphoreSlim _connectGate = new(1, 1);
	private IMqttClient? _client;
	private volatile bool _disposed;

	public MqttTransportReceiver(
		IMqttConnectionProvider connectionProvider,
		MqttOptions options,
		ILogger<MqttTransportReceiver> logger)
	{
		_connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Source => _options.Topic;

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureSubscribedAsync(cancellationToken).ConfigureAwait(false);

		var limit = Math.Max(1, maxMessages);
		var received = new List<TransportReceivedMessage>(limit);

		// Block for the first message (bounded by cancellation), then drain whatever else is already buffered.
		if (await _buffer.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			while (received.Count < limit && _buffer.Reader.TryRead(out var message))
			{
				received.Add(message);
			}
		}

		return received;
	}

	/// <inheritdoc />
	public async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var args = ClaimSettlement(message.Id, "acknowledge");
		await args.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Takes exclusive ownership of the settlement handle for <paramref name="id"/>, or throws.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>TryRemove</c> is the atomic step that decides which of two concurrent settlements owns the
	/// message, and it is the right primitive. What matters is what the <b>loser</b> does. This method used
	/// to be written inline as <c>if (TryRemove(...)) { settle; }</c>, so a caller that lost the race — or
	/// that settled the same message twice, or passed an id from another receiver — fell off the end and
	/// returned normally. That reports a settlement which demonstrably did not happen, and it is the exact
	/// condition the contract forbids: two callers could be told they had settled the same message to
	/// contradictory outcomes when at most one of them had settled it at all.
	/// </para>
	/// <para>
	/// A claim whose loser reports success is not a claim, so the loser raises instead.
	/// </para>
	/// </remarks>
	private MqttApplicationMessageReceivedEventArgs ClaimSettlement(string id, string outcome)
	{
		if (_pendingAcks.TryRemove(id, out var args))
		{
			return args;
		}

		throw new TransportSettlementException(
			$"MQTT cannot {outcome} message '{id}': this receiver holds no outstanding settlement handle for it. "
			+ "It was already settled, it was received by a different receiver, or it arrived under a "
			+ "configuration that keeps no handle.")
		{
			TransportName = "Mqtt",

			// The handle is gone, so this receiver cannot observe what became of the message. If a
			// competing settlement took it the outcome is that settlement's, not ours; if the message was
			// never ours the broker's view is unknown to us entirely. Unspecified is the measured answer
			// and the caller is told to treat it as redelivery-possible.
			RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,

			// Nothing about repeating the call restores a handle that no longer exists.
			Retryability = SettlementRetryability.Permanent,
		};
	}

	/// <inheritdoc />
	public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var args = ClaimSettlement(message.Id, requeue ? "requeue" : "reject");

		if (requeue)
		{
			// REQUEUE: withhold the acknowledgement so the broker redelivers the unacknowledged QoS 1/2
			// message on session resume. ProcessingFailed states that intent to the client library, which
			// then sends no acknowledgement packet for this delivery -- saying it outright rather than
			// relying on the handle being dropped before anything acknowledged it.
			//
			// THIS IS A PROMISE ABOUT THE BROKER, AND MqttOptions.PersistentSession IS WHAT KEEPS IT.
			// Redelivery happens only if the session outlives the disconnect; with a clean start, or a zero
			// expiry interval, the broker drops the session with the connection and the message withheld
			// here is gone. That combination was the shipped default, so this method described a recovery
			// that could not occur; registration now refuses it. Resuming also requires the same client id,
			// so a consumer who randomises MqttOptions.ClientId per process has no redelivery either -- that
			// one is a property of a string and cannot be checked at startup.
			args.ProcessingFailed = true;
			LogMessageRequeued(message.Id, Source, reason ?? "unspecified");
			return;
		}

		// REJECT WITHOUT REQUEUE: acknowledge, but carry a failure reason code. Under MQTT 5 this completes
		// the delivery flow and transfers ownership of the message to this client, so the broker does not
		// redeliver it -- which is the outcome the caller asked for. Withholding the acknowledgement instead
		// (what this method used to do for BOTH values of requeue) produces the opposite outcome while
		// reporting success, and turns an ordinary poison-message arm into an unbounded redelivery loop that
		// dead-letters a fresh copy on every pass.
		//
		// The reason code is what distinguishes this from an ordinary acknowledgement for anyone reading the
		// broker's logs; the suppression itself comes from acknowledging at all (QoS 1), or from terminating
		// the flow before PUBREL (QoS 2).
		args.ReasonCode = MqttApplicationMessageReceivedReasonCode.UnspecifiedError;
		args.ResponseReasonString = Truncate(reason);

		await args.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
		LogMessageRejected(message.Id, Source, reason ?? "unspecified");
	}

	/// <summary>
	/// Bounds a caller-supplied rejection reason to a length the MQTT 5 reason string can carry.
	/// </summary>
	/// <remarks>
	/// The reason string is a UTF-8 field with a two-byte length prefix, so an unbounded caller string
	/// would be refused by the client or truncated by the broker. Bounding it here makes the outcome the
	/// same on every broker rather than a property of whichever one is deployed.
	/// </remarks>
	private static string? Truncate(string? reason) =>
		reason is { Length: > MaxReasonStringLength } ? reason[..MaxReasonStringLength] : reason;

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return _client is not null && serviceType.IsInstanceOfType(_client) ? _client : null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_ = _buffer.Writer.TryComplete();
		_client?.Dispose();
		_connectGate.Dispose();
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Builds the broker subscribe topic filter: the MQTT-5 shared-subscription prefix
	/// <c>$share/{group}/{topic}</c> when <see cref="MqttOptions.UseSharedSubscription"/> is enabled (so
	/// grouped consumers compete and the broker load-balances), otherwise the bare topic (fan-out — every
	/// subscriber gets every message). Extracted for testability.
	/// </summary>
	internal static string BuildTopicFilter(MqttOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.UseSharedSubscription
			? $"$share/{options.SharedSubscriptionGroup}/{options.Topic}"
			: options.Topic;
	}

	/// <summary>
	/// Creates the identifier for a single inbound delivery, used as the key of the pending-acknowledgement
	/// map. Extracted for testability.
	/// </summary>
	/// <remarks>
	/// <b>Unconditionally unique, and that is the whole safety property.</b> The key of the
	/// pending-acknowledgement map must identify ONE delivery, so anything derived from message content can
	/// collide and silently replace another delivery's acknowledgement handle. Correlation data in
	/// particular is shared by related messages by design, so deriving the key from it made collisions
	/// ordinary traffic rather than an edge case.
	/// </remarks>
	/// <returns>A fresh identifier that cannot equal that of any other delivery.</returns>
	/// <summary>
	/// Creates the identity of a single delivery. It is unique per delivery and derived from no content.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the key of the pending-settlement map, so it must never collide — including between two
	/// messages a producer considers the same.</b> It was once the hex of the message's correlation data, and
	/// a correlation id is deliberately SHARED by related messages: two in-flight deliveries collided, the
	/// second replaced the first in the map, and acknowledging the first acknowledged the second. Deriving it
	/// from any content field — a correlation id, a deduplication id, a business key — reintroduces exactly
	/// that, because every such field is one a producer may legitimately repeat.
	/// </para>
	/// <para>
	/// <b>It therefore does NOT survive a redelivery, and a consumer that dead-letters must not key on it.</b>
	/// MQTT offers no stable per-message identity of its own: the packet identifier is session-scoped and
	/// reused. What does survive is a user property, which the broker retransmits with the message — so a
	/// producer that sets a deduplication id gives the consumer a redelivery-stable key, readable from
	/// <see cref="TransportReceivedMessage.Properties"/>. Dead-letter idempotency keys on that, not on
	/// <see cref="TransportReceivedMessage.Id"/>.
	/// </para>
	/// </remarks>
	internal static string CreateDeliveryId() => Guid.NewGuid().ToString("N");

	/// <summary>
	/// Decodes MQTT correlation data back to the string the sender wrote. Extracted for testability.
	/// </summary>
	/// <remarks>
	/// The sender writes <c>TransportMessage.CorrelationId</c> with
	/// <see cref="System.Text.Encoding.UTF8"/>, so this reverses exactly that. Absent or empty correlation
	/// data yields <see langword="null"/> rather than an empty string, because "the producer set no
	/// correlation" and "the producer set an empty one" are different facts and the contract for this field
	/// is nullable.
	/// </remarks>
	/// <param name="correlationData">The raw MQTT correlation data, if any.</param>
	/// <returns>The producer's correlation identifier, or <see langword="null"/> when none was set.</returns>
	internal static string? DecodeCorrelationId(byte[]? correlationData) =>
		correlationData is { Length: > 0 }
			? System.Text.Encoding.UTF8.GetString(correlationData)
			: null;

	/// <summary>
	/// Returns <see langword="true"/> when an inbound payload exceeds the configured
	/// <see cref="MqttOptions.MaxPayloadBytes"/> cap (fail-closed reject). No cap configured = never exceeds.
	/// Extracted for testability.
	/// </summary>
	internal static bool ExceedsPayloadLimit(MqttOptions options, long payloadLength)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.MaxPayloadBytes is { } max && payloadLength > max;
	}

	private async Task EnsureSubscribedAsync(CancellationToken cancellationToken)
	{
		if (_client is { IsConnected: true })
		{
			return;
		}

		await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_client is { IsConnected: true })
			{
				return;
			}

			// THE HANDLER IS SUBSCRIBED ONCE PER CLIENT, NOT ONCE PER CONNECTION ATTEMPT.
			// The client is deliberately reused across reconnects, so a `+=` out here ran again on every
			// attempt -- after a disconnect, or after a ConnectAsync that threw. MQTTnet's AsyncEvent
			// APPENDS a delegate and invokes every registration, so one broker callback was then handled
			// twice and the message was delivered twice: each invocation takes a fresh delivery id, so
			// nothing downstream can recognise them as the same packet. Binding the subscription to the
			// client's creation makes the duplicate registration unrepresentable rather than merely
			// avoided -- there is no path that creates a client without subscribing, and none that
			// subscribes to an existing one.
			if (_client is null)
			{
				_client = _connectionProvider.CreateClient();
				_client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
			}

			await _client.ConnectAsync(_connectionProvider.BuildClientOptions("sub"), cancellationToken).ConfigureAwait(false);

			var topicFilter = BuildTopicFilter(_options);

			var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
				.WithTopicFilter(topicFilter, MqttTransportSender.MapQos(_options.QualityOfService))
				.Build();
			_ = await _client.SubscribeAsync(subscribeOptions, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_ = _connectGate.Release();
		}
	}

	private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
	{
		// Manual acknowledgement — the message is only PUBACK'd when the consumer calls AcknowledgeAsync.
		args.AutoAcknowledge = false;

		// DELIVERY IDENTITY IS PER DELIVERY, AND IT MUST NOT BE DERIVED FROM APPLICATION METADATA.
		// This used to be the hex of CorrelationData when present. CorrelationData is what the sender
		// writes TransportMessage.CorrelationId into, and a correlation id is deliberately SHARED by
		// related messages -- that is what it is for. So two distinct deliveries routinely produced the
		// same id, the second overwrote the first in _pendingAcks, and acknowledging the first
		// acknowledged the SECOND: one message PUBACK'd without being processed, the other left with no
		// handle at all and redelivered on session resume. No concurrency and no network fault needed.
		var correlationId = DecodeCorrelationId(args.ApplicationMessage.CorrelationData);

		// MQTT 5 carries the CloudEvents binary-mode ce-* attributes as user properties and the
		// structured-mode marker as ContentType -- both were previously discarded here, which made
		// binary-mode CloudEvents structurally undetectable on receive.
		//
		// The properties are read BEFORE the identity is derived, because the identity is taken from one of
		// them when the publisher supplied it. See CreateDeliveryId: that is what lets a dead-letter store
		// recognise a redelivery of a message it has already recorded.
		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		if (args.ApplicationMessage.UserProperties is { } userProperties)
		{
			foreach (var property in userProperties)
			{
				properties[property.Name] = property.Value;
			}
		}

		var id = CreateDeliveryId();

		// Enforce the configured inbound payload cap (fail-closed). An oversized message can never be
		// processed, so it is discarded and acknowledged rather than buffered — withholding the ack
		// would loop the broker redelivering an unprocessable payload. Matches the PayloadSizeGuard contract
		// the other transport receivers use.
		if (ExceedsPayloadLimit(_options, args.ApplicationMessage.Payload.Length))
		{
			LogMessageDiscarded(id, Source, $"payload exceeds MaxPayloadBytes ({_options.MaxPayloadBytes})");
			args.AutoAcknowledge = true;
			return;
		}

		var received = new TransportReceivedMessage
		{
			Id = id,
			Body = args.ApplicationMessage.Payload.ToArray(),
			ContentType = args.ApplicationMessage.ContentType,
			// The correlation now travels in the field whose contract IS correlation, instead of being
			// hex-encoded into the identity. It was previously set nowhere, so a consumer could only
			// recover it by decoding Id -- which is also why conflating the two went unnoticed.
			CorrelationId = correlationId,
			Source = Source,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = properties,

			// MQTT reports REDELIVERY, not a count: the DUP flag says this packet has been sent before and
			// no field says how often. So this saturates at 2 and does not climb. It is reported anyway
			// because a pump bounding its poison retries needs to distinguish a first delivery from a
			// repeat, and leaving it unset -- which is what this receiver did -- reports every redelivery
			// as a first attempt on the one transport whose redelivery is otherwise unbounded.
			DeliveryCount = args.ApplicationMessage.Dup ? 2 : 1,
		};

		_pendingAcks[id] = args;
		await _buffer.Writer.WriteAsync(received).ConfigureAwait(false);
	}

	[LoggerMessage(EventId = 6130, Level = LogLevel.Warning,
		Message = "MQTT message {MessageId} from {Source} rejected without requeue (reason {Reason}); acknowledged with a failure reason code, so the broker does not redeliver it.")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(EventId = 6131, Level = LogLevel.Warning,
		Message = "MQTT message {MessageId} from {Source} rejected with requeue (reason {Reason}); the acknowledgement is withheld, so the broker redelivers it when the session resumes.")]
	private partial void LogMessageRequeued(string messageId, string source, string reason);

	[LoggerMessage(EventId = 6132, Level = LogLevel.Warning,
		Message = "MQTT message {MessageId} from {Source} discarded before delivery (reason {Reason}).")]
	private partial void LogMessageDiscarded(string messageId, string source, string reason);
}
