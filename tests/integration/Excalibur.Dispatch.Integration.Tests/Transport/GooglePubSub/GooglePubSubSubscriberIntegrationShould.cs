// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;

using Testcontainers.PubSub;

namespace Excalibur.Dispatch.Integration.Tests.Transport.GooglePubSub;

/// <summary>
/// Integration tests for Google Pub/Sub subscriber operations.
/// Verifies message pulling, acknowledgement, nack, fan-out, and ordering
/// against a real Pub/Sub emulator container.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "GooglePubSub")]
[Trait("Component", "Transport")]
[Collection(GooglePubSubTransportCollection.Name)]
public sealed class GooglePubSubSubscriberIntegrationShould : IAsyncLifetime
{
	private const string ProjectId = "test-project";

	private PubSubContainer? _container;
	private PublisherServiceApiClient? _publisherApi;
	private SubscriberServiceApiClient? _subscriberApi;
	private bool _dockerAvailable;

	public async Task InitializeAsync()
	{
		try
		{
			_container = new PubSubBuilder().Build();
			await _container.StartAsync().ConfigureAwait(false);

			var endpoint = _container.GetEmulatorEndpoint();
			Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", endpoint);

			_publisherApi = await new PublisherServiceApiClientBuilder
			{
				EmulatorDetection = EmulatorDetection.EmulatorOnly,
			}.BuildAsync().ConfigureAwait(false);

			_subscriberApi = await new SubscriberServiceApiClientBuilder
			{
				EmulatorDetection = EmulatorDetection.EmulatorOnly,
			}.BuildAsync().ConfigureAwait(false);

			// Verify the emulator is actually responding to gRPC calls.
			// The container may start but the emulator might not support the HTTP/2 handshake
			// required by the current gRPC client version.
			var probeTopic = TopicName.FromProjectTopic(ProjectId, $"probe-{Guid.NewGuid():N}");
			await _publisherApi.CreateTopicAsync(probeTopic).ConfigureAwait(false);
			await _publisherApi.DeleteTopicAsync(probeTopic).ConfigureAwait(false);

			_dockerAvailable = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Docker initialization failed: {ex.Message}");
			_dockerAvailable = false;
		}
	}

	public async Task DisposeAsync()
	{
		Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", null);

		try
		{
			if (_container is not null)
			{
				await _container.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch
		{
			// Best effort cleanup
		}
	}

	[SkippableFact]
	public async Task PullMessages_FromPopulatedSubscription()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var (topicName, subscriptionName) = await CreateTopicAndSubscriptionAsync().ConfigureAwait(false);

		await _publisherApi!.PublishAsync(topicName, [new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("pull-test-message"),
		}]).ConfigureAwait(false);

		// Act
		var pullResponse = await PullWithTransientRetryAsync(
			subscriptionName,
			maxMessages: 10,
			returnImmediately: true).ConfigureAwait(false);

		// Assert
		pullResponse.ReceivedMessages.Count.ShouldBe(1);
		pullResponse.ReceivedMessages[0].Message.Data.ToStringUtf8().ShouldBe("pull-test-message");
	}

	[SkippableFact]
	public async Task PullFromEmptySubscription_ReturnsEmptyList()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var (_, subscriptionName) = await CreateTopicAndSubscriptionAsync().ConfigureAwait(false);

		// Act — pull immediately with no messages published
		var pullResponse = await PullWithTransientRetryAsync(
			subscriptionName,
			maxMessages: 10,
			returnImmediately: true).ConfigureAwait(false);

