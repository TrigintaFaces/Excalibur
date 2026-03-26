// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Tests.Shared.Fixtures;

using ServiceBusContainerBuilder = Testcontainers.ServiceBus.ServiceBusBuilder;
using ServiceBusEmulatorContainer = Testcontainers.ServiceBus.ServiceBusContainer;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AzureServiceBus;

/// <summary>
/// Integration tests for Azure Service Bus transport receiver operations.
/// Verifies message consumption from a real Service Bus emulator container, including
/// receive, complete, abandon, dead-letter, peek, and property preservation.
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
public sealed class AzureServiceBusTransportReceiverIntegrationShould : IAsyncLifetime, IDisposable
{
	private const string TestQueueName = "receiver-test-queue";

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
				using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				await s_container.StartAsync(startCts.Token).ConfigureAwait(false);

				var connectionString = s_container.GetConnectionString();

				var adminClient = new ServiceBusAdministrationClient(connectionString);

				// Create test queue with dead-letter sub-queue enabled by default
				await adminClient.CreateQueueAsync(new CreateQueueOptions(TestQueueName)
				{
					DefaultMessageTimeToLive = TimeSpan.FromMinutes(5),
					MaxDeliveryCount = 10,
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
	public async Task ReceiveMessages_FromPopulatedQueue()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var expectedBody = "Receive test message";
		var message = new ServiceBusMessage(expectedBody)
		{
			MessageId = Guid.NewGuid().ToString(),
			ContentType = "text/plain",
		};

		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Act
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		// Assert
		received.ShouldNotBeNull();
		received.Body.ToString().ShouldBe(expectedBody);
		received.MessageId.ShouldBe(message.MessageId);
		received.ContentType.ShouldBe("text/plain");

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task ReceiveFromEmptyQueue_ReturnsNull()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var receiver = s_client!.CreateReceiver(TestQueueName);

		// Act - short timeout on empty queue
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Assert
		received.ShouldBeNull();
	}

	[SkippableFact]
	public async Task CompleteMessage_RemovesFromQueue()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var message = new ServiceBusMessage("complete test")
		{
			MessageId = Guid.NewGuid().ToString(),
		};
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Act - receive and complete
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		received.ShouldNotBeNull();
		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);

		// Assert - queue should be empty now
		var afterComplete = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
		afterComplete.ShouldBeNull();
	}

	[SkippableFact]
	public async Task AbandonMessage_MakesMessageReappear()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var message = new ServiceBusMessage("abandon test")
		{
			MessageId = Guid.NewGuid().ToString(),
		};
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Act - receive and abandon
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		received.ShouldNotBeNull();
		received.MessageId.ShouldBe(message.MessageId);
		await receiver.AbandonMessageAsync(received).ConfigureAwait(false);

		// Assert - message should be available again
		var redelivered = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		redelivered.ShouldNotBeNull();
		redelivered.MessageId.ShouldBe(message.MessageId);
		redelivered.DeliveryCount.ShouldBeGreaterThan(1);

		await receiver.CompleteMessageAsync(redelivered).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task DeadLetterMessage_MovesToDlq()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var message = new ServiceBusMessage("dead-letter test")
		{
			MessageId = Guid.NewGuid().ToString(),
		};
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Act - receive and dead-letter
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		received.ShouldNotBeNull();
		await receiver.DeadLetterMessageAsync(received, "TestReason", "Testing dead-letter flow").ConfigureAwait(false);

		// Assert - message should be in the dead-letter sub-queue
		var dlqPath = $"{TestQueueName}/$deadletterqueue";
		await using var dlqReceiver = s_client.CreateReceiver(dlqPath);

		var dlqMessage = await dlqReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		dlqMessage.ShouldNotBeNull();
		dlqMessage.MessageId.ShouldBe(message.MessageId);
		dlqMessage.DeadLetterReason.ShouldBe("TestReason");
		dlqMessage.DeadLetterErrorDescription.ShouldBe("Testing dead-letter flow");

		await dlqReceiver.CompleteMessageAsync(dlqMessage).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task PeekMessage_DoesNotRemove()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var message = new ServiceBusMessage("peek test")
		{
			MessageId = Guid.NewGuid().ToString(),
		};
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Act - peek (should not remove)
		var peeked = await receiver.PeekMessageAsync().ConfigureAwait(false);

		// Assert - peeked message is visible
		peeked.ShouldNotBeNull();
		peeked.MessageId.ShouldBe(message.MessageId);
		peeked.Body.ToString().ShouldBe("peek test");

		// Regular receive should still get the message
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		received.ShouldNotBeNull();
		received.MessageId.ShouldBe(message.MessageId);

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task ReceiveWithProperties_PropertiesPreserved()
	{
		Skip.IfNot(_dockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = s_client!.CreateSender(TestQueueName);
		await using var receiver = s_client.CreateReceiver(TestQueueName);

		var message = new ServiceBusMessage("properties test")
		{
			MessageId = Guid.NewGuid().ToString(),
			ContentType = "application/json",
			CorrelationId = "corr-recv-789",
			Subject = "TestSubject",
		};
		message.ApplicationProperties["source-system"] = "excalibur";
		message.ApplicationProperties["event-type"] = "OrderCreated";
		message.ApplicationProperties["priority"] = 5;

		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Act
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		// Assert
		received.ShouldNotBeNull();
		received.ContentType.ShouldBe("application/json");
		received.CorrelationId.ShouldBe("corr-recv-789");
		received.Subject.ShouldBe("TestSubject");
		received.ApplicationProperties.ShouldContainKey("source-system");
		received.ApplicationProperties["source-system"].ShouldBe("excalibur");
		received.ApplicationProperties.ShouldContainKey("event-type");
		received.ApplicationProperties["event-type"].ShouldBe("OrderCreated");
		received.ApplicationProperties.ShouldContainKey("priority");
		received.ApplicationProperties["priority"].ShouldBe(5);

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}
}
