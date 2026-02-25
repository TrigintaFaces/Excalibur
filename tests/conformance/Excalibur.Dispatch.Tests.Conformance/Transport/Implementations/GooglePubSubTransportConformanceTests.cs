// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Testcontainers.PubSub;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for Google Pub/Sub transport using TestContainers.
/// Automatically provisions a Google Pub/Sub emulator container for testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "GooglePubSub")]
public sealed class GooglePubSubTransportConformanceTests
	: TransportConformanceTestBase<GooglePubSubChannelSender, GooglePubSubChannelReceiver>
{
	private const string TopicId = "conformance-test-topic";
	private const string SubscriptionId = "conformance-test-subscription";
	private const string ProjectId = "test-project";

	private PubSubContainer? _pubSubContainer;
	private PublisherClient? _publisher;
	private SubscriberClient? _subscriber;
	private GooglePubSubDeadLetterQueueManager? _dlqManager;

	protected override async Task<GooglePubSubChannelSender> CreateSenderAsync()
	{
		// Start Google Pub/Sub emulator container
		_pubSubContainer = new PubSubBuilder()
			.Build();

		await _pubSubContainer.StartAsync();

		// Set the emulator host environment variable (required for SDK to find emulator)
		var emulatorHost = _pubSubContainer.GetEmulatorEndpoint();
		Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", emulatorHost);

		// Create topic
		var topicName = new TopicName(ProjectId, TopicId);

		try
		{
			// Use EmulatorDetection.EmulatorOnly to skip credential validation
			var publisherServiceApiClient = await new PublisherServiceApiClientBuilder
			{
				EmulatorDetection = EmulatorDetection.EmulatorOnly
			}.BuildAsync().ConfigureAwait(false);
			_ = await publisherServiceApiClient.CreateTopicAsync(topicName).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Topic might already exist - ignore
		}

		// Create publisher with EmulatorDetection
		_publisher = await new PublisherClientBuilder
		{
			TopicName = topicName,
			EmulatorDetection = EmulatorDetection.EmulatorOnly
		}.BuildAsync().ConfigureAwait(false);

		return new GooglePubSubChannelSender(_publisher);
	}

	protected override async Task<GooglePubSubChannelReceiver> CreateReceiverAsync()
	{
		// Create subscription
		var topicName = new TopicName(ProjectId, TopicId);
		var subscriptionName = new SubscriptionName(ProjectId, SubscriptionId);

		try
		{
			// Use EmulatorDetection.EmulatorOnly to skip credential validation
			var subscriberServiceApiClient = await new SubscriberServiceApiClientBuilder
			{
				EmulatorDetection = EmulatorDetection.EmulatorOnly
			}.BuildAsync().ConfigureAwait(false);
			_ = await subscriberServiceApiClient.CreateSubscriptionAsync(
				subscriptionName,
				topicName,
				pushConfig: null,
				ackDeadlineSeconds: 60).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Subscription might already exist - ignore
		}

		// Create subscriber with EmulatorDetection
		_subscriber = await new SubscriberClientBuilder
		{
			SubscriptionName = subscriptionName,
			EmulatorDetection = EmulatorDetection.EmulatorOnly
		}.BuildAsync().ConfigureAwait(false);

		return new GooglePubSubChannelReceiver(_subscriber);
	}

	protected override async Task<IDeadLetterQueueManager?> CreateDlqManagerAsync()
	{
		if (_publisher == null)
		{
			throw new InvalidOperationException("Pub/Sub not initialized.");
		}

		// Create DLQ topic
		var dlqTopicName = new TopicName(ProjectId, $"{TopicId}-dlq");

		try
		{
			// Use EmulatorDetection.EmulatorOnly to skip credential validation
			var publisherServiceApiClient = await new PublisherServiceApiClientBuilder
			{
				EmulatorDetection = EmulatorDetection.EmulatorOnly
			}.BuildAsync().ConfigureAwait(false);
			_ = await publisherServiceApiClient.CreateTopicAsync(dlqTopicName).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Topic might already exist - ignore
		}

		// Create DLQ publisher with EmulatorDetection
		var dlqPublisher = await new PublisherClientBuilder
		{
			TopicName = dlqTopicName,
			EmulatorDetection = EmulatorDetection.EmulatorOnly
		}.BuildAsync().ConfigureAwait(false);

		_dlqManager = new GooglePubSubDeadLetterQueueManager(dlqPublisher);
		return _dlqManager;
	}

	protected override async Task DisposeTransportAsync()
	{
		if (_publisher != null)
		{
			await _publisher.ShutdownAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		}

		if (_subscriber != null)
		{
			try
			{
				await _subscriber.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// Subscriber may not have been started if no ReceiveAsync calls were made
			}
		}

		// Clear the environment variable
		Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", null);

		if (_pubSubContainer != null)
		{
			await _pubSubContainer.DisposeAsync();
		}
	}
}

