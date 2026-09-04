// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Grpc.Core;

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
	private const int MaxPulledMessages = 10;

	// Per-pull deadline. Must be OURS: an unbounded pull on an empty subscription never returns.
	private static readonly TimeSpan PullDeadline = TimeSpan.FromSeconds(2);

	// How long a redelivery is given to reappear before the subscription is judged empty.
	private static readonly TimeSpan RemainingCountWindow = TestTimeouts.Scale(TimeSpan.FromSeconds(6));

	private PubSubContainer? _container;
	private PublisherServiceApiClient? _publisherApi;
	private SubscriberServiceApiClient? _subscriberApi;

	public async ValueTask InitializeAsync()
	{
		_container = new PubSubBuilder().WithImage(TestContainerImages.GoogleCloudEmulators).Build();
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

	// Counts what is still outstanding on the subscription, Nacking anything seen so counting never
	// consumes the message under test.
	//
	// A Pull carries NO client-side deadline by default and ReturnImmediately is obsolete, so an empty
	// subscription long-polls until the transport aborts the RPC — the "zero outstanding" case then
	// surfaces as a cancelled RPC instead of the zero it is. The deadline must therefore be ours:
	//   - each pull is bounded, and a pull that expires with nothing available counts as zero (safety);
	//   - pulls are retried within a window, so a redelivery landing slightly after the Nack is still
	//     observed rather than read as zero (liveness).
	// Both arms of the settlement contract run through this one helper: the no-dead-letter case must
	// reach zero, and the dead-letter case must still see its redelivered message.
	private async Task<int> RemainingMessageCountAsync(string subscription)
	{
		var outstanding = 0;

		var observed = await WaitHelpers.WaitUntilAsync(
			async _ => (outstanding = await PullAndReleaseAsync(subscription).ConfigureAwait(false)) > 0,
			RemainingCountWindow,
			WaitHelpers.DefaultPollInterval).ConfigureAwait(false);

		// A false here means every bounded pull returned empty for the whole window — a real zero.
		// It can never mean "an RPC failed": a pull that fails for any reason other than OUR OWN
		// deadline propagates out of PullAndReleaseAsync and fails the test loudly.
		return observed ? outstanding : 0;
	}

	// One bounded, non-destructive pull. Returns 0 ONLY when our own deadline expired with nothing
	// available — never because the transport gave up.
	private async Task<int> PullAndReleaseAsync(string subscription)
	{
		using var pullCts = new CancellationTokenSource(PullDeadline);

		PullResponse response;

		try
		{
			response = await _subscriberApi!.PullAsync(
				new PullRequest { Subscription = subscription, MaxMessages = MaxPulledMessages },
				pullCts.Token).ConfigureAwait(false);
		}
		catch (RpcException) when (pullCts.IsCancellationRequested)
		{
			// OUR deadline fired, so the subscription had nothing to give within the bound: zero.
			//
			// The discriminator is deliberately "did WE cancel it", NOT the gRPC status code. The
			// original defect of this helper was a transport-initiated `Cancelled` (HTTP-2 CANCEL)
			// raised with no external cancellation at all — so matching on `Cancelled` would map a
			// broken transport onto the same zero as a healthy empty subscription, and the arm that
			// asserts zero could no longer tell an earned pass from an infrastructure failure.
			// Anything we did not cause propagates.
			return 0;
		}

		if (response.ReceivedMessages.Count > 0)
		{
			// Release them back immediately (deadline 0) so the assertion is non-destructive.
			await _subscriberApi!.ModifyAckDeadlineAsync(
				subscription, response.ReceivedMessages.Select(m => m.AckId), 0, CancellationToken.None).ConfigureAwait(false);
		}

		return response.ReceivedMessages.Count;
	}
}
