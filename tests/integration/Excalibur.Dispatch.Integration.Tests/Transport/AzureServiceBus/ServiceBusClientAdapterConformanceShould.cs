// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.AzureServiceBus;

/// <summary>
/// Real-SDK conformance smoke for <see cref="ServiceBusClientAdapter"/>.
/// Verifies that the S798-A4 <see cref="IServiceBusClient"/> seam
/// (<c>f5c960341</c>) faithfully passes through to the underlying
/// <see cref="ServiceBusClient"/>, exercising all three use-case methods
/// (<see cref="IServiceBusClient.SendMessageAsync"/>,
/// <see cref="IServiceBusClient.PeekDlqMessagesAsync"/>,
/// <see cref="IServiceBusClient.PurgeDlqAsync"/>) against the
/// Azure Service Bus emulator.
/// </summary>
/// <remarks>
/// <para>
/// This is the S798 task-515 A5 deliverable per OVERWATCH msg 1752 and
/// COMPASS A2 msg 1705 §A5: "conformance test confirming adapter passthrough:
/// one real-SDK smoke test per adapter under integration shard."
/// </para>
/// <para>
/// Scope is deliberately minimal — verifies the adapter constructs cleanly
/// from a real <see cref="ServiceBusClient"/> and that each of the three seam
/// methods reaches the wire. Behaviorally-exhaustive ServiceBus transport
/// tests already live in
/// <see cref="AzureServiceBusTransportSenderIntegrationShould"/> and
/// <see cref="AzureServiceBusTransportReceiverIntegrationShould"/> — this
/// smoke is the ADR-142 §D7 seam-passthrough contract, not a re-test of SDK
/// behavior.
/// </para>
/// </remarks>
[Collection(ContainerCollections.AzureServiceBus)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "AzureServiceBus")]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class ServiceBusClientAdapterConformanceShould
{
	// Pre-created by the emulator's Config.json (servicebus-emulator-config.json)
	// loaded via AzureServiceBusContainerFixture.WithConfig().
	private const string TestQueueName = "test-queue";

	private readonly AzureServiceBusContainerFixture _fixture;

	public ServiceBusClientAdapterConformanceShould(AzureServiceBusContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[SkippableFact]
	public void Construct_WithRealServiceBusClient_Succeeds()
	{
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// The adapter is the ONLY place in the framework that touches the live SDK
		// ServiceBusClient; this asserts the ctor contract round-trips on a real client.
		var adapter = new ServiceBusClientAdapter(_fixture.Client);

		((IServiceBusClient)adapter).ShouldNotBeNull();
	}

	[SkippableFact]
	public void Construct_WithNullInner_ThrowsArgumentNullException()
	{
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		// Guardrail: null-inner rejection is part of the seam contract.
		Should.Throw<ArgumentNullException>(() => new ServiceBusClientAdapter(null!));
	}

	[SkippableFact]
	public async Task SendMessageAsync_PassesThroughToRealClient()
	{
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		IServiceBusClient adapter = new ServiceBusClientAdapter(_fixture.Client);

		var sentMessageId = Guid.NewGuid().ToString();
		var message = new ServiceBusMessage("adapter conformance smoke")
		{
			MessageId = sentMessageId,
			ContentType = "text/plain",
		};

		// Act — exercises the adapter's CreateSender + SendMessageAsync path
		await adapter.SendMessageAsync(TestQueueName, message, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — round-trip via an independent receiver; confirms the message
		// reached the emulator (i.e., the adapter is a true passthrough).
		await using var receiver = _fixture.Client.CreateReceiver(TestQueueName);
		var received = await receiver
			.ReceiveMessageAsync(TimeSpan.FromSeconds(10))
			.ConfigureAwait(false);

		received.ShouldNotBeNull();
		received.MessageId.ShouldBe(sentMessageId);

		await receiver.CompleteMessageAsync(received).ConfigureAwait(false);
	}

	[SkippableFact]
	public async Task PeekDlqMessagesAsync_PassesThroughToRealClient()
	{
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		IServiceBusClient adapter = new ServiceBusClientAdapter(_fixture.Client);

		// Act — exercises the adapter's CreateReceiver(DLQ) + PeekMessagesAsync path.
		// DLQ may be empty on a fresh emulator; the contract is "the call completes
		// cleanly without SDK exceptions" — if the adapter passthrough is wrong, this
		// throws InvalidOperationException or ServiceBusException.
		var peeked = await adapter.PeekDlqMessagesAsync(
			TestQueueName,
			maxMessages: 10,
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		// Assert — the call returns a well-formed list (possibly empty).
		peeked.ShouldNotBeNull();
	}

	[SkippableFact]
	public async Task PurgeDlqAsync_PassesThroughToRealClient()
	{
		Skip.IfNot(_fixture.DockerAvailable, "Docker is not available");

		IServiceBusClient adapter = new ServiceBusClientAdapter(_fixture.Client);

		// Act — exercises the adapter's CreateReceiver(DLQ) + ReceiveMessagesAsync +
		// CompleteMessageAsync drain loop. Uses a short wait time so the test does
		// not hang when the DLQ is empty (the common case on a fresh emulator).
		var purged = await adapter.PurgeDlqAsync(
			TestQueueName,
			maxBatchSize: 10,
			receiveWaitTime: TimeSpan.FromSeconds(1),
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		// Assert — the drain reports a non-negative count (0 when DLQ is empty).
		purged.ShouldBeGreaterThanOrEqualTo(0);
	}
}
