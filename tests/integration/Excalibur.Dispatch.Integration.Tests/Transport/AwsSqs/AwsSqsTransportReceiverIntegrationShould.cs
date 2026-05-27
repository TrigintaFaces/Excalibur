// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS.Model;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AwsSqs;

/// <summary>
/// Integration tests for AWS SQS transport receiver operations.
/// Verifies message consumption from a real SQS queue via LocalStack, including
/// receive, delete, visibility timeout, attributes, max messages, encoding, and
/// concurrent consumers.
/// </summary>
/// <remarks>
/// Container lifecycle is managed by <see cref="AwsSqsContainerFixture"/> via the
/// xUnit collection fixture pattern. The fixture is shared across all test classes
/// in the <see cref="ContainerCollections.AwsSqs"/> collection.
/// </remarks>
[Collection(ContainerCollections.AwsSqs)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "AwsSqs")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsSqsTransportReceiverIntegrationShould
{
	private readonly AwsSqsContainerFixture _fixture;

	public AwsSqsTransportReceiverIntegrationShould(AwsSqsContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private async Task<string> CreateStandardQueueAsync(string? queueName = null, int? visibilityTimeout = null)
	{
		queueName ??= $"test-queue-{Guid.NewGuid():N}";
		var request = new CreateQueueRequest
		{
			QueueName = queueName,
		};

		if (visibilityTimeout.HasValue)
		{
			request.Attributes = new Dictionary<string, string>
			{
				["VisibilityTimeout"] = visibilityTimeout.Value.ToString(),
			};
		}

		var response = await _fixture.SqsClient.CreateQueueAsync(request).ConfigureAwait(false);
		return response.QueueUrl;
	}

	private async Task SendMessageAsync(string queueUrl, string body, Dictionary<string, MessageAttributeValue>? attributes = null)
	{
		var request = new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = body,
		};

		if (attributes is not null)
		{
			request.MessageAttributes = attributes;
		}

		await _fixture.SqsClient.SendMessageAsync(request).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReceiveMessages_FromPopulatedQueue()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		var expectedBody = "Hello from SQS receiver test";
		await SendMessageAsync(queueUrl, expectedBody).ConfigureAwait(false);

		// Act
		var receiveResponse = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		// Assert
		receiveResponse.Messages.ShouldNotBeEmpty();
		receiveResponse.Messages.Count.ShouldBe(1);
		receiveResponse.Messages[0].Body.ShouldBe(expectedBody);
		receiveResponse.Messages[0].MessageId.ShouldNotBeNullOrWhiteSpace();
		receiveResponse.Messages[0].ReceiptHandle.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task ReceiveFromEmptyQueue_ReturnsEmptyList()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);

		// Act — short poll (no wait) on empty queue
		var receiveResponse = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 0,
		}).ConfigureAwait(false);

		// Assert — SQS/LocalStack may return null or empty list when no messages
		(receiveResponse.Messages ?? []).ShouldBeEmpty();
	}

	[Fact]
	public async Task DeleteMessage_RemovesFromQueue()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		await SendMessageAsync(queueUrl, "delete-me").ConfigureAwait(false);

		var receiveResponse = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		receiveResponse.Messages.ShouldNotBeEmpty();
		var receiptHandle = receiveResponse.Messages[0].ReceiptHandle;

		// Act — delete the message
		await _fixture.SqsClient.DeleteMessageAsync(queueUrl, receiptHandle).ConfigureAwait(false);

		// Assert — queue should be empty now
		var afterDelete = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 0,
		}).ConfigureAwait(false);

		(afterDelete.Messages ?? []).ShouldBeEmpty();
	}

	[Fact]
	public async Task ChangeVisibility_MakesMessageReappear()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange — create a queue with a long default visibility timeout
		var queueUrl = await CreateStandardQueueAsync(visibilityTimeout: 30).ConfigureAwait(false);
		await SendMessageAsync(queueUrl, "visibility-test").ConfigureAwait(false);

		// Receive the message (it becomes invisible for 30s)
		var receiveResponse = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		receiveResponse.Messages.ShouldNotBeEmpty();
		var receiptHandle = receiveResponse.Messages[0].ReceiptHandle;

		// Act — change visibility timeout to 0 to make it immediately visible again
		await _fixture.SqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
		{
			QueueUrl = queueUrl,
			ReceiptHandle = receiptHandle,
			VisibilityTimeout = 0,
		}).ConfigureAwait(false);

		// Assert — message should be receivable again
		var reReceive = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		reReceive.Messages.ShouldNotBeEmpty();
		reReceive.Messages[0].Body.ShouldBe("visibility-test");
	}

	[Fact]
	public async Task ReceiveWithAttributes_AttributesPreserved()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		var attributes = new Dictionary<string, MessageAttributeValue>
		{
			["EventType"] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = "UserCreated",
			},
			["CorrelationId"] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = "corr-001",
			},
		};

		await SendMessageAsync(queueUrl, "{\"userId\": 1}", attributes).ConfigureAwait(false);

		// Act
		var receiveResponse = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
			MessageAttributeNames = ["All"],
		}).ConfigureAwait(false);

		// Assert
		receiveResponse.Messages.ShouldNotBeEmpty();
		var received = receiveResponse.Messages[0];
		received.MessageAttributes.ShouldContainKey("EventType");
		received.MessageAttributes["EventType"].StringValue.ShouldBe("UserCreated");
		received.MessageAttributes.ShouldContainKey("CorrelationId");
		received.MessageAttributes["CorrelationId"].StringValue.ShouldBe("corr-001");
	}

	[Fact]
	public async Task ReceiveMaxMessages_RespectsLimit()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange — send 5 messages
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		for (var i = 0; i < 5; i++)
		{
			await SendMessageAsync(queueUrl, $"msg-{i}").ConfigureAwait(false);
		}

		// Act — receive with max 3
		var receiveResponse = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 3,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		// Assert
		receiveResponse.Messages.Count.ShouldBeInRange(1, 3);
	}

	[Fact]
	public async Task MessageBody_PreservesEncoding()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange — UTF-8 body with special characters
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		var unicodeBody = "{\"name\":\"caf\u00e9\",\"emoji\":\"\u2728\",\"cjk\":\"\u4f60\u597d\"}";

		await SendMessageAsync(queueUrl, unicodeBody).ConfigureAwait(false);

		// Act
		var receiveResponse = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		// Assert
		receiveResponse.Messages.ShouldNotBeEmpty();
		receiveResponse.Messages[0].Body.ShouldBe(unicodeBody);
	}

	[Fact]
	public async Task MultipleConsumers_CanReceive()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange — send 4 messages, use short visibility so they don't overlap
		var queueUrl = await CreateStandardQueueAsync(visibilityTimeout: 5).ConfigureAwait(false);
		for (var i = 0; i < 4; i++)
		{
			await SendMessageAsync(queueUrl, $"concurrent-msg-{i}").ConfigureAwait(false);
		}

		// Act — two concurrent receive calls
		var task1 = _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 2,
			WaitTimeSeconds = 5,
		});
		var task2 = _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 2,
			WaitTimeSeconds = 5,
		});

		var results = await Task.WhenAll(task1, task2).ConfigureAwait(false);

		// Assert — both receive calls should get messages (total >= 2)
		var messages0 = results[0].Messages ?? [];
		var messages1 = results[1].Messages ?? [];
		var totalReceived = messages0.Count + messages1.Count;
		totalReceived.ShouldBeGreaterThanOrEqualTo(2);

		// Verify message IDs are distinct across the two calls
		var allIds = messages0.Select(m => m.MessageId)
			.Concat(messages1.Select(m => m.MessageId))
			.ToList();
		allIds.Distinct().Count().ShouldBe(allIds.Count);
	}
}
