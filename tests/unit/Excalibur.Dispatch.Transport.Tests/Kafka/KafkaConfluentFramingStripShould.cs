// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Author≠impl WIRE read-through lock (bead 6v59o0-A) for the Confluent Schema Registry framing strip on
/// the Kafka RECEIVE path. When a transport is schema-registry-configured (<c>decodeConfluentFraming:
/// true</c>), an inbound Confluent-framed payload (magic byte <c>0x00</c> + 4-byte schema id) must have its
/// 5-byte header stripped so the downstream deserializer receives the raw payload — the .NET message type
/// travels in the <c>message-type</c> header, so the schema id is not needed to deserialize.
/// </summary>
/// <remarks>
/// This drives the real <see cref="KafkaTransportReceiver.ReceiveAsync"/> path against a faked
/// <see cref="IConsumer{TKey,TValue}"/> (per verify-against-real-infra: the external broker is not under
/// test — only OUR framing strip on the receive path is), structurally proving
/// <c>MaterializeBody</c> is invoked end-to-end rather than merely that the strip helper exists.
/// Non-vacuity: the SAME framed payload with <c>decodeConfluentFraming: false</c> retains the header (RED
/// against the strip), and a non-framed payload with decoding enabled passes through untouched. A
/// non-skipped TestContainers produce→consume complement is the stronger form when Docker is available.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaConfluentFramingStripShould
{
	private const string Topic = "orders-topic";
	private const int SchemaId = 42;

	private static readonly byte[] Payload = Encoding.UTF8.GetBytes("{\"orderId\":\"o-1\",\"total\":42}");

	private static byte[] Framed(byte[] payload, int schemaId)
	{
		var framed = new byte[ConfluentWireFormat.HeaderSize + payload.Length];
		ConfluentWireFormat.WriteHeader(framed, schemaId);
		payload.CopyTo(framed, ConfluentWireFormat.HeaderSize);
		return framed;
	}

	private static KafkaConsumeResult Record(byte[] value, long offset) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = "m1", Value = value },
		};

	private static KafkaTransportReceiver CreateReceiver(IConsumer<string, byte[]> consumer, bool decodeFraming) =>
		new(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance,
			maxPayloadBytes: null, decodeConfluentFraming: decodeFraming);

	private static IConsumer<string, byte[]> ConsumerYielding(byte[] value)
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var seq = new Queue<KafkaConsumeResult?>([Record(value, offset: 3), null]);
		A.CallTo(() => consumer.Consume(A<TimeSpan>._)).ReturnsLazily(() => seq.Dequeue());
		return consumer;
	}

	[Fact]
	public async Task StripConfluentFraming_OnReceivePath_WhenDecodingEnabled()
	{
		// Arrange — a Confluent-framed inbound payload, transport schema-registry-configured.
		var consumer = ConsumerYielding(Framed(Payload, SchemaId));

		// Act — drive the real ReceiveAsync path.
		var messages = await CreateReceiver(consumer, decodeFraming: true).ReceiveAsync(1, CancellationToken.None);

		// Assert — the 5-byte header is stripped; the downstream body is the raw payload.
		messages.Count.ShouldBe(1);
		messages[0].Body.ToArray().ShouldBe(Payload);
	}

	[Fact]
	public async Task PassFramedPayloadThrough_WhenDecodingDisabled()
	{
		// Non-vacuity: with decoding OFF, the identical framed payload keeps its 5-byte header — proving the
		// strip is gated on decodeConfluentFraming and the assertion above is not vacuously true.
		var framed = Framed(Payload, SchemaId);
		var consumer = ConsumerYielding(framed);

		var messages = await CreateReceiver(consumer, decodeFraming: false).ReceiveAsync(1, CancellationToken.None);

		messages.Count.ShouldBe(1);
		messages[0].Body.ToArray().ShouldBe(framed);
		messages[0].Body.Length.ShouldBe(Payload.Length + ConfluentWireFormat.HeaderSize);
	}

	[Fact]
	public async Task PassNonFramedPayloadThrough_WhenDecodingEnabled()
	{
		// A non-Confluent-framed payload (no magic byte) must pass through untouched even with decoding on —
		// the strip only triggers when the wire-format header is actually present.
		var consumer = ConsumerYielding(Payload);

		var messages = await CreateReceiver(consumer, decodeFraming: true).ReceiveAsync(1, CancellationToken.None);

		messages.Count.ShouldBe(1);
		messages[0].Body.ToArray().ShouldBe(Payload);
	}
}
