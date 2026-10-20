// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Logging.Abstractions;
using Azure.Messaging.ServiceBus.Administration;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AzureServiceBus;

/// <summary>
/// Integration tests for Azure Service Bus transport receiver operations.
/// Verifies message consumption from a real Service Bus emulator container, including
/// receive, complete, abandon, dead-letter, peek, and property preservation.
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
public sealed class AzureServiceBusTransportReceiverIntegrationShould : IAsyncLifetime
{
	private const string TestQueueName = "receiver-test-queue";

	private readonly AzureServiceBusContainerFixture _fixture;

	public AzureServiceBusTransportReceiverIntegrationShould(AzureServiceBusContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// Creates the queue needed by receiver tests. Called once per test instance,
	/// but queue creation is idempotent (catches conflict if already exists).
	/// </summary>
	public ValueTask InitializeAsync() => default;
	// Queues are pre-created by the emulator's Config.json (servicebus-emulator-config.json)
	// loaded via AzureServiceBusContainerFixture.WithConfig().

	public ValueTask DisposeAsync() => default;

	[Fact]
	public async Task ReceiveMessages_FromPopulatedQueue()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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

	[Fact]
	public async Task ReceiveFromEmptyQueue_ReturnsNull()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

		// Act - short timeout on empty queue
		var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Assert
		received.ShouldBeNull();
	}

