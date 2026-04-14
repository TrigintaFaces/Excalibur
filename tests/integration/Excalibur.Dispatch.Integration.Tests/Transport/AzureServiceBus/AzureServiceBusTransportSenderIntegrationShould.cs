// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AzureServiceBus;

/// <summary>
/// Integration tests for Azure Service Bus transport sender operations.
/// Verifies message publishing to a real Service Bus emulator container, including
/// single sends, batch sends, scheduled messages, and message property mapping.
/// </summary>
/// <remarks>
/// Container lifecycle is managed by <see cref="AzureServiceBusContainerFixture"/> via the
/// xUnit collection fixture pattern. The fixture is shared across all test classes
/// in the <see cref="ContainerCollections.AzureServiceBus"/> collection.
/// </remarks>
[Collection(ContainerCollections.AzureServiceBus)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "AzureServiceBus")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AzureServiceBusTransportSenderIntegrationShould : IAsyncLifetime
{
	private const string TestQueueName = "test-queue";
	private const string SessionQueueName = "session-queue";

	private readonly AzureServiceBusContainerFixture _fixture;

	public AzureServiceBusTransportSenderIntegrationShould(AzureServiceBusContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// Creates the queues needed by sender tests. Called once per test instance,
	/// but queue creation is idempotent (catches conflict if already exists).
	/// </summary>
	public Task InitializeAsync() => Task.CompletedTask;
	// Queues are pre-created by the emulator's Config.json (servicebus-emulator-config.json)
	// loaded via AzureServiceBusContainerFixture.WithConfig().

	public Task DisposeAsync() => Task.CompletedTask;

	[SkippableFact]
	public async Task SendMessage_DeliversToQueue()
	{
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);

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
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(SessionQueueName);

		var sessionId = $"session-{Guid.NewGuid():N}";
		var message = new ServiceBusMessage("session message")
		{
			MessageId = Guid.NewGuid().ToString(),
			SessionId = sessionId,
		};

		// Act
		await sender.SendMessageAsync(message).ConfigureAwait(false);

		// Assert - receive from the specific session
		await using var sessionReceiver = await _fixture.Client.AcceptSessionAsync(
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
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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
