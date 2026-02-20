// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

using Testcontainers.LocalStack;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AwsSqs;

/// <summary>
/// Integration tests for AWS SQS transport sender operations.
/// Verifies message sending to a real SQS queue via LocalStack, including
/// single sends, batch sends, message attributes, FIFO queues, and body round-tripping.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "AwsSqs")]
[Trait("Component", "Transport")]
public sealed class AwsSqsTransportSenderIntegrationShould : IAsyncLifetime, IDisposable
{
	private LocalStackContainer? _container;
	private AmazonSQSClient? _sqsClient;
	private bool _dockerAvailable;

	public async Task InitializeAsync()
	{
		try
		{
			_container = new LocalStackBuilder()
				.WithImage("localstack/localstack:latest")
				.WithEnvironment("SERVICES", "sqs")
				.Build();
			await _container.StartAsync().ConfigureAwait(false);

			var credentials = new BasicAWSCredentials("test", "test");
			var config = new AmazonSQSConfig
			{
				ServiceURL = _container.GetConnectionString(),
			};
			_sqsClient = new AmazonSQSClient(credentials, config);
			_dockerAvailable = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Docker initialization failed: {ex.Message}");
			_dockerAvailable = false;
		}
	}

	public void Dispose()
	{
		_sqsClient?.Dispose();
	}

	public async Task DisposeAsync()
	{
		_sqsClient?.Dispose();

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

	private async Task<string> CreateStandardQueueAsync(string? queueName = null)
	{
		queueName ??= $"test-queue-{Guid.NewGuid():N}";
		var response = await _sqsClient!.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = queueName,
		}).ConfigureAwait(false);
		return response.QueueUrl;
	}

	private async Task<string> CreateFifoQueueAsync(string? queueName = null)
	{
		queueName ??= $"test-queue-{Guid.NewGuid():N}.fifo";
		var response = await _sqsClient!.CreateQueueAsync(new CreateQueueRequest
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
		var sendResponse = await _sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = messageBody,
		}).ConfigureAwait(false);

		// Assert — receive and verify
		var receiveResponse = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
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
		var batchResponse = await _sqsClient!.SendMessageBatchAsync(new SendMessageBatchRequest
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
			var receiveResponse = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
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
		await _sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = "{\"orderId\": 42}",
			MessageAttributes = attributes,
		}).ConfigureAwait(false);

		// Assert
		var receiveResponse = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
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
			await _sqsClient!.SendMessageAsync(new SendMessageRequest
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
			var receiveResponse = await _sqsClient!.ReceiveMessageAsync(new ReceiveMessageRequest
			{
				QueueUrl = queueUrl,
				MaxNumberOfMessages = 10,
				WaitTimeSeconds = 5,
			}).ConfigureAwait(false);

			foreach (var msg in receiveResponse.Messages)
			{
				receivedBodies.Add(msg.Body);
				// Delete so we can receive the next batch
				await _sqsClient.DeleteMessageAsync(queueUrl, msg.ReceiptHandle).ConfigureAwait(false);
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
		var sendResponse = await _sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = " ",
		}).ConfigureAwait(false);

		// Assert
		sendResponse.MessageId.ShouldNotBeNullOrWhiteSpace();

		var receiveResponse = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
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
		var createResponse = await _sqsClient!.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = queueName,
		}).ConfigureAwait(false);

		// Assert — GetQueueUrl should resolve
		var getUrlResponse = await _sqsClient.GetQueueUrlAsync(queueName).ConfigureAwait(false);
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
		await _sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = jsonBody,
		}).ConfigureAwait(false);

		// Assert
		var receiveResponse = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
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
		var sendResponse = await _sqsClient!.SendMessageAsync(new SendMessageRequest
		{
			QueueUrl = queueUrl,
			MessageBody = "id-check",
		}).ConfigureAwait(false);

		// Assert
		sendResponse.MessageId.ShouldNotBeNullOrWhiteSpace();
		sendResponse.MD5OfMessageBody.ShouldNotBeNullOrWhiteSpace();
	}
}
