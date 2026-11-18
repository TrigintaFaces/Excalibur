// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Covers the inbound half of Pub/Sub body compression: that a body the producer compressed is
/// decompressed on receive, and that decompression cannot be made to allocate without bound.
/// </summary>
/// <remarks>
/// <para>
/// The two properties are deliberately in one file because each is the other's failure mode. A receive
/// path that never decompresses is perfectly safe against an expansion attack and silently breaks every
/// compressed message; a receive path that decompresses without a bound reads every message and lets a
/// few hundred bytes name an arbitrary allocation. Asserting either alone is satisfied by the defect the
/// other arm exists to catch.
/// </para>
/// <para>
/// The limits here are small (1 KiB) so the arms are fast and the boundary is unambiguous. The shipped
/// default is 4 MiB; <c>BoundTheDecompressedBodyByDefault</c> pins that separately, because a limit whose
/// default is "no limit" is inert for every consumer who never sets one.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PubSubInboundDecompressionShould
{
	private const string TestSubscription = "projects/test-project/subscriptions/orders-sub";
	private const int SmallLimit = 1024;

	[Fact]
	public async Task DecompressTheBodyWhenTheProducerMarkedItCompressed()
	{
		var payload = System.Text.Encoding.UTF8.GetBytes(new string('a', 512));
		var fakeSubscriber = PullOnce(Compressed(payload));
		var receiver = Receiver(fakeSubscriber, SmallLimit);

		var messages = await receiver.ReceiveAsync(1, CancellationToken.None);

		// The liveness half, and the one the half-wire failed: the producer-side encoder for this
		// transport compresses bodies over a threshold and records the algorithm in an attribute. A
		// receive path that ignored the attribute handed the deserializer compressed bytes.
		messages.Count.ShouldBe(1);
		messages[0].Body.ToArray().ShouldBe(payload);
	}

	[Fact]
	public async Task LeaveAnUncompressedBodyUnchanged()
	{
		var payload = System.Text.Encoding.UTF8.GetBytes("plain body");
		var message = new PubsubMessage { MessageId = "m1", Data = ByteString.CopyFrom(payload) };
		var receiver = Receiver(PullOnce(message), SmallLimit);

		var messages = await receiver.ReceiveAsync(1, CancellationToken.None);

		// The control for the arm above. Without it, a receive path that decompressed unconditionally —
		// or one that returned the wrong buffer on the not-compressed branch — would still pass.
		messages.Count.ShouldBe(1);
		messages[0].Body.ToArray().ShouldBe(payload);
	}

	[Fact]
	public async Task SettleAMessageThatExpandsBeyondTheLimitInsteadOfMaterializingIt()
	{
		// 64 KiB of zeros compresses to a few hundred bytes, so the inbound length check the receiver
		// already performed passes comfortably. Only an output bound rejects this.
		var bomb = Compressed(new byte[64 * 1024]);
		bomb.Data.Length.ShouldBeLessThan(SmallLimit);

		var fakeSubscriber = PullOnce(bomb);
		var receiver = Receiver(fakeSubscriber, SmallLimit);

		var messages = await receiver.ReceiveAsync(1, CancellationToken.None);

		// Settled as an unprocessable payload, never returned. With no dead-letter policy declared the
		// receiver acks to drop it, which is the existing poison-message decision — this arm asserts the
		// refusal reaches that decision rather than being swallowed or allocated.
		messages.ShouldBeEmpty();
		A.CallTo(() => fakeSubscriber.AcknowledgeAsync(
				TestSubscription,
				A<IEnumerable<string>>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RefuseASnappyPayloadWhoseDeclaredLengthExceedsTheLimit()
	{
		var compressed = GooglePubSubCompression.Compress(new byte[64 * 1024], CompressionAlgorithm.Snappy);

		// Snappy carries the uncompressed length in its own header. That number is the sender's claim and
		// nothing has verified it, so it must be refused before a buffer is sized from it.
		_ = Should.Throw<PayloadTooLargeException>(
			() => GooglePubSubCompression.Decompress(compressed, CompressionAlgorithm.Snappy, SmallLimit));
	}

	[Fact]
	public void RefuseAnOverLimitPayloadThroughAutoDetectionRatherThanPassingItThrough()
	{
		var bomb = Compressed(new byte[64 * 1024]);
		_ = bomb.Attributes.Remove(GooglePubSubMessageAttributes.Compression);

		// Auto-detection catches decompression failures and treats them as "not actually compressed".
		// A size refusal is not that: the magic bytes matched and the payload really is compressed, so
		// returning the still-compressed bytes would hand the deserializer something that is not the
		// message, with nothing recording that a limit fired.
		_ = Should.Throw<PayloadTooLargeException>(
			() => GooglePubSubMessageBodyCodec.TryDecodeBody(
				bomb,
				enableAutoDetection: true,
				maxDecompressedBytes: SmallLimit,
				out _,
				out _));
	}

	[Fact]
	public void BoundTheDecompressedBodyByDefault() =>
		// A limit that defaults to "unbounded" protects only consumers who already knew to set it, while
		// the option reads as though a protection were configured.
		new CloudEventTransportOptions().MaxDecompressedBytes.ShouldBe(PayloadSizeGuard.DefaultMaxPayloadBytes);

	private static PubsubMessage Compressed(byte[] payload)
	{
		var compressed = GooglePubSubCompression.Compress(payload, CompressionAlgorithm.Gzip);
		var message = new PubsubMessage { MessageId = "m1", Data = ByteString.CopyFrom(compressed) };
		message.Attributes[GooglePubSubMessageAttributes.Compression] = CompressionAlgorithm.Gzip.ToString();
		return message;
	}

	private static ISubscriberApiClientSeam PullOnce(PubsubMessage message)
	{
		var fake = A.Fake<ISubscriberApiClientSeam>();
		A.CallTo(() => fake.PullAsync(A<PullRequest>._, A<CancellationToken>._))
			.Returns(new PullResponse
			{
				ReceivedMessages =
				{
					new ReceivedMessage { AckId = "ack-1", Message = message, DeliveryAttempt = 1 },
				},
			});
		return fake;
	}

	private static PubSubTransportReceiver Receiver(ISubscriberApiClientSeam client, int? maxPayloadBytes) =>
		new(
			client,
			TestSubscription,
			NullLogger<PubSubTransportReceiver>.Instance,
			maxPayloadBytes: maxPayloadBytes);
}