	[Fact]
	public async Task CompleteMessage_RemovesFromQueue()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");
		await VerifySettlementAsync("acknowledge", noOp: false).ConfigureAwait(false);
		await VerifySettlementAsync("acknowledge", noOp: true).ConfigureAwait(false);
	}

	[Fact]
	public async Task AbandonMessage_MakesMessageReappear()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");
		await VerifySettlementAsync("requeue", noOp: false).ConfigureAwait(false);
		await VerifySettlementAsync("requeue", noOp: true).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeadLetterMessage_MovesToDlq()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");
		await VerifySettlementAsync("reject", noOp: false).ConfigureAwait(false);
		await VerifySettlementAsync("reject", noOp: true).ConfigureAwait(false);
	}

	[Fact]
	public async Task PeekMessage_DoesNotRemove()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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

	[Fact]
	public async Task ReceiveWithProperties_PropertiesPreserved()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, "Docker is not available");

		// Arrange
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);

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

	private async Task VerifySettlementAsync(string action, bool noOp)
	{
		using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(25));
		var targetId = $"settlement-{Guid.NewGuid():N}";
		var body = $"body-{targetId}";
		const string RejectionReason = "integration rejection";
		await using var sender = _fixture.Client.CreateSender(TestQueueName);
		await sender.SendMessageAsync(new ServiceBusMessage(body) { MessageId = targetId }, bounded.Token).ConfigureAwait(false);
		await using var realReceiver = new ServiceBusTransportReceiver(
			_fixture.Client.CreateReceiver(TestQueueName), TestQueueName, NullLogger<ServiceBusTransportReceiver>.Instance);
		ITransportReceiver receiver = noOp ? new NoOpSettlementReceiver(realReceiver) : realReceiver;
		var received = (await receiver.ReceiveAsync(1, bounded.Token).ConfigureAwait(false)).Single();
		try
		{
			received.Id.ShouldBe(targetId);
			Encoding.UTF8.GetString(received.Body.Span).ShouldBe(body);
			var sequence = (long)received.ProviderData[TransportOrderingMetadata.AzureServiceBusSequenceKey];
			await using var peek = _fixture.Client.CreateReceiver(TestQueueName);
			var before = await peek.PeekMessageAsync(sequence, bounded.Token).ConfigureAwait(false);
			before.ShouldNotBeNull();
			before.MessageId.ShouldBe(targetId);
			before.SequenceNumber.ShouldBe(sequence);
			if (action == "acknowledge")
				await receiver.AcknowledgeAsync(received, bounded.Token).ConfigureAwait(false);
			else
				await receiver.RejectAsync(received, RejectionReason, action == "requeue", bounded.Token).ConfigureAwait(false);

			if (action == "requeue")
			{
				await using var probe = _fixture.Client.CreateReceiver(TestQueueName);
				var redelivered = await probe.ReceiveMessageAsync(TimeSpan.FromSeconds(3), bounded.Token).ConfigureAwait(false);
				if (noOp)
				{
					try
					{
						redelivered.ShouldBeNull("the no-op retains the target lock; receiving another message invalidates this isolated probe");
					}
					finally
					{
						if (redelivered is not null)
							await probe.AbandonMessageAsync(redelivered, cancellationToken: CancellationToken.None).ConfigureAwait(false);
					}
				}
				else
				{
					redelivered.ShouldNotBeNull();
					redelivered.MessageId.ShouldBe(targetId);
					redelivered.Body.ToString().ShouldBe(body);
					redelivered.DeliveryCount.ShouldBeGreaterThan(received.DeliveryCount);
					await probe.CompleteMessageAsync(redelivered, bounded.Token).ConfigureAwait(false);
				}
			}
			if (action == "reject")
			{
				// This serialized class owns receiver-test-queue; observed DLQ targets remain until fixture disposal.
				await using var dlq = _fixture.Client.CreateReceiver(TestQueueName,
					new ServiceBusReceiverOptions
					{
						SubQueue = SubQueue.DeadLetter,
						ReceiveMode = ServiceBusReceiveMode.PeekLock,
					});
				if (noOp)
				{
					var deadLettered = await PeekTargetAsync(dlq, targetId, bounded.Token).ConfigureAwait(false);
					deadLettered.ShouldBeNull();
				}
				else
				{
					// Each poll starts at zero so delayed target arrival cannot be missed by an advanced cursor.
					var deadLettered = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitForValueAsync(
						() => PeekTargetAsync(dlq, targetId, bounded.Token), TimeSpan.FromSeconds(5),
						cancellationToken: bounded.Token).ConfigureAwait(false);
					deadLettered.ShouldNotBeNull();
					deadLettered.MessageId.ShouldBe(targetId);
					deadLettered.Body.ToString().ShouldBe(body);
					deadLettered.DeadLetterReason.ShouldBe("Rejected");
					deadLettered.DeadLetterErrorDescription.ShouldBe(RejectionReason);
				}
			}

			// A locked message was visible to this independent peek before settlement.
			await using var afterProbe = _fixture.Client.CreateReceiver(TestQueueName);
			var after = await afterProbe.PeekMessageAsync(sequence, bounded.Token).ConfigureAwait(false);
			if (noOp)
			{
				after.ShouldNotBeNull();
				after.MessageId.ShouldBe(targetId);
				after.SequenceNumber.ShouldBe(sequence);
				after.Body.ToString().ShouldBe(body);
			}
			else
				(after?.SequenceNumber == sequence).ShouldBeFalse("the settled target must leave the source queue");
		}
		finally
		{
			if (noOp)
				await realReceiver.AcknowledgeAsync(received, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private static async Task<ServiceBusReceivedMessage?> PeekTargetAsync(
		ServiceBusReceiver receiver, string targetId, CancellationToken cancellationToken)
	{
		long cursor = 0;
		while (true)
		{
			var page = await receiver.PeekMessagesAsync(100, cursor, cancellationToken).ConfigureAwait(false);
			if (page.Count == 0)
				return null;
			var target = page.FirstOrDefault(message => message.MessageId == targetId);
			if (target is not null)
				return target;
			var next = checked(page.Max(message => message.SequenceNumber) + 1);
			if (next <= cursor)
				throw new InvalidOperationException("The DLQ peek cursor did not advance.");
			cursor = next;
		}
	}

	private sealed class NoOpSettlementReceiver(ITransportReceiver inner) : DelegatingTransportReceiver(inner)
	{
		public override Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
		public override Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
