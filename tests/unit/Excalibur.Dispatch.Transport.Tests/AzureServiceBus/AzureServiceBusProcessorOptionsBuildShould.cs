// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Regression locks for the Azure Service Bus processor-options wiring (i6r213): the collected
/// <see cref="AzureServiceBusProcessorOptions"/> must be projected onto the SDK processor options, and
/// the dead-letter description must be forwarded (not dropped) when a message is rejected.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AzureServiceBusProcessorOptionsBuildShould
{
	[Fact]
	public void ProjectEveryCollectedProcessorOptionOntoTheSdkOptions()
	{
		// Arrange — a fully populated, non-default processor configuration.
		var collected = new AzureServiceBusProcessorOptions
		{
			PrefetchCount = 200,
			MaxConcurrentCalls = 32,
			AutoCompleteMessages = false,
			ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
			MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10),
		};

		// Act
		var sdk = AzureServiceBusTransportServiceCollectionExtensions.BuildProcessorOptions(collected);

		// Assert — pre-fix the processor was created with no options, so none of these were applied.
		sdk.PrefetchCount.ShouldBe(200);
		sdk.MaxConcurrentCalls.ShouldBe(32);
		sdk.AutoCompleteMessages.ShouldBeFalse();
		sdk.ReceiveMode.ShouldBe(ServiceBusReceiveMode.ReceiveAndDelete);
		sdk.MaxAutoLockRenewalDuration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void PreserveTheSdkDefaultLockRenewalWhenNoneIsConfigured()
	{
		// Arrange — MaxAutoLockRenewalDuration left unset (null) on the collected options.
		var collected = new AzureServiceBusProcessorOptions();
		var sdkDefault = new ServiceBusProcessorOptions().MaxAutoLockRenewalDuration;

		// Act
		var sdk = AzureServiceBusTransportServiceCollectionExtensions.BuildProcessorOptions(collected);

		// Assert — a null consumer value must keep the SDK default, never force TimeSpan.Zero.
		sdk.MaxAutoLockRenewalDuration.ShouldBe(sdkDefault);
	}

	[Fact]
	public async Task ForwardTheDeadLetterDescriptionWhenRejectingAMessage()
	{
		// Arrange — a received message whose lock token round-trips through the receiver cache.
		var sbMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
			body: BinaryData.FromString("{}"),
			messageId: "msg-1",
			lockTokenGuid: Guid.NewGuid());

		var seam = A.Fake<IServiceBusReceiverSeam>();
		A.CallTo(() => seam.ReceiveMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new[] { sbMessage });

		await using var receiver = new ServiceBusTransportReceiver(
			seam,
			"test-queue",
			A.Fake<ILogger<ServiceBusTransportReceiver>>());

		var received = await receiver.ReceiveAsync(1, CancellationToken.None);

		// Act — reject (not requeue) with a detailed reason that must reach the DLQ description field.
		await receiver.RejectAsync(received[0], "bad-payload", requeue: false, CancellationToken.None);

		// Assert — pre-fix the seam carried no description parameter and the detail was dropped.
		A.CallTo(() => seam.DeadLetterMessageAsync(
				A<ServiceBusReceivedMessage>._,
				A<string?>._,
				"bad-payload",
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
