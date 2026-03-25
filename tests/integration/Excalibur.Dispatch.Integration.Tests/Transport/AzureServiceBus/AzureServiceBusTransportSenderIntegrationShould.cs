// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Tests.Shared.Fixtures;

using ServiceBusContainerBuilder = Testcontainers.ServiceBus.ServiceBusBuilder;
using ServiceBusEmulatorContainer = Testcontainers.ServiceBus.ServiceBusContainer;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AzureServiceBus;

/// <summary>
/// Integration tests for Azure Service Bus transport sender operations.
/// Verifies message publishing to a real Service Bus emulator container, including
/// single sends, batch sends, scheduled messages, and message property mapping.
/// </summary>
/// <remarks>
/// Container lifecycle: a single static emulator container is shared across all
/// test instances in this class. This avoids per-test container creation that
/// each times out (~20s) when the emulator is unavailable on Ubuntu CI.
/// </remarks>
[Collection(ContainerCollections.AzureServiceBus)]
[Trait("Category", "Integration")]
[Trait("Provider", "AzureServiceBus")]
[Trait("Component", "Transport")]
public sealed class AzureServiceBusTransportSenderIntegrationShould : IAsyncLifetime, IDisposable
{
	private const string TestQueueName = "test-queue";
	private const string SessionQueueName = "session-queue";

	// Static container shared across all test instances in this class.
	// Avoids per-test container creation that each times out (~20s) on Ubuntu CI.
	private static readonly SemaphoreSlim s_initLock = new(1, 1);
	private static volatile bool s_initialized;
	private static volatile bool s_dockerAvailable;
	private static ServiceBusEmulatorContainer? s_container;
	private static ServiceBusClient? s_client;

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
				s_container = new ServiceBusContainerBuilder()
					.WithAcceptLicenseAgreement(true)
					.Build();
				await s_container.StartAsync().ConfigureAwait(false);

				var connectionString = s_container.GetConnectionString();

				var adminClient = new ServiceBusAdministrationClient(connectionString);

				// Create standard test queue
				await adminClient.CreateQueueAsync(new CreateQueueOptions(TestQueueName)
				{
					DefaultMessageTimeToLive = TimeSpan.FromMinutes(5),
				}).ConfigureAwait(false);

				// Create session-enabled queue
				await adminClient.CreateQueueAsync(new CreateQueueOptions(SessionQueueName)
				{
					RequiresSession = true,
					DefaultMessageTimeToLive = TimeSpan.FromMinutes(5),
				}).ConfigureAwait(false);

				s_client = new ServiceBusClient(connectionString);
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

	public Task DisposeAsync() => Task.CompletedTask;

	public void Dispose()
	{
		// Static resources are not disposed per-test; container lives for the class lifetime.
	}

	[SkippableFact]
	public async Task SendMessage_DeliversToQueue()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var messageBody = "Hello, Azure Service Bus!";
		var message = new ServiceBusMessage(messageBody)
		{
			MessageId = Guid.NewGuid().ToString(),
			ContentType = "text/plain",
		};

		// Act
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Assert
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		received.ShouldNotBeNull();
		received.Body.ToString().ShouldBe(messageBody);
		received.MessageId.ShouldBe(message.MessageId);
		received.ContentType.ShouldBe("text/plain");

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task SendBatchMessages_AllDeliverSuccessfully()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		const int batchSize = 5;
		using var batch = await sender.CreateMessageBatchAsync().ConfigureAwait(false);

		for (var i = 0; i < batchSize; i++)
		{
			var added = batch.TryAddMessage(new ServiceBusMessage($"Batch message {i}")
			{
				MessageId = $"batch-{i}",
			});
			added.ShouldBeTrue($"Failed to add message {i} to batch");
		}

		// Act
		await sender.SendMessagesAsync(batch).ConfigureAwait(false);

		// Assert
		var receivedCount = 0;
		for (var i = 0; i < batchSize; i++)
		{
			var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
			if (received is not null)
			{
				receivedCount++;
				await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
			}
		}

		receivedCount.ShouldBe(batchSize);
	}

	[SkippableFact]
	public async Task SendMessageWithProperties_PropertiesPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var message = new ServiceBusMessage("property test")
		{
			MessageId = Guid.NewGuid().ToString(),
			ContentType = "application/json",
			CorrelationId = "corr-456",
		};
		message.ApplicationProperties["custom-key"] = "custom-value";
		message.ApplicationProperties["retry-count"] = 3;

		// Act
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Assert
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		received.ShouldNotBeNull();
		received.CorrelationId.ShouldBe("corr-456");
		received.ApplicationProperties.ShouldContainKey("custom-key");
		received.ApplicationProperties["custom-key"].ShouldBe("custom-value");
		received.ApplicationProperties.ShouldContainKey("retry-count");
		received.ApplicationProperties["retry-count"].ShouldBe(3);

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task SendScheduledMessage_ReturnsSequenceNumber()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);

		var message = new ServiceBusMessage("scheduled message")
		{
			MessageId = Guid.NewGuid().ToString(),
		};
		var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(30);

		// Act
		var sequenceNumber = await sender.ScheduleMessageAsync(message, scheduledTime).ConfigureAwait(false);

		// Assert
		sequenceNumber.ShouldBeGreaterThan(0);

		// Cleanup - cancel the scheduled message so it doesn't linger
		await sender.CancelScheduledMessageAsync(sequenceNumber).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task SendMessageWithSessionId_SessionPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(SessionQueueName);

		var sessionId = $"session-{Guid.NewGuid():N}";
		var message = new ServiceBusMessage("session message")
		{
			MessageId = Guid.NewGuid().ToString(),
			SessionId = sessionId,
		};

		// Act
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Assert - receive from the specific session
		await using var sessionReceiver = await s_client.AcceptSessionAsync(
			SessionQueueName,
			sessionId,
			new ServiceBusSessionReceiverOptions
			{
				ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
			}).ConfigureAwait(false);

		var received = await sessionReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		received.ShouldNotBeNull();
		received.SessionId.ShouldBe(sessionId);
		received.Body.ToString().ShouldBe("session message");
	}

	[SkippableFact]
	public async Task SendMessageWithSubject_SubjectPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var message = new ServiceBusMessage("subject test")
		{
			MessageId = Guid.NewGuid().ToString(),
			Subject = "OrderCreated",
		};

		// Act
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Assert
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		received.ShouldNotBeNull();
		received.Subject.ShouldBe("OrderCreated");

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task MessageBody_RoundTrips()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var jsonPayload = "{\"orderId\":42,\"amount\":99.95,\"currency\":\"USD\"}";
		var binaryBody = BinaryData.FromString(jsonPayload);

		var message = new ServiceBusMessage(binaryBody)
		{
			MessageId = Guid.NewGuid().ToString(),
			ContentType = "application/json",
		};

		// Act
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Assert
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		received.ShouldNotBeNull();
		received.Body.ToString().ShouldBe(jsonPayload);
		received.ContentType.ShouldBe("application/json");

		// Verify raw bytes match
		var expectedBytes = Encoding.UTF8.GetBytes(jsonPayload);
		received.Body.ToArray().ShouldBe(expectedBytes);

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}
}
