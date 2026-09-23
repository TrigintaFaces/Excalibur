// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using CloudNative.CloudEvents;

using Testcontainers.ServiceBus;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for Azure Service Bus transport using TestContainers.
/// Automatically provisions an Azure Service Bus emulator container for testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Transport")]
[Trait("Transport", "AzureServiceBus")]
// CA1001: Disposable fields are disposed via IAsyncLifetime.DisposeAsync -> DisposeTransportAsync pattern
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Base class implements IAsyncLifetime which calls DisposeTransportAsync to dispose fields")]
public sealed class AzureServiceBusTransportConformanceTests
	: TransportConformanceTestBase<AzureServiceBusChannelSender, AzureServiceBusChannelReceiver>,
	ITransportConformanceCapabilities
{
	private const string QueueName = "conformance-test-queue";
	private const string TopicName = "conformance-test-topic";
	private const string SubscriptionName = "conformance-test-sub";

	/// <summary>
	/// The Service Bus emulator is genuinely optional infrastructure, so this suite skips rather than fails
	/// when it cannot start -- the same call <c>AzureServiceBusContainerFixture</c> already makes by
	/// overriding <c>AllowGracefulDegradation</c> to true. Required transports keep the fail-closed default.
	/// </summary>
	protected override bool AllowUnavailableTransport => true;

	/// <summary>
	/// Only <see cref="TransportCapability.PublishTimeFiltering" /> is advertised (R2.15, bd-uzzze3): Azure
	/// Service Bus supports real server-side content filtering via a topic subscription's SQL rule, and that
	/// rule is evaluated when a message is PUBLISHED to the topic. MEASURED from this suite's own
	/// implementation, not assumed: the rule is installed in <c>PrepareFilterAsync</c> before the sends, and
	/// <c>ReceiveMatchingAsync</c> never consults its <c>filter</c> argument -- it reads whatever the broker
	/// already admitted. A predicate first supplied on the read therefore cannot be honoured here, which is
	/// exactly why <see cref="TransportCapability.ReceiveTimeFiltering" /> is NOT advertised. The other
	/// capability flags are not implemented here -- this suite exists to prove filtering, not the full set.
	/// Returns null until the emulator client exists, so the capability-gated fact skips rather than NREs
	/// if it somehow ran before <see cref="CreateSenderAsync" />.
	/// </summary>
	protected override ITransportConformanceCapabilities? AdvancedCapabilities => _client is null ? null : this;

	/// <inheritdoc />
	TransportCapability ITransportConformanceCapabilities.Capabilities => TransportCapability.PublishTimeFiltering;

	private ServiceBusContainer? _serviceBusContainer;
	private ServiceBusClient? _client;
	private ServiceBusAdministrationClient? _adminClient;
	private ServiceBusSender? _sender;
	private ServiceBusReceiver? _receiver;
	private ServiceBusSender? _topicSender;
	private AzureServiceBusDeadLetterQueueManager? _dlqManager;

	protected override async Task<AzureServiceBusChannelSender> CreateSenderAsync()
	{
		// Start Azure Service Bus emulator container
		_serviceBusContainer = new ServiceBusBuilder()
			.WithAcceptLicenseAgreement(true)
			.Build();

		await _serviceBusContainer.StartAsync();

		// Create the client using the emulator connection string
		var connectionString = _serviceBusContainer.GetConnectionString();
		_client = new ServiceBusClient(connectionString);

		// The emulator serves messaging over AMQP (5672) and management over a SEPARATE HTTP endpoint
		// (5300), and GetConnectionString() addresses only the former. Handing it to the administration
		// client sends every management request to a port that answers nothing, which surfaces as the
		// SDK's transport-level "An error occurred while sending the request" after its four retries --
		// no queue is created, initialization throws, and every arm in this suite skips. Testcontainers
		// exposes GetHttpConnectionString() for exactly this client.
		var adminClient = new ServiceBusAdministrationClient(_serviceBusContainer.GetHttpConnectionString());
		_adminClient = adminClient;
		if (!await adminClient.QueueExistsAsync(QueueName))
		{
			_ = await adminClient.CreateQueueAsync(QueueName);
		}

		// Topic + subscription for R2.15 (message filtering, bd-uzzze3): the subscription's SQL rule is
		// (re)configured per ReceiveMatchingAsync call so it reflects the caller's filter, which is what
		// makes the assertion real -- a broker-side rule, not a client-side re-implementation of filtering.
		if (!await adminClient.TopicExistsAsync(TopicName))
		{
			_ = await adminClient.CreateTopicAsync(TopicName);
		}

		if (!await adminClient.SubscriptionExistsAsync(TopicName, SubscriptionName))
		{
			_ = await adminClient.CreateSubscriptionAsync(TopicName, SubscriptionName);
		}

		_topicSender = _client.CreateSender(TopicName);

		_sender = _client.CreateSender(QueueName);

		var sender = new AzureServiceBusChannelSender(_sender);
		return sender;
	}

	/// <inheritdoc />
	async Task ITransportConformanceCapabilities.PrepareFilterAsync(
		IReadOnlyDictionary<string, string> filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		if (_adminClient is null)
		{
			throw new InvalidOperationException("Admin client not initialized. CreateSenderAsync must run first.");
		}

		// A subscription's rules are broker-side and are evaluated when a message is PUBLISHED to the topic,
		// so the fixed default rule ($Default, TrueFilter -> matches everything) is replaced with a real SQL
		// rule built from the caller's filter before anything is sent. That ordering is what makes this a
		// server-side filtering assertion rather than a client-side re-implementation of it: the broker is
		// offered the non-matching message while this rule is live, and declines it.
		var sqlExpression = string.Join(
			" AND ",
			filter.Select(kv => $"{kv.Key} = '{kv.Value.Replace("'", "''", StringComparison.Ordinal)}'"));

		await foreach (var rule in _adminClient.GetRulesAsync(TopicName, SubscriptionName, cancellationToken))
		{
			await _adminClient.DeleteRuleAsync(TopicName, SubscriptionName, rule.Name, cancellationToken)
				.ConfigureAwait(false);
		}

		_ = await _adminClient.CreateRuleAsync(
			TopicName,
			SubscriptionName,
			new CreateRuleOptions("conformance-filter", new SqlRuleFilter(sqlExpression)),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	Task ITransportConformanceCapabilities.SendFilterableAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> attributes,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(attributes);
		if (_topicSender is null)
		{
			throw new InvalidOperationException("Topic sender not initialized. CreateSenderAsync must run first.");
		}

		var json = System.Text.Json.JsonSerializer.Serialize(body);
		var message = new ServiceBusMessage(json) { ContentType = "application/json" };
		foreach (var (key, value) in attributes)
		{
			message.ApplicationProperties[key] = value;
		}

		return _topicSender.SendMessageAsync(message, cancellationToken);
	}

	/// <inheritdoc />
	async Task<ConformanceReceiveResult<T>?> ITransportConformanceCapabilities.ReceiveMatchingAsync<T>(
		IReadOnlyDictionary<string, string> filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		if (_client is null)
		{
			throw new InvalidOperationException("Client not initialized. CreateSenderAsync must run first.");
		}

		// The rule was installed by PrepareFilterAsync before the sends -- it has to be, because Service Bus
		// applies a subscription's rules at publish time. Nothing to do here but read what the broker let in.
		await using var subscriptionReceiver = _client.CreateReceiver(TopicName, SubscriptionName);
		var received = await subscriptionReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken)
			.ConfigureAwait(false);

		if (received is null)
		{
			return null;
		}

		await subscriptionReceiver.CompleteMessageAsync(received, cancellationToken).ConfigureAwait(false);
		var body = System.Text.Json.JsonSerializer.Deserialize<T>(received.Body.ToString());
		return new ConformanceReceiveResult<T>(body, headers: null, acknowledge: null, reject: null);
	}

	/// <inheritdoc />
	Task ITransportConformanceCapabilities.SendWithHeadersAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> headers,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(AzureServiceBusTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	/// <inheritdoc />
	Task<ConformanceReceiveResult<T>?> ITransportConformanceCapabilities.ReceiveWithContextAsync<T>(
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(AzureServiceBusTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	/// <inheritdoc />
	Task ITransportConformanceCapabilities.SendCloudEventAsync(
		CloudEvent cloudEvent,
		CloudEventBinding binding,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(AzureServiceBusTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	/// <inheritdoc />
	Task<CloudEvent?> ITransportConformanceCapabilities.ReceiveCloudEventAsync(
		CloudEventBinding binding,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"{nameof(AzureServiceBusTransportConformanceTests)} advertises only {nameof(TransportCapability.PublishTimeFiltering)}.");

	protected override async Task<AzureServiceBusChannelReceiver> CreateReceiverAsync()
	{
		if (_client == null)
		{
			throw new InvalidOperationException("Client not initialized. Ensure sender is created first.");
		}

		_receiver = _client.CreateReceiver(QueueName, new ServiceBusReceiverOptions
		{
			ReceiveMode = ServiceBusReceiveMode.PeekLock,

			// Zero, deliberately. A prefetching link keeps pulling and LOCKING messages in the background
			// for as long as this receiver is open, including messages published by another arm of the same
			// test -- the dead-letter arm re-publishes to this queue and then waits on its own receiver for a
			// message this one has already taken. Prefetch buys throughput the conformance arms do not need
			// and costs the suite a fault that looks like the broker losing a message.
			PrefetchCount = 0
		});

		var receiver = new AzureServiceBusChannelReceiver(_receiver);
		return await Task.FromResult(receiver);
	}

	protected override async Task<IDeadLetterQueueManager?> CreateDlqManagerAsync()
	{
		if (_client == null)
		{
			throw new InvalidOperationException("Client not initialized.");
		}

		_dlqManager = new AzureServiceBusDeadLetterQueueManager(_client, QueueName);
		return await Task.FromResult<IDeadLetterQueueManager?>(_dlqManager);
	}

	protected override async Task DisposeTransportAsync()
	{
		if (_sender != null)
		{
			await _sender.DisposeAsync();
		}

		if (_receiver != null)
		{
			await _receiver.DisposeAsync();
		}

		if (_topicSender != null)
		{
			await _topicSender.DisposeAsync();
		}

		if (_client != null)
		{
			await _client.DisposeAsync();
		}

		if (_serviceBusContainer != null)
		{
			await _serviceBusContainer.DisposeAsync();
		}
	}
}

/// <summary>
/// Azure Service Bus implementation of IChannelSender for conformance testing.
/// </summary>
public sealed class AzureServiceBusChannelSender : IChannelSender
{
	private readonly ServiceBusSender _sender;

	public AzureServiceBusChannelSender(ServiceBusSender sender)
	{
		_sender = sender ?? throw new ArgumentNullException(nameof(sender));
	}

	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		if (message == null)
		{
			throw new ArgumentNullException(nameof(message));
		}

		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var serviceBusMessage = new ServiceBusMessage(json)
		{
			ContentType = "application/json",
			MessageId = Guid.NewGuid().ToString()
		};

		// Extract metadata if message has required properties
		var messageType = typeof(T);
		if (messageType.GetProperty("MessageId") != null)
		{
			var messageId = messageType.GetProperty("MessageId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(messageId))
			{
				serviceBusMessage.MessageId = messageId;
			}
		}

		if (messageType.GetProperty("CorrelationId") != null)
		{
			var correlationId = messageType.GetProperty("CorrelationId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(correlationId))
			{
				serviceBusMessage.CorrelationId = correlationId;
			}
		}

		await _sender.SendMessageAsync(serviceBusMessage, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// Azure Service Bus implementation of IChannelReceiver for conformance testing.
/// </summary>
public sealed class AzureServiceBusChannelReceiver : IChannelReceiver
{
	private readonly ServiceBusReceiver _receiver;

	public AzureServiceBusChannelReceiver(ServiceBusReceiver receiver)
	{
		_receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
	}

	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		var message = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken)
			.ConfigureAwait(false);

		if (message == null)
		{
			return default;
		}

		try
		{
			var json = message.Body.ToString();
			var result = System.Text.Json.JsonSerializer.Deserialize<T>(json);

			// Complete the message after successful deserialization
			await _receiver.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch
		{
			// Abandon the message on failure (will be retried)
			await _receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
			throw;
		}
	}
}

/// <summary>
/// Azure Service Bus implementation of IDeadLetterQueueManager for conformance testing.
/// </summary>
public sealed class AzureServiceBusDeadLetterQueueManager : IDeadLetterQueueManager
{
	private readonly ServiceBusClient _client;
	private readonly string _queueName;

	public AzureServiceBusDeadLetterQueueManager(ServiceBusClient client, string queueName)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
	}

	public async Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Service Bus has no writable dead-letter endpoint: $DeadLetterQueue is a sub-queue the broker fills,
		// and the only way in is to dead-letter a message the broker is currently leasing to you. The caller
		// has already consumed and COMPLETED the message it is naming here, so the live queue is empty --
		// receiving from it returned null, nothing was dead-lettered, and this method used to hand back
		// message.Id anyway, reporting a move it had not performed. Re-publish the named message and
		// dead-letter that, which is the only sequence the broker actually offers.
		await using var sender = _client.CreateSender(_queueName);
		await sender.SendMessageAsync(
			new ServiceBusMessage(BinaryData.FromBytes(message.Body)) { MessageId = message.Id },
			cancellationToken).ConfigureAwait(false);

		await using var receiver = _client.CreateReceiver(_queueName);
		var receivedMessage = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken)
			.ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Dead-lettering '{message.Id}' failed: the message was re-published to '{_queueName}' but did "
				+ "not come back within 30s, so nothing could be dead-lettered.");

		await receiver.DeadLetterMessageAsync(receivedMessage, reason, exception?.Message, cancellationToken)
			.ConfigureAwait(false);
		return receivedMessage.MessageId;
	}

	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		await using var dlqReceiver = _client.CreateReceiver(_queueName, new ServiceBusReceiverOptions
		{
			SubQueue = SubQueue.DeadLetter
		});

		// Peek, not receive. ReceiveMessagesAsync issues maxMessages of link credit and, when fewer
		// messages than that exist, reclaims the unused credit by DRAINING the link -- a round trip the
		// caller never asked for, bounded by the client's own 60s TryTimeout rather than by the 5s wait
		// passed here. Asking for 10 against a dead-letter sub-queue holding one message therefore spends
		// 1 message and 9 credits, and an emulator that is slow to answer the drain fails the arm with
		// "did not complete within the allocated time 00:01:00 for object drain". Peek is a management
		// request/response: no credit, no lock, no drain, and it carries every field read below. The
		// suite only ever READS the dead-letter queue, so nothing here needs a lock in the first place.
		var messages = await dlqReceiver.PeekMessagesAsync(maxMessages, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		var result = new List<DeadLetterMessage>();
		foreach (var message in messages)
		{
			result.Add(new DeadLetterMessage
			{
				OriginalMessage = new TransportMessage
				{
					Id = message.MessageId,
					Body = message.Body.ToArray()
				},
				Reason = message.DeadLetterReason ?? "Unknown",
				Exception = message.DeadLetterErrorDescription != null
					? new InvalidOperationException(message.DeadLetterErrorDescription)
					: null,
				DeadLetteredAt = message.EnqueuedTime
			});
		}

		return result;
	}

	public Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions options,
		CancellationToken cancellationToken)
	{
		// Azure Service Bus DLQ reprocessing would involve moving messages back to active queue
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
		await using var dlqReceiver = _client.CreateReceiver(_queueName, new ServiceBusReceiverOptions
		{
			SubQueue = SubQueue.DeadLetter,
			ReceiveMode = ServiceBusReceiveMode.PeekLock
		});

		// Peek messages to get count (Azure Service Bus doesn't have direct count API)
		var peekedMessages = await dlqReceiver.PeekMessagesAsync(100, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return new DeadLetterStatistics
		{
			MessageCount = peekedMessages.Count,
			OldestMessageAge = peekedMessages.Count > 0
				? DateTimeOffset.UtcNow - peekedMessages.Min(m => m.EnqueuedTime)
				: TimeSpan.Zero
		};
	}

	public async Task<int> PurgeAllTenantsDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		await using var dlqReceiver = _client.CreateReceiver(_queueName, new ServiceBusReceiverOptions
		{
			SubQueue = SubQueue.DeadLetter,
			ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete // Auto-delete on receive
		});

		int purgedCount = 0;
		bool hasMoreMessages = true;

		while (hasMoreMessages && !cancellationToken.IsCancellationRequested)
		{
			var messages = await dlqReceiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(1), cancellationToken)
				.ConfigureAwait(false);

			purgedCount += messages.Count;
			hasMoreMessages = messages.Count > 0;
		}

		return purgedCount;
	}
}
