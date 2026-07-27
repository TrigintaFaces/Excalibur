// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.PubSub;
using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Integration.Tests.Transport.GooglePubSub;

/// <summary>
/// jh5lnr — author≠impl NON-SKIPPED real-Pub/Sub-emulator lock (TestsDeveloper) for the poison-payload
/// settlement decision (<c>PoisonPayloadSettlement.ShouldDeadLetter</c>) as wired into
/// <see cref="PubSubTransportReceiver"/>. When an oversized (unprocessable) payload is pulled:
/// <list type="bullet">
/// <item><b>Dead-letter policy declared → Nack</b> (ack-deadline 0) — the message REAPPEARS on the
/// subscription so Pub/Sub can dead-letter it after its delivery attempts (never acked away).</item>
/// <item><b>No dead-letter policy → Ack-drop</b> — the message is REMOVED, breaking the otherwise-infinite
/// redelivery loop of an un-routable poison payload (liveness over a wedged subscription).</item>
/// </list>
/// Proven against a real Pub/Sub emulator (not a mock): the settlement's effect on message visibility is
/// the observable, per <c>verify-against-real-infra-not-mock</c>.
/// </summary>
/// <remarks>
/// <b>RED mutant:</b> invert <c>ShouldDeadLetter</c> (return <c>!hasDeadLetterPolicy</c>) ⇒ the DLQ case
/// drops and the no-DLQ case redelivers — both assertions flip. Container is a hard requirement (never skipped).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "GooglePubSub")]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Collection(GooglePubSubTransportCollection.Name)]
public sealed class GooglePubSubPoisonPayloadSettlementIntegrationShould : IAsyncLifetime
{
	private const string ProjectId = "test-project";
	private const int TinyMaxPayloadBytes = 8; // any real body exceeds this → forced poison path.

	private PubSubContainer? _container;
	private PublisherServiceApiClient? _publisherApi;
	private SubscriberServiceApiClient? _subscriberApi;

	public async ValueTask InitializeAsync()
	{
		_container = new PubSubBuilder().Build();
		await TestTimeouts.WithTimeout(
			_container.StartAsync(), TestTimeouts.ContainerStart, "PubSub emulator container start").ConfigureAwait(false);

		Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", _container.GetEmulatorEndpoint());

		_publisherApi = await new PublisherServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }
			.BuildAsync().ConfigureAwait(false);
		_subscriberApi = await new SubscriberServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }
			.BuildAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task Nack_PoisonPayload_ForRedelivery_WhenDeadLetterPolicyDeclared()
	{
		var subscription = await CreatePoisonMessageOnFreshSubscriptionAsync().ConfigureAwait(false);
		var receiver = new PubSubTransportReceiver(
			_subscriberApi!, subscription, NullLogger<PubSubTransportReceiver>.Instance,
			maxPayloadBytes: TinyMaxPayloadBytes, hasDeadLetterPolicy: true);

		// The receiver detects the oversized payload and settles it (Nack → deadline 0).
		var received = await receiver.ReceiveAsync(10, CancellationToken.None).ConfigureAwait(false);
		received.ShouldBeEmpty("an oversized poison payload is never surfaced to the handler.");

		(await RemainingMessageCountAsync(subscription).ConfigureAwait(false))
			.ShouldBe(1, "with a dead-letter policy the poison payload is Nack'd and REAPPEARS (so Pub/Sub can dead-letter it) — never acked away.");
	}

	[Fact]
	public async Task AckDrop_PoisonPayload_WhenNoDeadLetterPolicy()
	{
		var subscription = await CreatePoisonMessageOnFreshSubscriptionAsync().ConfigureAwait(false);
		var receiver = new PubSubTransportReceiver(
			_subscriberApi!, subscription, NullLogger<PubSubTransportReceiver>.Instance,
			maxPayloadBytes: TinyMaxPayloadBytes, hasDeadLetterPolicy: false);

		var received = await receiver.ReceiveAsync(10, CancellationToken.None).ConfigureAwait(false);
		received.ShouldBeEmpty("an oversized poison payload is never surfaced to the handler.");

		(await RemainingMessageCountAsync(subscription).ConfigureAwait(false))
			.ShouldBe(0, "with no dead-letter policy the un-routable poison payload is Ack-dropped, breaking the infinite redelivery loop.");
	}

	// Creates a fresh topic+subscription, publishes one over-sized (poison) message, returns the subscription name.
	private async Task<string> CreatePoisonMessageOnFreshSubscriptionAsync()
	{
		var topic = TopicName.FromProjectTopic(ProjectId, $"poison-topic-{Guid.NewGuid():N}");
		var subscription = SubscriptionName.FromProjectSubscription(ProjectId, $"poison-sub-{Guid.NewGuid():N}");

		_ = await _publisherApi!.CreateTopicAsync(topic).ConfigureAwait(false);
		_ = await _subscriberApi!.CreateSubscriptionAsync(
			subscription, topic, pushConfig: null, ackDeadlineSeconds: 10).ConfigureAwait(false);

		// A body well over TinyMaxPayloadBytes → the receiver's size guard forces the poison-settlement path.
		_ = await _publisherApi.PublishAsync(topic, [new PubsubMessage { Data = ByteString.CopyFromUtf8("this-payload-is-too-large-to-process") }])
			.ConfigureAwait(false);

		return subscription.ToString();
	}

	// Pulls (return-immediately) and Nacks anything seen, so counting never consumes the message under test.
	private async Task<int> RemainingMessageCountAsync(string subscription)
	{
		var response = await _subscriberApi!.PullAsync(
			new PullRequest { Subscription = subscription, MaxMessages = 10 }, CancellationToken.None).ConfigureAwait(false);

		if (response.ReceivedMessages.Count > 0)
		{
			// Release them back immediately (deadline 0) so the assertion is non-destructive.
			await _subscriberApi.ModifyAckDeadlineAsync(
				subscription, response.ReceivedMessages.Select(m => m.AckId), 0, CancellationToken.None).ConfigureAwait(false);
		}

		return response.ReceivedMessages.Count;
	}
}
