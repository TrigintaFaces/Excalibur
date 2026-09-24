// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

using RabbitMQ.Client;

using Testcontainers.RabbitMq;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for RabbitMQ transport using TestContainers.
/// Automatically provisions a RabbitMQ container for testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Transport")]
[Trait("Transport", "RabbitMq")]
public sealed class RabbitMqTransportConformanceTests
	: TransportConformanceTestBase<RabbitMqChannelSender, RabbitMqChannelReceiver>,
	ITransportConformanceCapabilities
{
	private const string QueueName = "conformance-test-queue";
	private const string DlqName = "conformance-test-queue-dlq";
	private const string FilterExchangeName = "conformance-filter-exchange";
	private const string FilterQueueName = "conformance-filter-queue";

	private RabbitMqContainer? _rabbitMqContainer;
	private IConnection? _connection;
	private IChannel? _senderChannel;
	private IChannel? _receiverChannel;
	private RabbitMqDeadLetterQueueManager? _dlqManager;
	private IChannel? _filterChannel;

	/// <summary>
	/// Only <see cref="TransportCapability.PublishTimeFiltering" /> is advertised, and the bead that asked for
	/// this declaration assumed the opposite — it named RabbitMQ as a receive-time filterer. It is not, and the
	/// reason is architectural rather than incidental: AMQP routes a message to queues by evaluating BINDINGS
	/// AT PUBLISH TIME. A binding created after the publish does not retroactively route anything, so once a
	/// message has been routed (or discarded for matching no binding) no server-side predicate can be applied
	/// to it on the read. There is no RabbitMQ topology that honours a filter first supplied at receive time;
	/// a consumer could only fetch everything and discard client-side, which is not server-side filtering and
	/// would make the assertion measure this test rather than the broker.
	/// <para>
	/// MEASURED from this suite's own implementation, the same way the Azure Service Bus suite states its own:
	/// the headers-exchange binding is installed in <see cref="ITransportConformanceCapabilities.PrepareFilterAsync" />
	/// before the sends, and <c>ReceiveMatchingAsync</c> never consults its <c>filter</c> argument — it reads
	/// whatever the broker already admitted into the bound queue. That is what makes this a broker-side
	/// assertion: the non-matching message was offered to the exchange while the binding was live and was not
	/// routed. The remaining capability flags are not implemented here; this suite proves filtering, not the
	/// full set.
	/// </para>
	/// Returns null until the filter channel exists, so the capability-gated facts skip rather than NRE if they
	/// somehow ran before <see cref="CreateSenderAsync" />.
	/// </summary>
	protected override ITransportConformanceCapabilities? AdvancedCapabilities => _filterChannel is null ? null : this;

	/// <inheritdoc />
	TransportCapability ITransportConformanceCapabilities.Capabilities => TransportCapability.PublishTimeFiltering;

	protected override async Task<RabbitMqChannelSender> CreateSenderAsync()
	{
		// Start RabbitMQ container
		_rabbitMqContainer = new RabbitMqBuilder()
			.WithImage("rabbitmq:3-management")
			.Build();

		await _rabbitMqContainer.StartAsync();

		// Create connection
		var factory = new ConnectionFactory
		{
			Uri = new Uri(_rabbitMqContainer.GetConnectionString())
		};
		_connection = await factory.CreateConnectionAsync();
		_senderChannel = await _connection.CreateChannelAsync();

		// Declare queue
		_ = await _senderChannel.QueueDeclareAsync(
			queue: QueueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: null);

		// A headers exchange is the RabbitMQ mechanism for routing on message ATTRIBUTES rather than on a
		// routing-key string, which is what the filtering arm supplies. The exchange and the queue are
		// declared here; the BINDING between them is deliberately NOT — it is the filter itself, and it is
		// installed in PrepareFilterAsync so the broker has it live before anything is published.
		_filterChannel = await _connection.CreateChannelAsync();

		await _filterChannel.ExchangeDeclareAsync(
			exchange: FilterExchangeName,
			type: ExchangeType.Headers,
			durable: false,
			autoDelete: false,
			arguments: null);

		_ = await _filterChannel.QueueDeclareAsync(
			queue: FilterQueueName,
			durable: false,
			exclusive: false,
			autoDelete: false,
			arguments: null);

		return new RabbitMqChannelSender(_senderChannel, QueueName);
	}

	/// <inheritdoc />
	async Task ITransportConformanceCapabilities.PrepareFilterAsync(
		IReadOnlyDictionary<string, string> filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		if (_filterChannel is null)
		{
			throw new InvalidOperationException("Filter channel not initialized. CreateSenderAsync must run first.");
		}

		// x-match=all means every listed header must match for the broker to route the message here. The
		// binding is created BEFORE the sends because AMQP evaluates bindings at publish time: a binding
		// added afterwards would not route a message that has already been published, and the non-matching
		// message would simply have been discarded by the exchange with nowhere to go. Installing it first is
		// what lets the assertion mean what it says — the broker was offered the non-matching message while
		// this binding was live and declined to route it.
		var bindingArguments = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["x-match"] = "all",
		};

		foreach (var (key, value) in filter)
		{
			bindingArguments[key] = value;
		}

		await _filterChannel.QueueBindAsync(
			queue: FilterQueueName,
			exchange: FilterExchangeName,
			routingKey: string.Empty,
			arguments: bindingArguments,
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	async Task ITransportConformanceCapabilities.SendFilterableAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> attributes,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(attributes);
		if (_filterChannel is null)
		{
			throw new InvalidOperationException("Filter channel not initialized. CreateSenderAsync must run first.");
		}

		var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
		foreach (var (key, value) in attributes)
		{
			headers[key] = value;
		}

		var properties = new BasicProperties
		{
			ContentType = "application/json",
			Headers = headers,
		};

		// Published to the EXCHANGE, not to a queue: the exchange is where the broker evaluates the binding.
		// A message whose headers do not match is not routed anywhere, which is the outcome under test.
		await _filterChannel.BasicPublishAsync(
			exchange: FilterExchangeName,
			routingKey: string.Empty,
			mandatory: false,
			basicProperties: properties,
			body: System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(body)),
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	async Task<ConformanceReceiveResult<T>?> ITransportConformanceCapabilities.ReceiveMatchingAsync<T>(
		IReadOnlyDictionary<string, string> filter,
		CancellationToken cancellationToken)
		where T : default
	{
		ArgumentNullException.ThrowIfNull(filter);
		if (_filterChannel is null)
		{
			throw new InvalidOperationException("Filter channel not initialized. CreateSenderAsync must run first.");
		}

		// The filter argument is deliberately NOT consulted. The broker already applied it at publish time via
		// the binding, so reading the bound queue returns only what was routed. Re-applying the predicate here
		// would turn a server-side filtering assertion into a client-side one that passes regardless of what
		// the broker did — the precise vacuity this capability exists to avoid.
		while (!cancellationToken.IsCancellationRequested)
		{
			var result = await _filterChannel.BasicGetAsync(FilterQueueName, autoAck: true, cancellationToken)
				.ConfigureAwait(false);

			if (result is not null)
			{
				var json = System.Text.Encoding.UTF8.GetString(result.Body.Span);
				return new ConformanceReceiveResult<T>(
					System.Text.Json.JsonSerializer.Deserialize<T>(json),
					headers: null,
					acknowledge: null,
					reject: null);
			}

			await Task.Delay(50, cancellationToken).ConfigureAwait(false); // delay-ok: poll pacing; the loop exits on a message arriving, not on elapsed time
		}

		return null;
	}

	/// <inheritdoc />
	Task ITransportConformanceCapabilities.SendWithHeadersAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> headers,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(RabbitMqTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	/// <inheritdoc />
	Task<ConformanceReceiveResult<T>?> ITransportConformanceCapabilities.ReceiveWithContextAsync<T>(
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(RabbitMqTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	/// <inheritdoc />
	Task ITransportConformanceCapabilities.SendCloudEventAsync(
		CloudEvent cloudEvent,
		CloudEventBinding binding,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(RabbitMqTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	/// <inheritdoc />
	Task<CloudEvent?> ITransportConformanceCapabilities.ReceiveCloudEventAsync(
		CloudEventBinding binding,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(RabbitMqTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	protected override async Task<RabbitMqChannelReceiver> CreateReceiverAsync()
	{
		if (_connection == null)
		{
			throw new InvalidOperationException("RabbitMQ connection not initialized. Ensure sender is created first.");
		}

		_receiverChannel = await _connection.CreateChannelAsync();

		return new RabbitMqChannelReceiver(_receiverChannel, QueueName);
	}

	protected override async Task<IDeadLetterQueueManager?> CreateDlqManagerAsync()
	{
		if (_senderChannel == null)
		{
			throw new InvalidOperationException("RabbitMQ channel not initialized.");
		}

		// Declare DLQ
		_ = await _senderChannel.QueueDeclareAsync(
			queue: DlqName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: null);

		_dlqManager = new RabbitMqDeadLetterQueueManager(_senderChannel, QueueName, DlqName);
		return _dlqManager;
	}

	protected override async Task DisposeTransportAsync()
	{
		if (_senderChannel != null)
		{
			await _senderChannel.CloseAsync();
			_senderChannel.Dispose();
		}

		if (_receiverChannel != null)
		{
			await _receiverChannel.CloseAsync();
			_receiverChannel.Dispose();
		}

		if (_filterChannel != null)
		{
			await _filterChannel.CloseAsync();
			_filterChannel.Dispose();
		}

		if (_connection != null)
		{
			await _connection.CloseAsync();
			_connection.Dispose();
		}

		if (_rabbitMqContainer != null)
		{
			await _rabbitMqContainer.DisposeAsync();
		}
	}
}

/// <summary>
/// RabbitMQ implementation of IChannelSender for conformance testing.
/// </summary>
public sealed class RabbitMqChannelSender : IChannelSender
{
	private readonly IChannel _channel;
	private readonly string _queueName;

	public RabbitMqChannelSender(IChannel channel, string queueName)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
	}

	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		if (message == null)
		{
			throw new ArgumentNullException(nameof(message));
		}

		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var body = System.Text.Encoding.UTF8.GetBytes(json);

		var properties = new BasicProperties
		{
			ContentType = "application/json",
			DeliveryMode = DeliveryModes.Persistent,
			MessageId = Guid.NewGuid().ToString()
		};

		// Extract metadata if available
		var messageType = typeof(T);
		if (messageType.GetProperty("MessageId") != null)
		{
			var messageId = messageType.GetProperty("MessageId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(messageId))
			{
				properties.MessageId = messageId;
			}
		}

		if (messageType.GetProperty("CorrelationId") != null)
		{
			var correlationId = messageType.GetProperty("CorrelationId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(correlationId))
			{
				properties.CorrelationId = correlationId;
			}
		}

		await _channel.BasicPublishAsync(
			exchange: string.Empty,
			routingKey: _queueName,
			mandatory: false,
			basicProperties: properties,
			body: body,
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// RabbitMQ implementation of IChannelReceiver for conformance testing.
/// </summary>
public sealed class RabbitMqChannelReceiver : IChannelReceiver
{
	private readonly IChannel _channel;
	private readonly string _queueName;

	public RabbitMqChannelReceiver(IChannel channel, string queueName)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
	}

	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		// Poll basic-get until a message is available or the caller's deadline (cancellationToken) fires.
		// A single get (or one short retry) returns null whenever the message has not yet been routed and
		// queued, which is common under heavy CI/TestContainers load. Polling for the full receive window
		// makes the round-trip deterministic: it returns as soon as the message arrives.
		BasicGetResult? result = null;
		while (!cancellationToken.IsCancellationRequested)
		{
			result = await _channel.BasicGetAsync(_queueName, autoAck: false, cancellationToken)
				.ConfigureAwait(false);
			if (result is not null)
			{
				break;
			}

			try
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}

		if (result is null)
		{
			return default;
		}

		try
		{
			var json = System.Text.Encoding.UTF8.GetString(result.Body.ToArray());
			var message = System.Text.Json.JsonSerializer.Deserialize<T>(json);

			// Ack message after successful processing
			await _channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken)
				.ConfigureAwait(false);

			return message;
		}
		catch
		{
			// Nack message on failure (will be requeued)
			await _channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true, cancellationToken)
				.ConfigureAwait(false);
			throw;
		}
	}
}

/// <summary>
/// RabbitMQ implementation of IDeadLetterQueueManager for conformance testing.
/// </summary>
public sealed class RabbitMqDeadLetterQueueManager : IDeadLetterQueueManager
{
	private readonly IChannel _channel;
	private readonly string _queueName;
	private readonly string _dlqName;

	public RabbitMqDeadLetterQueueManager(IChannel channel, string queueName, string dlqName)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
		_dlqName = dlqName ?? throw new ArgumentNullException(nameof(dlqName));
	}

	public async Task<string> MoveToDeadLetterAsync(
			TransportMessage message,
			string reason,
			Exception? exception,
			CancellationToken cancellationToken)
	{
		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var body = System.Text.Encoding.UTF8.GetBytes(json);

		var properties = new BasicProperties
		{
			ContentType = "application/json",
			DeliveryMode = DeliveryModes.Persistent,
			MessageId = message.Id,
			Headers = new Dictionary<string, object?>
			{
				["Reason"] = reason,
				["DeadLetteredAt"] = DateTimeOffset.UtcNow.ToString("O")
			}
		};

		if (exception != null)
		{
			properties.Headers["Exception"] = exception.Message;
		}

		await _channel.BasicPublishAsync(
			exchange: string.Empty,
			routingKey: _dlqName,
			mandatory: false,
			basicProperties: properties,
			body: body,
			cancellationToken: cancellationToken).ConfigureAwait(false);

		return message.Id;
	}

	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
			int maxMessages,
			CancellationToken cancellationToken)
	{
		var result = new List<DeadLetterMessage>();

		for (int i = 0; i < maxMessages; i++)
		{
			var message = await _channel.BasicGetAsync(_dlqName, autoAck: false, cancellationToken)
				.ConfigureAwait(false);

			if (message == null)
			{
				break;
			}

			var json = System.Text.Encoding.UTF8.GetString(message.Body.ToArray());
			var transportMessage = System.Text.Json.JsonSerializer.Deserialize<TransportMessage>(json);

			if (transportMessage != null)
			{
				var reason = GetHeaderString(message.BasicProperties.Headers, "Reason") ?? "Unknown";
				var exceptionMessage = GetHeaderString(message.BasicProperties.Headers, "Exception");
				var deadLetteredAtStr = GetHeaderString(message.BasicProperties.Headers, "DeadLetteredAt");
				var deadLetteredAt = !string.IsNullOrEmpty(deadLetteredAtStr)
					? DateTimeOffset.Parse(deadLetteredAtStr)
					: DateTimeOffset.UtcNow;

				result.Add(new DeadLetterMessage
				{
					OriginalMessage = transportMessage,
					Reason = reason,
					Exception = exceptionMessage != null ? new InvalidOperationException(exceptionMessage) : null,
					DeadLetteredAt = deadLetteredAt
				});
			}
		}

		return result;
	}

	public Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
			IEnumerable<DeadLetterMessage> messages,
			ReprocessOptions options,
			CancellationToken cancellationToken)
	{
		var result = new ReprocessResult
		{
			SuccessCount = messages.Count(),
			FailureCount = 0
		};

		return Task.FromResult(result);
	}

	public async Task<DeadLetterStatistics> GetStatisticsAsync(
			CancellationToken cancellationToken)
	{
		var queueInfo = await _channel.QueueDeclarePassiveAsync(_dlqName, cancellationToken)
			.ConfigureAwait(false);

		return new DeadLetterStatistics
		{
			MessageCount = (int)queueInfo.MessageCount,
			OldestMessageAge = TimeSpan.Zero // RabbitMQ doesn't provide this directly
		};
	}

	public async Task<int> PurgeAllTenantsDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		var result = await _channel.QueuePurgeAsync(_dlqName, cancellationToken).ConfigureAwait(false);
		return (int)result;
	}

	/// <summary>
	/// Safely extracts a string from RabbitMQ headers, handling byte arrays.
	/// </summary>
	private static string? GetHeaderString(IDictionary<string, object?>? headers, string key)
	{
		if (headers == null || !headers.TryGetValue(key, out var value) || value == null)
		{
			return null;
		}

		// RabbitMQ stores header strings as byte arrays
		if (value is byte[] bytes)
		{
			return System.Text.Encoding.UTF8.GetString(bytes);
		}

		return value.ToString();
	}
}