		// Assert
		pullResponse.ReceivedMessages.Count.ShouldBe(0);
	}

	[SkippableFact]
	public async Task AcknowledgeMessage_RemovesFromSubscription()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var (topicName, subscriptionName) = await CreateTopicAndSubscriptionAsync().ConfigureAwait(false);

		await _publisherApi!.PublishAsync(topicName, [new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("ack-test-message"),
		}]).ConfigureAwait(false);

		var pullResponse = await PullWithTransientRetryAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);
		pullResponse.ReceivedMessages.Count.ShouldBe(1);

		var ackId = pullResponse.ReceivedMessages[0].AckId;

		// Act — acknowledge the message
		await _subscriberApi.AcknowledgeAsync(subscriptionName, [ackId]).ConfigureAwait(false);

		// Assert — pulling again should return no messages
		var secondPull = await PullWithTransientRetryAsync(
			subscriptionName,
			maxMessages: 10,
			returnImmediately: true).ConfigureAwait(false);
		secondPull.ReceivedMessages.Count.ShouldBe(0);
	}

	[SkippableFact]
	public async Task NackMessage_MakesMessageReappear()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var (topicName, subscriptionName) = await CreateTopicAndSubscriptionAsync().ConfigureAwait(false);

		await _publisherApi!.PublishAsync(topicName, [new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("nack-test-message"),
		}]).ConfigureAwait(false);

		var pullResponse = await PullWithTransientRetryAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);
		pullResponse.ReceivedMessages.Count.ShouldBe(1);

		var ackId = pullResponse.ReceivedMessages[0].AckId;

		// Act — nack the message by setting ack deadline to 0
		await _subscriberApi.ModifyAckDeadlineAsync(subscriptionName, [ackId], 0).ConfigureAwait(false);

		// Assert — message should reappear (eventual redelivery in emulator can vary under CI load)
		var deadline = DateTime.UtcNow.AddSeconds(10);
		PullResponse? secondPull = null;
		while (DateTime.UtcNow < deadline)
		{
			secondPull = await PullWithTransientRetryAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);
			if (secondPull.ReceivedMessages.Count > 0)
			{
				break;
			}

			await Task.Delay(200).ConfigureAwait(false);
		}

		secondPull.ShouldNotBeNull();
		secondPull.ReceivedMessages.Count.ShouldBe(1);
		secondPull.ReceivedMessages[0].Message.Data.ToStringUtf8().ShouldBe("nack-test-message");
	}

	[SkippableFact]
	public async Task PullWithAttributes_AttributesPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var (topicName, subscriptionName) = await CreateTopicAndSubscriptionAsync().ConfigureAwait(false);

		var message = new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("attrs-test"),
			Attributes =
			{
				["event-type"] = "OrderCreated",
				["correlation-id"] = "corr-xyz-789",
				["source"] = "integration-test",
			},
		};

		await _publisherApi!.PublishAsync(topicName, [message]).ConfigureAwait(false);

		// Act
		var pullResponse = await PullWithTransientRetryAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);

		// Assert
		pullResponse.ReceivedMessages.Count.ShouldBe(1);
		var received = pullResponse.ReceivedMessages[0].Message;
		received.Attributes["event-type"].ShouldBe("OrderCreated");
		received.Attributes["correlation-id"].ShouldBe("corr-xyz-789");
		received.Attributes["source"].ShouldBe("integration-test");
	}

	[SkippableFact]
	public async Task MultipleSubscriptions_ReceiveSameMessage()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange — create one topic with two subscriptions (fan-out)
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);

		var sub1Id = $"test-sub-1-{Guid.NewGuid():N}";
		var sub2Id = $"test-sub-2-{Guid.NewGuid():N}";
		var sub1Name = SubscriptionName.FromProjectSubscription(ProjectId, sub1Id);
		var sub2Name = SubscriptionName.FromProjectSubscription(ProjectId, sub2Id);

		await _subscriberApi!.CreateSubscriptionAsync(sub1Name, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);
		await _subscriberApi.CreateSubscriptionAsync(sub2Name, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		// Act — publish a single message
		await _publisherApi.PublishAsync(topicName, [new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("fan-out-message"),
		}]).ConfigureAwait(false);

		// Assert — both subscriptions should receive the message
		var pull1 = await PullWithTransientRetryAsync(sub1Name, maxMessages: 10).ConfigureAwait(false);
		var pull2 = await PullWithTransientRetryAsync(sub2Name, maxMessages: 10).ConfigureAwait(false);

		pull1.ReceivedMessages.Count.ShouldBe(1);
		pull2.ReceivedMessages.Count.ShouldBe(1);
		pull1.ReceivedMessages[0].Message.Data.ToStringUtf8().ShouldBe("fan-out-message");
		pull2.ReceivedMessages[0].Message.Data.ToStringUtf8().ShouldBe("fan-out-message");
	}

	[SkippableFact]
	public async Task MessageOrdering_WithOrderingKey()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange — create topic with message ordering enabled
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"test-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(new Subscription
		{
			SubscriptionName = subscriptionName,
			TopicAsTopicName = topicName,
			AckDeadlineSeconds = 10,
			EnableMessageOrdering = true,
		}).ConfigureAwait(false);

		// Act — publish messages with the same ordering key
		var messages = new List<PubsubMessage>();
		for (var i = 0; i < 5; i++)
		{
			messages.Add(new PubsubMessage
			{
				Data = ByteString.CopyFromUtf8($"ordered-{i}"),
				OrderingKey = "same-key",
			});
		}

		await _publisherApi.PublishAsync(topicName, messages).ConfigureAwait(false);

		// Assert — messages should arrive in order
		var pullResponse = await PullWithTransientRetryAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);

		pullResponse.ReceivedMessages.Count.ShouldBe(5);
		for (var i = 0; i < 5; i++)
		{
			pullResponse.ReceivedMessages[i].Message.Data.ToStringUtf8().ShouldBe($"ordered-{i}");
			pullResponse.ReceivedMessages[i].Message.OrderingKey.ShouldBe("same-key");
		}
	}

	private async Task<(TopicName TopicName, SubscriptionName SubscriptionName)> CreateTopicAndSubscriptionAsync()
	{
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"test-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		return (topicName, subscriptionName);
	}

	private async Task<PullResponse> PullWithTransientRetryAsync(
		SubscriptionName subscriptionName,
		int maxMessages,
		bool returnImmediately = false,
		int maxAttempts = 4)
	{
		Exception? lastException = null;

		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				return await _subscriberApi!.PullAsync(
					subscriptionName,
					maxMessages: maxMessages,
					returnImmediately: returnImmediately).ConfigureAwait(false);
			}
			catch (Exception ex) when (IsTransientPullFailure(ex) && attempt < maxAttempts)
			{
				lastException = ex;
				await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt)).ConfigureAwait(false);
			}
		}

		throw lastException ?? new InvalidOperationException("Pull operation failed without exception details.");
	}

	private static bool IsTransientPullFailure(Exception ex)
	{
		if (ex is RpcException rpcException)
		{
			return rpcException.StatusCode is StatusCode.Cancelled or StatusCode.Unavailable or StatusCode.DeadlineExceeded;
		}

		var current = ex;
		while (current is not null)
		{
			if (current is HttpRequestException)
			{
				return true;
			}

			current = current.InnerException!;
		}

		return false;
	}
}