/// <summary>
/// Google Pub/Sub implementation of IChannelSender for conformance testing.
/// </summary>
public sealed class GooglePubSubChannelSender : IChannelSender
{
	private readonly PublisherClient _publisher;

	public GooglePubSubChannelSender(PublisherClient publisher)
	{
		_publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
	}

	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		if (message == null)
		{
			throw new ArgumentNullException(nameof(message));
		}

		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var pubsubMessage = new PubsubMessage
		{
			Data = Google.Protobuf.ByteString.CopyFromUtf8(json),
			Attributes =
			{
				["ContentType"] = "application/json"
			}
		};

		// Extract metadata if available
		var messageType = typeof(T);
		if (messageType.GetProperty("MessageId") != null)
		{
			var messageId = messageType.GetProperty("MessageId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(messageId))
			{
				pubsubMessage.Attributes["MessageId"] = messageId;
			}
		}

		if (messageType.GetProperty("CorrelationId") != null)
		{
			var correlationId = messageType.GetProperty("CorrelationId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(correlationId))
			{
				pubsubMessage.Attributes["CorrelationId"] = correlationId;
			}
		}

		_ = await _publisher.PublishAsync(pubsubMessage).ConfigureAwait(false);
	}
}

/// <summary>
/// Google Pub/Sub implementation of IChannelReceiver for conformance testing.
/// </summary>
public sealed class GooglePubSubChannelReceiver : IChannelReceiver
{
	private readonly SubscriberClient _subscriber;
	private readonly System.Threading.Channels.Channel<PubsubMessage> _messageChannel;
	private Task? _subscriptionTask;

	public GooglePubSubChannelReceiver(SubscriberClient subscriber)
	{
		_subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
		_messageChannel = System.Threading.Channels.Channel.CreateUnbounded<PubsubMessage>();
	}

	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		// Start subscription if not already started
		if (_subscriptionTask == null)
		{
			_subscriptionTask = _subscriber.StartAsync((message, cancellationToken) =>
			{
				_ = _messageChannel.Writer.TryWrite(message);
				return Task.FromResult(SubscriberClient.Reply.Ack);
			});
		}

		// Wait for message from channel
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(TimeSpan.FromSeconds(30));

		try
		{
			var pubsubMessage = await _messageChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
			var json = pubsubMessage.Data.ToStringUtf8();
			return System.Text.Json.JsonSerializer.Deserialize<T>(json);
		}
		catch (OperationCanceledException)
		{
			return default;
		}
	}
}

/// <summary>
/// Google Pub/Sub implementation of IDeadLetterQueueManager for conformance testing.
/// </summary>
public sealed class GooglePubSubDeadLetterQueueManager : IDeadLetterQueueManager
{
	private readonly PublisherClient _dlqPublisher;
	private readonly List<DeadLetterMessage> _dlqMessages = new();

	public GooglePubSubDeadLetterQueueManager(PublisherClient dlqPublisher)
	{
		_dlqPublisher = dlqPublisher ?? throw new ArgumentNullException(nameof(dlqPublisher));
	}

	public async Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var pubsubMessage = new PubsubMessage
		{
			Data = Google.Protobuf.ByteString.CopyFromUtf8(json),
			Attributes =
			{
				["Reason"] = reason,
				["DeadLetteredAt"] = DateTimeOffset.UtcNow.ToString("O")
			}
		};

		if (exception != null)
		{
			pubsubMessage.Attributes["Exception"] = exception.Message;
		}

		var messageId = await _dlqPublisher.PublishAsync(pubsubMessage).ConfigureAwait(false);

		// Track locally for testing
		_dlqMessages.Add(new DeadLetterMessage
		{
			OriginalMessage = message,
			Reason = reason,
			Exception = exception,
			DeadLetteredAt = DateTimeOffset.UtcNow
		});

		return messageId;
	}

	public Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(
			_dlqMessages.Take(maxMessages).ToList());
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

	public Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		return Task.FromResult(new DeadLetterStatistics
		{
			MessageCount = _dlqMessages.Count,
			OldestMessageAge = _dlqMessages.Count > 0
				? DateTimeOffset.UtcNow - _dlqMessages.Min(m => m.DeadLetteredAt)
				: TimeSpan.Zero
		});
	}

	public Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		var count = _dlqMessages.Count;
		_dlqMessages.Clear();
		return Task.FromResult(count);
	}
}
