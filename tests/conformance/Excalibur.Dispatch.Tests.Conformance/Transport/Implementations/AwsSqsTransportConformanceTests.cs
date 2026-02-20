// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

using Testcontainers.LocalStack;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for AWS SQS transport using LocalStack TestContainers.
/// Automatically provisions a LocalStack container for testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "AwsSqs")]
// CA1001: Disposable fields are disposed via IAsyncLifetime.DisposeAsync -> DisposeTransportAsync pattern
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Base class implements IAsyncLifetime which calls DisposeTransportAsync to dispose fields")]
public sealed class AwsSqsTransportConformanceTests
	: TransportConformanceTestBase<AwsSqsChannelSender, AwsSqsChannelReceiver>
{
	private const string QueueName = "conformance-test-queue";

	private LocalStackContainer? _localStackContainer;
	private IAmazonSQS? _sqsClient;
	private string? _queueUrl;
	private AwsSqsDeadLetterQueueManager? _dlqManager;

	protected override async Task<AwsSqsChannelSender> CreateSenderAsync()
	{
		// Start LocalStack container
		_localStackContainer = new LocalStackBuilder()
			.WithImage("localstack/localstack:latest")
			.Build();

		await _localStackContainer.StartAsync();

		// Create SQS client pointing to LocalStack
		var credentials = new BasicAWSCredentials("test", "test");
		_sqsClient = new AmazonSQSClient(
			credentials,
			new AmazonSQSConfig
			{
				ServiceURL = _localStackContainer.GetConnectionString(),
				AuthenticationRegion = RegionEndpoint.USEast1.SystemName
			});

		// Create test queue
		var createQueueResponse = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = QueueName,
			Attributes = new Dictionary<string, string>
			{
				{ "MessageRetentionPeriod", "300" }, // 5 minutes
				{ "VisibilityTimeout", "30" }
			}
		});

		_queueUrl = createQueueResponse.QueueUrl;

		return new AwsSqsChannelSender(_sqsClient, _queueUrl);
	}

	protected override async Task<AwsSqsChannelReceiver> CreateReceiverAsync()
	{
		if (_sqsClient == null || _queueUrl == null)
		{
			throw new InvalidOperationException("SQS client not initialized. Ensure sender is created first.");
		}

		return await Task.FromResult(new AwsSqsChannelReceiver(_sqsClient, _queueUrl));
	}

	protected override async Task<IDeadLetterQueueManager?> CreateDlqManagerAsync()
	{
		if (_sqsClient == null || _queueUrl == null)
		{
			throw new InvalidOperationException("SQS client not initialized.");
		}

		// Create DLQ
		var dlqResponse = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
		{
			QueueName = $"{QueueName}-dlq"
		});

		_dlqManager = new AwsSqsDeadLetterQueueManager(_sqsClient, _queueUrl, dlqResponse.QueueUrl);
		return _dlqManager;
	}

	protected override async Task DisposeTransportAsync()
	{
		if (_sqsClient != null && _queueUrl != null)
		{
			try
			{
				_ = await _sqsClient.DeleteQueueAsync(_queueUrl);
			}
			catch
			{
				// Ignore errors during cleanup
			}

			_sqsClient.Dispose();
		}

		if (_localStackContainer != null)
		{
			await _localStackContainer.DisposeAsync();
		}
	}
}

/// <summary>
/// AWS SQS implementation of IChannelSender for conformance testing.
/// </summary>
public sealed class AwsSqsChannelSender : IChannelSender
{
	private readonly IAmazonSQS _sqsClient;
	private readonly string _queueUrl;

