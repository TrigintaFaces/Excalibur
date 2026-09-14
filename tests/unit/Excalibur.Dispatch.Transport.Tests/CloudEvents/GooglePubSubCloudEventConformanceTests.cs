// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

using Google.Cloud.PubSub.V1;

using Google.Protobuf;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the Google Pub/Sub transport.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion lives in the kit. This class supplies only the three Pub/Sub-specific things: the
/// receiver a consumer resolves, a way to put a message in front of it, and this transport's own
/// encoding.
/// </para>
/// <para>
/// <b>No infrastructure.</b> The vendor client is faked and registered under the transport's key, which
/// the registration now consults before falling back to the SDK's default — so the receiver under test
/// is built by the transport's own factory, carrying the decode decoration that factory applies.
/// </para>
/// <para>
/// <b>This suite could not be written at all until that lookup existed</b>, and the reason is worth
/// stating because it was a consumer-facing gap rather than a testing inconvenience: the factory
/// constructed its client inline, so the endpoint and credentials were settled before a consumer could
/// speak. Anyone pointing this transport at the Pub/Sub emulator, or at a non-default service account,
/// had no seam to do it through. That the conformance kit could not reach the receiver either is the
/// same fact observed from inside.
/// </para>
/// <para>
/// <b>The encoder is RESOLVED, not constructed</b>, for the reason the sibling suites give: constructing
/// an encoder by hand tests an encoder nobody runs. CloudEvents is opt-in here, so the fixture makes
/// both calls a CloudEvents consumer makes — the transport registration and the CloudEvents binding.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GooglePubSubCloudEventConformanceTests : CloudEventTransportConformanceTests, IAsyncDisposable
{
	private const string TransportName = "pubsub-conformance";

	private readonly List<ReceivedMessage> _inbound = [];

	private ServiceProvider? _provider;

	/// <inheritdoc />
	protected override Task<ITransportReceiver> CreateReceiverAsync() =>
		Task.FromResult(BuildProvider().GetRequiredKeyedService<ITransportReceiver>(TransportName));

	/// <inheritdoc />
	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		_inbound.Clear();

		var ackId = 1;

		foreach (var message in messages)
		{
			var pubsubMessage = new PubsubMessage
			{
				MessageId = message.Id,
				Data = ByteString.CopyFrom(message.Body.ToArray()),
			};

			// Pub/Sub carries every CloudEvents attribute, and the structured media type with them, as
			// message ATTRIBUTES - there is no separate envelope field for a content type on this
			// transport, which is why the encoding below can be fed back through properties alone.
			foreach (var property in message.Properties)
			{
				pubsubMessage.Attributes[property.Key] = property.Value?.ToString() ?? string.Empty;
			}

			_inbound.Add(new ReceivedMessage
			{
				AckId = $"ack-{ackId++}",
				Message = pubsubMessage,
				DeliveryAttempt = 1,
			});
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		var encoder = BuildProvider().GetRequiredService<ICloudEventEncoder<PubsubMessage>>();

		var encoded = await encoder.ToTransportMessageAsync(cloudEvent, mode, CancellationToken.None);

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var attribute in encoded.Attributes)
		{
			properties[attribute.Key] = attribute.Value;
		}

		return new TransportReceivedMessage
		{
			Id = "pubsub-wire-1",
			Body = encoded.Data.ToByteArray(),
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = properties,
		};
	}

	private ServiceProvider BuildProvider()
	{
		if (_provider is not null)
		{
			return _provider;
		}

		var client = A.Fake<SubscriberServiceApiClient>();

		_ = A.CallTo(() => client.PullAsync(A<PullRequest>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var response = new PullResponse();

				if (_inbound.Count > 0)
				{
					response.ReceivedMessages.Add(_inbound[0]);
					_inbound.RemoveAt(0);
				}

				// An empty response is how the receiver learns the subscription has nothing ready, which
				// is exactly what a real pull returns against a drained subscription.
				return Task.FromResult(response);
			});

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Registered under the transport's key, which is the seam the registration consults first; a
		// consumer pointing this transport at the emulator registers a client the same way.
		_ = services.AddKeyedSingleton(TransportName, client);

		// A project and a subscription are REQUIRED by this transport's options validator, and the
		// receiver is only registered at all when a subscription is configured - so supplying them is
		// part of testing the consumer's path rather than a way around it. Nothing connects.
		_ = services.AddGooglePubSubTransport(TransportName, pubsub => pubsub
			.ProjectId("conformance-project")
			.SubscriptionId("conformance-subscription"));

		_ = services.AddCloudEventsForPubSub();

		_provider = services.BuildServiceProvider();

		return _provider;
	}

	/// <summary>
	/// LIVENESS. Binary-mode round trip through this transport's own send and receive paths.
	/// </summary>
	[Fact]
	public Task Round_trip_a_binary_mode_event() =>
		VerifyBinaryModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// LIVENESS. Structured mode is a different wire shape and a different decode path.
	/// </summary>
	[Fact]
	public Task Round_trip_a_structured_mode_event() =>
		VerifyStructuredModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// SAFETY. Ordinary Pub/Sub traffic is not reported as a CloudEvent.
	/// </summary>
	[Fact]
	public Task Leave_ordinary_traffic_alone() =>
		VerifyOrdinaryTrafficIsNotReportedAsCloudEvent();

	/// <summary>
	/// SAFETY. A mode this transport declines is not decoded anyway.
	/// </summary>
	[Fact]
	public Task Decline_no_mode_it_cannot_decode() =>
		VerifyDeclinedModesDoNotDecode();

	/// <inheritdoc />
	/// <remarks>
	/// ASYNC disposal: the container resolves a receiver decorator implementing only
	/// <see cref="IAsyncDisposable"/>, so a synchronous Dispose throws in teardown.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		if (_provider is not null)
		{
			await _provider.DisposeAsync();
		}

		GC.SuppressFinalize(this);
	}
}
