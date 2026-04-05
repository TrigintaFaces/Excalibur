// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

using Tests.Shared.Fixtures;

using Testcontainers.LocalStack;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AwsSqs;

/// <summary>
/// Integration tests for AWS SQS transport sender operations.
/// Verifies message sending to a real SQS queue via LocalStack, including
/// single sends, batch sends, message attributes, FIFO queues, and body round-tripping.
/// </summary>
/// <remarks>
/// Container lifecycle: a single static LocalStack container is shared across all
/// test instances in this class. This avoids per-test container churn that causes
/// resource exhaustion and disposal hangs on Ubuntu CI.
/// </remarks>
[Collection(ContainerCollections.AwsSqs)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "AwsSqs")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsSqsTransportSenderIntegrationShould : IAsyncLifetime, IDisposable
{
	// Static container shared across all test instances in this class.
	// Avoids per-test container creation that exhausts CI resources.
	private static readonly SemaphoreSlim s_initLock = new(1, 1);
	private static volatile bool s_initialized;
	private static volatile bool s_dockerAvailable;
	private static LocalStackContainer? s_container;
	private static AmazonSQSClient? s_sqsClient;

	private bool _dockerAvailable;

	public async Task InitializeAsync()
	{
		if (s_initialized)
		{
			_dockerAvailable = s_dockerAvailable;
			return;
		}

		await s_initLock.WaitAsync().ConfigureAwait(false);
		try
		{
			// Double-check after acquiring lock
			if (s_initialized)
			{
				_dockerAvailable = s_dockerAvailable;
				return;
			}

			try
			{
				s_container = new LocalStackBuilder()
					.WithImage("localstack/localstack:latest")
					.WithEnvironment("SERVICES", "sqs")
					.Build();
				using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				await s_container.StartAsync(startCts.Token).ConfigureAwait(false);

				var credentials = new BasicAWSCredentials("test", "test");
				var config = new AmazonSQSConfig
				{
					ServiceURL = s_container.GetConnectionString(),
				};
				s_sqsClient = new AmazonSQSClient(credentials, config);
				s_dockerAvailable = true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Docker initialization failed: {ex.Message}");
				s_dockerAvailable = false;
			}

			s_initialized = true;
		}
		finally
		{
			s_initLock.Release();
		}

		_dockerAvailable = s_dockerAvailable;
	}

	public void Dispose()
	{
		// Static resources are not disposed per-test; container lives for the class lifetime.
		// xUnit disposes the process at the end, which cleans up the container.
	}

	public Task DisposeAsync() => Task.CompletedTask;

	private async Task<string> CreateStandardQueueAsync(string? queueName = null)
	{
		queueName ??= $"test-queue-{Guid.NewGuid():N}";
		var response = await s_sqsClient!.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = queueName,
		}).ConfigureAwait(false);
		return response.QueueUrl;
	}

	private async Task<string> CreateFifoQueueAsync(string? queueName = null)
	{
		queueName ??= $"test-queue-{Guid.NewGuid():N}.fifo";
		var response = await s_sqsClient!.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = queueName,
			Attributes = new Dictionary<string, string>
			{
				["FifoQueue"] = "true",
				["ContentBasedDeduplication"] = "false",
			},
		}).ConfigureAwait(false);
		return response.QueueUrl;
	}

	[SkippableFact]
	public async Task SendMessage_DeliversToQueue()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		var messageBody = "Hello, SQS!";

		// Act
		var sendResponse = await s_sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = messageBody,
		}).ConfigureAwait(false);

		// Assert — receive and verify
		var receiveResponse = await s_sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		receiveResponse.Messages.ShouldNotBeEmpty();
		receiveResponse.Messages[0].Body.ShouldBe(messageBody);
		sendResponse.MessageId.ShouldNotBeNullOrWhiteSpace();
	}

	[SkippableFact]
	public async Task SendBatchMessages_AllDeliverSuccessfully()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		const int batchSize = 5;

		var entries = new List<SendMessageBatchRequestEntry>();
		for (var i = 0; i < batchSize; i++)
		{
			entries.Add(new SendMessageBatchRequestEntry
			{
				Id = $"msg-{i}",
				MessageBody = $"Batch message {i}",
			});
		}

		// Act
		var batchResponse = await s_sqsClient!.SendMessageBatchAsync(new SendMessageBatchRequest
		{
			QueueUrl = queueUrl,
			Entries = entries,
		}).ConfigureAwait(false);

		// Assert — Failed may be null when no failures (SDK/LocalStack behavior)
		(batchResponse.Failed ?? []).ShouldBeEmpty();
		batchResponse.Successful.Count.ShouldBe(batchSize);

		// Verify all messages arrived
		var receivedCount = 0;
		for (var attempt = 0; attempt < 3 && receivedCount < batchSize; attempt++)
		{
			var receiveResponse = await s_sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
			{
				QueueUrl = queueUrl,
				MaxNumberOfMessages = 10,
				WaitTimeSeconds = 5,
			}).ConfigureAwait(false);
			receivedCount += receiveResponse.Messages.Count;
		}

		receivedCount.ShouldBe(batchSize);
	}

	[SkippableFact]
	public async Task SendMessageWithAttributes_AttributesPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);

		var attributes = new Dictionary<string, MessageAttributeValue>
		{
			["EventType"] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = "OrderCreated",
			},
			["Priority"] = new MessageAttributeValue
			{
				DataType = "Number",
				StringValue = "5",
			},
		};

		// Act
		await s_sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = "{\"orderId\": 42}",
			MessageAttributes = attributes,
		}).ConfigureAwait(false);

		// Assert
		var receiveResponse = await s_sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
			MessageAttributeNames = ["All"],
		}).ConfigureAwait(false);

		receiveResponse.Messages.ShouldNotBeEmpty();
		var received = receiveResponse.Messages[0];
		received.MessageAttributes.ShouldContainKey("EventType");
		received.MessageAttributes["EventType"].StringValue.ShouldBe("OrderCreated");
		received.MessageAttributes.ShouldContainKey("Priority");
		received.MessageAttributes["Priority"].StringValue.ShouldBe("5");
	}

	[SkippableFact]
	public async Task SendMessageToFifoQueue_OrderPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateFifoQueueAsync().ConfigureAwait(false);
		const int messageCount = 5;

		// Act — send messages sequentially with the same group
		for (var i = 0; i < messageCount; i++)
		{
			await s_sqsClient!.SendMessageAsync(new SendMessageRequest
			{
				QueueUrl = queueUrl,
				MessageBody = $"FIFO message {i}",
				MessageGroupId = "test-group",
				MessageDeduplicationId = $"dedup-{Guid.NewGuid():N}",
			}).ConfigureAwait(false);
		}

		// Assert — receive in order
		var receivedBodies = new List<string>();
		for (var attempt = 0; attempt < 5 && receivedBodies.Count < messageCount; attempt++)
		{
			var receiveResponse = await s_sqsClient!.ReceiveMessageAsync(new ReceiveMessageRequest
			{
				QueueUrl = queueUrl,
				MaxNumberOfMessages = 10,
				WaitTimeSeconds = 5,
			}).ConfigureAwait(false);

			foreach (var msg in receiveResponse.Messages)
			{
				receivedBodies.Add(msg.Body);
				// Delete so we can receive the next batch
				await s_sqsClient.DeleteMessageAsync(queueUrl, msg.ReceiptHandle).ConfigureAwait(false);
			}
		}

		receivedBodies.Count.ShouldBe(messageCount);
		for (var i = 0; i < messageCount; i++)
		{
			receivedBodies[i].ShouldBe($"FIFO message {i}");
		}
	}

	[SkippableFact]
	public async Task SendEmptyBody_Succeeds()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);

		// Act — SQS requires at least 1 character, use a single space
		var sendResponse = await s_sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = " ",
		}).ConfigureAwait(false);

		// Assert
		sendResponse.MessageId.ShouldNotBeNullOrWhiteSpace();

		var receiveResponse = await s_sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		receiveResponse.Messages.ShouldNotBeEmpty();
		receiveResponse.Messages[0].Body.ShouldBe(" ");
	}

	[SkippableFact]
	public async Task QueueUrl_IsAccessible()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueName = $"test-exists-{Guid.NewGuid():N}";

		// Act
		var createResponse = await s_sqsClient!.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = queueName,
		}).ConfigureAwait(false);

		// Assert — GetQueueUrl should resolve
		var getUrlResponse = await s_sqsClient.GetQueueUrlAsync(queueName).ConfigureAwait(false);
		getUrlResponse.QueueUrl.ShouldBe(createResponse.QueueUrl);
	}

	[SkippableFact]
	public async Task MessageBody_RoundTrips()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);
		var jsonBody = "{\"id\":1,\"name\":\"Test \u00e9v\u00e9nement\",\"tags\":[\"a\",\"b\"]}";

		// Act
		await s_sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = jsonBody,
		}).ConfigureAwait(false);

		// Assert
		var receiveResponse = await s_sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
		{
			QueueUrl = queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 5,
		}).ConfigureAwait(false);

		receiveResponse.Messages.ShouldNotBeEmpty();
		receiveResponse.Messages[0].Body.ShouldBe(jsonBody);
	}

	[SkippableFact]
	public async Task SendMessage_ReturnsMessageId()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		var queueUrl = await CreateStandardQueueAsync().ConfigureAwait(false);

		// Act
		var sendResponse = await s_sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = "id-check",
		}).ConfigureAwait(false);

		// Assert
		sendResponse.MessageId.ShouldNotBeNullOrWhiteSpace();
		sendResponse.MD5OfMessageBody.ShouldNotBeNullOrWhiteSpace();
	}
}
