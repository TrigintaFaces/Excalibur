// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Testcontainers.PubSub;

namespace Excalibur.Dispatch.Integration.Tests.Transport.GooglePubSub;

/// <summary>
/// Integration tests for Google Pub/Sub publisher operations.
/// Verifies message publishing to a real Pub/Sub emulator container, including
/// single publishes, batch publishes, attribute support, and data round-tripping.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "GooglePubSub")]
[Trait("Component", "Transport")]
[Collection(GooglePubSubTransportCollection.Name)]
public sealed class GooglePubSubPublisherIntegrationShould : IAsyncLifetime
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
	public async Task PublishMessage_DeliversToTopic()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"test-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		// Act
		var response = await _publisherApi.PublishAsync(topicName, [new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("Hello, Pub/Sub!"),
		}]).ConfigureAwait(false);

		// Assert — pull and verify the message arrived
		var pullResponse = await _subscriberApi.PullAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);

		pullResponse.ReceivedMessages.Count.ShouldBe(1);
		pullResponse.ReceivedMessages[0].Message.Data.ToStringUtf8().ShouldBe("Hello, Pub/Sub!");
		response.MessageIds.Count.ShouldBe(1);
	}

	[SkippableFact]
	public async Task PublishBatchMessages_AllDeliverSuccessfully()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"test-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		const int batchSize = 5;
		var messages = new List<PubsubMessage>();
		for (var i = 0; i < batchSize; i++)
		{
			messages.Add(new PubsubMessage
			{
				Data = ByteString.CopyFromUtf8($"Batch message {i}"),
			});
		}

		// Act
		var response = await _publisherApi.PublishAsync(topicName, messages).ConfigureAwait(false);

		// Assert
		response.MessageIds.Count.ShouldBe(batchSize);

		var pullResponse = await _subscriberApi.PullAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);
		pullResponse.ReceivedMessages.Count.ShouldBe(batchSize);
	}

	[SkippableFact]
	public async Task PublishMessageWithAttributes_AttributesPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"test-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		var message = new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("message-with-attrs"),
			Attributes =
			{
				["correlation-id"] = "corr-abc-123",
				["message-type"] = "OrderCreated",
				["custom-header"] = "custom-value",
			},
		};

		// Act
		await _publisherApi.PublishAsync(topicName, [message]).ConfigureAwait(false);

		// Assert
		var pullResponse = await _subscriberApi.PullAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);

		pullResponse.ReceivedMessages.Count.ShouldBe(1);
		var received = pullResponse.ReceivedMessages[0].Message;
		received.Attributes["correlation-id"].ShouldBe("corr-abc-123");
		received.Attributes["message-type"].ShouldBe("OrderCreated");
		received.Attributes["custom-header"].ShouldBe("custom-value");
	}

	[SkippableFact]
	public async Task PublishEmptyData_Succeeds()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"test-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		var message = new PubsubMessage
		{
			Data = ByteString.Empty,
			Attributes =
			{
				["marker"] = "empty-body-test",
			},
		};

		// Act
		var response = await _publisherApi.PublishAsync(topicName, [message]).ConfigureAwait(false);

		// Assert
		response.MessageIds.Count.ShouldBe(1);

		var pullResponse = await _subscriberApi.PullAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);
		pullResponse.ReceivedMessages.Count.ShouldBe(1);
		pullResponse.ReceivedMessages[0].Message.Data.Length.ShouldBe(0);
	}

	[SkippableFact]
	public async Task MessageData_RoundTrips()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"test-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
		var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		var jsonPayload = "{\"orderId\":42,\"customer\":\"John Doe\",\"total\":99.99,\"items\":[\"widget\",\"gadget\"]}";
		var message = new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8(jsonPayload),
		};

		// Act
		await _publisherApi.PublishAsync(topicName, [message]).ConfigureAwait(false);

		// Assert
		var pullResponse = await _subscriberApi.PullAsync(subscriptionName, maxMessages: 10).ConfigureAwait(false);

		pullResponse.ReceivedMessages.Count.ShouldBe(1);
		pullResponse.ReceivedMessages[0].Message.Data.ToStringUtf8().ShouldBe(jsonPayload);
	}

	[SkippableFact]
	public async Task PublishMessage_ReturnsMessageId()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);

		var message = new PubsubMessage
		{
			Data = ByteString.CopyFromUtf8("test message for ID"),
		};

		// Act
		var response = await _publisherApi.PublishAsync(topicName, [message]).ConfigureAwait(false);

		// Assert
		response.MessageIds.Count.ShouldBe(1);
		response.MessageIds[0].ShouldNotBeNullOrWhiteSpace();
	}

	[SkippableFact]
	public async Task TopicCreation_IsSuccessful()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var topicId = $"test-topic-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);

		// Act
		var topic = await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);

		// Assert
		topic.ShouldNotBeNull();
		topic.TopicName.TopicId.ShouldBe(topicId);
		topic.TopicName.ProjectId.ShouldBe(ProjectId);

		// Verify we can get the topic
		var retrieved = await _publisherApi.GetTopicAsync(topicName).ConfigureAwait(false);
		retrieved.ShouldNotBeNull();
		retrieved.TopicName.TopicId.ShouldBe(topicId);
	}
}