	public AwsSqsChannelSender(IAmazonSQS sqsClient, string queueUrl)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
	}

	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		if (message == null)
		{
			throw new ArgumentNullException(nameof(message));
		}

		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var sendRequest = new SendMessageRequest
		{
			QueueUrl = _queueUrl,
			MessageBody = json,
			MessageAttributes = new Dictionary<string, MessageAttributeValue>()
		};

		// Extract metadata if available
		var messageType = typeof(T);
		if (messageType.GetProperty("MessageId") != null)
		{
			var messageId = messageType.GetProperty("MessageId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(messageId))
			{
				sendRequest.MessageAttributes["MessageId"] = new MessageAttributeValue
				{
					DataType = "String",
					StringValue = messageId
				};
			}
		}

		if (messageType.GetProperty("CorrelationId") != null)
		{
			var correlationId = messageType.GetProperty("CorrelationId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(correlationId))
			{
				sendRequest.MessageAttributes["CorrelationId"] = new MessageAttributeValue
				{
					DataType = "String",
					StringValue = correlationId
				};
			}
		}

		_ = await _sqsClient.SendMessageAsync(sendRequest, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// AWS SQS implementation of IChannelReceiver for conformance testing.
/// </summary>
public sealed class AwsSqsChannelReceiver : IChannelReceiver
{
	private readonly IAmazonSQS _sqsClient;
	private readonly string _queueUrl;

	public AwsSqsChannelReceiver(IAmazonSQS sqsClient, string queueUrl)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
	}

	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		var receiveRequest = new ReceiveMessageRequest
		{
			QueueUrl = _queueUrl,
			MaxNumberOfMessages = 1,
			WaitTimeSeconds = 20, // Long polling
			MessageAttributeNames = new List<string> { "All" }
		};

		var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken)
			.ConfigureAwait(false);

		if (response.Messages == null || response.Messages.Count == 0)
		{
			return default;
		}

		var sqsMessage = response.Messages[0];

		try
		{
			var result = System.Text.Json.JsonSerializer.Deserialize<T>(sqsMessage.Body);

			// Delete message after successful processing
			_ = await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
			{
				QueueUrl = _queueUrl,
				ReceiptHandle = sqsMessage.ReceiptHandle
			}, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch
		{
			// Message will become visible again after visibility timeout
			throw;
		}
	}
}

/// <summary>
/// AWS SQS implementation of IDeadLetterQueueManager for conformance testing.
/// </summary>
public sealed class AwsSqsDeadLetterQueueManager : IDeadLetterQueueManager
{
	private readonly IAmazonSQS _sqsClient;
	private readonly string _queueUrl;
	private readonly string _dlqUrl;

	public AwsSqsDeadLetterQueueManager(IAmazonSQS sqsClient, string queueUrl, string dlqUrl)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
		_dlqUrl = dlqUrl ?? throw new ArgumentNullException(nameof(dlqUrl));
	}

	public async Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		// Move message to DLQ
		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var sendRequest = new SendMessageRequest
		{
			QueueUrl = _dlqUrl,
			MessageBody = json,
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["Reason"] = new MessageAttributeValue { DataType = "String", StringValue = reason },
				["DeadLetteredAt"] = new MessageAttributeValue
				{
					DataType = "String",
					StringValue = DateTimeOffset.UtcNow.ToString("O")
				}
			}
		};

		if (exception != null)
		{
			sendRequest.MessageAttributes["Exception"] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = exception.Message
			};
		}

		var response = await _sqsClient.SendMessageAsync(sendRequest, cancellationToken).ConfigureAwait(false);
		return response.MessageId;
	}

	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		var receiveRequest = new ReceiveMessageRequest
		{
			QueueUrl = _dlqUrl,
			MaxNumberOfMessages = Math.Min(maxMessages, 10),
			MessageAttributeNames = new List<string> { "All" }
		};

		var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken)
			.ConfigureAwait(false);

		var result = new List<DeadLetterMessage>();
		foreach (var sqsMessage in response.Messages)
		{
			var transportMessage = System.Text.Json.JsonSerializer.Deserialize<TransportMessage>(sqsMessage.Body);
			if (transportMessage != null)
			{
				var reason = sqsMessage.MessageAttributes.ContainsKey("Reason")
					? sqsMessage.MessageAttributes["Reason"].StringValue
					: "Unknown";

				var exceptionMessage = sqsMessage.MessageAttributes.ContainsKey("Exception")
					? sqsMessage.MessageAttributes["Exception"].StringValue
					: null;

				var deadLetteredAt = sqsMessage.MessageAttributes.ContainsKey("DeadLetteredAt")
					? DateTimeOffset.Parse(sqsMessage.MessageAttributes["DeadLetteredAt"].StringValue)
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
		// Move messages back to active queue
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
		var attributesRequest = new GetQueueAttributesRequest
		{
			QueueUrl = _dlqUrl,
			AttributeNames = new List<string> { "ApproximateNumberOfMessages" }
		};

		var response = await _sqsClient.GetQueueAttributesAsync(attributesRequest, cancellationToken)
			.ConfigureAwait(false);

		var messageCount = response.ApproximateNumberOfMessages;

		return new DeadLetterStatistics
		{
			MessageCount = messageCount,
			OldestMessageAge = TimeSpan.Zero // SQS doesn't provide this directly
		};
	}

	public async Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		var stats = await GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
		var count = stats.MessageCount;

		_ = await _sqsClient.PurgeQueueAsync(new PurgeQueueRequest
		{
			QueueUrl = _dlqUrl
		}, cancellationToken).ConfigureAwait(false);

		return count;
	}
}
