// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Tests;

/// <summary>
///     Provides a shared specification that ensures every transport honours the messaging contract.
/// </summary>
/// <typeparam name="TFixture"> The fixture supplying a transport harness. </typeparam>
public abstract class TransportConformanceSpecification<TFixture>(TFixture fixture) : IClassFixture<TFixture>
	where TFixture : TransportConformanceFixtureBase
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

	protected TFixture Fixture { get; } = fixture;

	protected ITransportTestHarness Harness => Fixture.Harness;

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task AcknowledgedMessagesShouldNotBeRedelivered()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var message = TransportTestMessage.Create();
		await Harness.PublishAsync(message).ConfigureAwait(false);

		var received = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = received.ShouldNotBeNull($"{Fixture.TransportName} did not deliver the published message.");
		received.Message.Id.ShouldBe(message.Id);

		await Harness.AcknowledgeAsync(received).ConfigureAwait(false);

		var replay = await Harness.ReceiveAsync(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
		replay.ShouldBeNull("Acknowledged messages must not be redelivered");
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task NegativeAcknowledgementWithRequeueShouldRedeliver()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var message = TransportTestMessage.Create();
		await Harness.PublishAsync(message).ConfigureAwait(false);

		var first = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = first.ShouldNotBeNull();
		first.Message.Id.ShouldBe(message.Id);

		await Harness.NegativeAcknowledgeAsync(first, requeue: true).ConfigureAwait(false);

		var second = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = second.ShouldNotBeNull("Message should be redelivered after NACK with requeue");
		second.Message.Id.ShouldBe(message.Id);
		second.DeliveryAttempt.ShouldBeGreaterThan(1);
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task NegativeAcknowledgementWithoutRequeueShouldDeadLetter()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var message = TransportTestMessage.Create();
		await Harness.PublishAsync(message).ConfigureAwait(false);

		var received = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = received.ShouldNotBeNull();

		await Harness.NegativeAcknowledgeAsync(received, requeue: false).ConfigureAwait(false);

		var deadLetters = await Harness.ReadDeadLettersAsync(DefaultTimeout).ConfigureAwait(false);
		deadLetters.ShouldNotBeEmpty("Message should move to DLQ when not requeued");
		deadLetters.ShouldContain(dlq => dlq.Message.Id == message.Id);
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task MessagesShouldPreserveFifoOrdering()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var messages = Enumerable.Range(0, 5)
			.Select(index => TransportTestMessage.Create(id: $"msg-{index}"))
			.ToArray();

		foreach (var message in messages)
		{
			await Harness.PublishAsync(message).ConfigureAwait(false);
		}

		foreach (var expected in messages)
		{
			var received = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
			_ = received.ShouldNotBeNull();
			received.Message.Id.ShouldBe(expected.Id);
			await Harness.AcknowledgeAsync(received).ConfigureAwait(false);
		}
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task DuplicateMessagesShouldBeDeliveredOnce()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var original = TransportTestMessage.Create(id: "duplicate-id", body: "first");
		var duplicate = TransportTestMessage.Create(id: original.Id, body: "second");

		await Harness.PublishDuplicateAsync(original, duplicate).ConfigureAwait(false);

		var received = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = received.ShouldNotBeNull();
		received.Message.Id.ShouldBe(original.Id);
		await Harness.AcknowledgeAsync(received).ConfigureAwait(false);

		var extra = await Harness.ReceiveAsync(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
		extra.ShouldBeNull("Duplicate messages must not be delivered twice");
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task CloudEventsShouldRoundTripWithMetadata()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Id = Guid.NewGuid().ToString("N"),
			Subject = "transport-conformance",
			Type = "com.excalibur.dispatch.transport",
			Time = DateTimeOffset.UtcNow,
			DataContentType = "application/json",
			Data = new { message = "hello" },
		};

		cloudEvent.SetAttributeFromString("partitionkey", "demo");
		await Harness.PublishCloudEventAsync(cloudEvent).ConfigureAwait(false);

		var received = await Harness.ReceiveCloudEventAsync(DefaultTimeout).ConfigureAwait(false);
		_ = received.ShouldNotBeNull();
		received.Id.ShouldBe(cloudEvent.Id);
		received.Type.ShouldBe(cloudEvent.Type);
		received.Subject.ShouldBe(cloudEvent.Subject);
		received.Source.ShouldBe(cloudEvent.Source);
		_ = received.Time.ShouldNotBeNull();
		received["partitionkey"].ShouldBe("demo");
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task ReceiveShouldReturnNullWhenTimeoutExpiresWithoutMessage()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var received = await Harness.ReceiveAsync(TimeSpan.FromMilliseconds(75)).ConfigureAwait(false);

		received.ShouldBeNull("Receive must return null when no message is available before timeout.");
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task ReceiveShouldHonorCancellationToken()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => Harness.ReceiveAsync(DefaultTimeout, cts.Token).AsTask()).ConfigureAwait(false);
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task RequeueRetriesShouldIncreaseDeliveryAttemptBeforeDeadLetter()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var message = TransportTestMessage.Create();
		await Harness.PublishAsync(message).ConfigureAwait(false);

		var first = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = first.ShouldNotBeNull();
		first.DeliveryAttempt.ShouldBe(1);
		await Harness.NegativeAcknowledgeAsync(first, requeue: true).ConfigureAwait(false);

		var second = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = second.ShouldNotBeNull();
		second.DeliveryAttempt.ShouldBeGreaterThan(first.DeliveryAttempt);
		await Harness.NegativeAcknowledgeAsync(second, requeue: true).ConfigureAwait(false);

		var third = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = third.ShouldNotBeNull();
		third.DeliveryAttempt.ShouldBeGreaterThan(second.DeliveryAttempt);
		await Harness.NegativeAcknowledgeAsync(third, requeue: false).ConfigureAwait(false);

		var deadLetters = await Harness.ReadDeadLettersAsync(DefaultTimeout).ConfigureAwait(false);
		deadLetters.ShouldContain(dlq => dlq.Message.Id == message.Id);
	}

	[Fact]
	[Trait("Category", "TransportConformance")]
	public async Task PoisonMessagesShouldCaptureDeadLetterFailureMetadata()
	{
		await Fixture.PurgeAsync().ConfigureAwait(false);

		var message = TransportTestMessage.Create();
		await Harness.PublishAsync(message).ConfigureAwait(false);

		var received = await Harness.ReceiveAsync(DefaultTimeout).ConfigureAwait(false);
		_ = received.ShouldNotBeNull();

		await Harness.NegativeAcknowledgeAsync(received, requeue: false).ConfigureAwait(false);

		var deadLetter = (await Harness.ReadDeadLettersAsync(DefaultTimeout).ConfigureAwait(false))
			.Single(dlq => dlq.Message.Id == message.Id);

		deadLetter.Reason.ShouldNotBeNullOrWhiteSpace();
		deadLetter.DeadLetteredAtUtc.ShouldBeGreaterThan(received.EnqueuedAtUtc);
		deadLetter.TransportMetadata.ShouldContainKey("delivery-attempt");
	}
}
