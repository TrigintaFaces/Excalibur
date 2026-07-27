// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;

using MQTTnet;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Subscribes to an MQTT topic and adapts the broker's push delivery to the pull-based
/// <see cref="ITransportReceiver"/> contract: received messages are buffered and drained by
/// <see cref="ReceiveAsync"/>. Manual acknowledgement is honored — <see cref="AcknowledgeAsync"/> sends the
/// MQTT PUBACK/PUBCOMP for QoS 1/2.
/// </summary>
/// <remarks>
/// <b>Capability limits (MQTT, per the transport seam):</b> MQTT has no per-message negative-acknowledge or
/// requeue-after-processing. <see cref="RejectAsync"/> therefore withholds the acknowledgement (the broker
/// redelivers unacknowledged QoS 1/2 messages when the session resumes) rather than actively requeuing —
/// reliable competing-consumer redelivery is not a capability MQTT honors. Exactly-once end-to-end requires
/// QoS 2; ordering is not guaranteed under QoS 0/1.
/// </remarks>
internal sealed partial class MqttTransportReceiver : ITransportReceiver
{
	private const int BufferCapacity = 10_000;

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
		if (_pendingAcks.TryRemove(message.Id, out var args))
		{
			await args.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		// MQTT has no active per-message requeue: withhold the ack so the broker redelivers the unacknowledged
		// QoS 1/2 message on session resume. Drop the pending handle so it is never acknowledged.
		_ = _pendingAcks.TryRemove(message.Id, out _);
		LogMessageRejected(message.Id, Source, reason ?? "unspecified", requeue);
		return Task.CompletedTask;
	}

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

			_client ??= _connectionProvider.CreateClient();
			_client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

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

		var id = args.ApplicationMessage.CorrelationData is { Length: > 0 } correlation
			? Convert.ToHexString(correlation)
			: Guid.NewGuid().ToString("N");

		// Enforce the configured inbound payload cap (fail-closed). An oversized message can never be
		// processed, so it is rejected and acknowledged (dropped) rather than buffered — withholding the ack
		// would loop the broker redelivering an unprocessable payload. Matches the PayloadSizeGuard contract
		// the other transport receivers use.
		if (ExceedsPayloadLimit(_options, args.ApplicationMessage.Payload.Length))
		{
			LogMessageRejected(id, Source, $"payload exceeds MaxPayloadBytes ({_options.MaxPayloadBytes})", requeue: false);
			args.AutoAcknowledge = true;
			return;
		}

		var received = new TransportReceivedMessage
		{
			Id = id,
			Body = args.ApplicationMessage.Payload.ToArray(),
			Source = Source,
			EnqueuedAt = DateTimeOffset.UtcNow,
		};

		_pendingAcks[id] = args;
		await _buffer.Writer.WriteAsync(received).ConfigureAwait(false);
	}

	[LoggerMessage(EventId = 6130, Level = LogLevel.Warning,
		Message = "MQTT message {MessageId} from {Source} rejected (reason {Reason}, requeue {Requeue}); MQTT withholds the ack for broker redelivery on session resume.")]
	private partial void LogMessageRejected(string messageId, string source, string reason, bool requeue);
}
