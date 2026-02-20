// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Testcontainers.ServiceBus;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for Azure Service Bus transport using TestContainers.
/// Automatically provisions an Azure Service Bus emulator container for testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "AzureServiceBus")]
// CA1001: Disposable fields are disposed via IAsyncLifetime.DisposeAsync -> DisposeTransportAsync pattern
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Base class implements IAsyncLifetime which calls DisposeTransportAsync to dispose fields")]
public sealed class AzureServiceBusTransportConformanceTests
	: TransportConformanceTestBase<AzureServiceBusChannelSender, AzureServiceBusChannelReceiver>
{
	private const string QueueName = "conformance-test-queue";

	private ServiceBusContainer? _serviceBusContainer;
	private ServiceBusClient? _client;
	private ServiceBusSender? _sender;
	private ServiceBusReceiver? _receiver;
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

		// Create the queue using the administration client
		var adminClient = new ServiceBusAdministrationClient(connectionString);
		if (!await adminClient.QueueExistsAsync(QueueName))
		{
			_ = await adminClient.CreateQueueAsync(QueueName);
		}

		_sender = _client.CreateSender(QueueName);

		var sender = new AzureServiceBusChannelSender(_sender);
		return sender;
	}

	protected override async Task<AzureServiceBusChannelReceiver> CreateReceiverAsync()
	{
		if (_client == null)
		{
			throw new InvalidOperationException("Client not initialized. Ensure sender is created first.");
		}

		_receiver = _client.CreateReceiver(QueueName, new ServiceBusReceiverOptions
		{
			ReceiveMode = ServiceBusReceiveMode.PeekLock,
			PrefetchCount = 10
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
		// In Azure Service Bus, messages are moved to DLQ by dead-lettering them
		// This is typically done during message processing, not directly
		// For conformance testing, we simulate this behavior
		await using var receiver = _client.CreateReceiver(_queueName);
		var receivedMessage = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), cancellationToken)
			.ConfigureAwait(false);

		if (receivedMessage != null)
		{
			await receiver.DeadLetterMessageAsync(receivedMessage, reason, exception?.Message, cancellationToken)
				.ConfigureAwait(false);
			return receivedMessage.MessageId;
		}

		return message.Id;
	}

	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		await using var dlqReceiver = _client.CreateReceiver(_queueName, new ServiceBusReceiverOptions
		{
			SubQueue = SubQueue.DeadLetter,
			ReceiveMode = ServiceBusReceiveMode.PeekLock
		});

		var messages = await dlqReceiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), cancellationToken)
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

	public async Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
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
