// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

using DotPulsar;
using DotPulsar.Abstractions;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the Pulsar transport.
/// </summary>
/// <remarks>
/// <para>
/// <b>This transport is STRUCTURED-ONLY, and that is a contract rather than a gap.</b> Its registration
/// binds structured-only decoding and its sender applies the shared encoding decorator, which publishes
/// structured JSON — encode and decode agreeing. Declaring the narrower set here is therefore honest, and
/// it is not free: the kit requires a declined mode to actually fail to decode, so a declaration made to
/// dodge a red arm produces a different red instead.
/// </para>
/// <para>
/// <b>This transport has no bespoke adapter.</b> Where SQS and Kafka each own an encoder, Pulsar carries
/// the shared one, so the encoding seam here drives that decorator over a substituted sender and captures
/// what it produced. All of it is public surface — the decorator type is internal, the extension that
/// applies it is not.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PulsarCloudEventConformanceTests : CloudEventTransportConformanceTests
{
	private const string TransportName = "pulsar-conformance";

	private readonly List<IMessage<byte[]>> _inbound = [];

	/// <inheritdoc />
	/// <remarks>
	/// Structured only. See the class remarks: this is the transport's registered contract, and the
	/// declined-mode arm holds the declaration to it.
	/// </remarks>
	protected override IReadOnlyCollection<CloudEventMode> SupportedModes => [CloudEventMode.Structured];

	/// <inheritdoc />
	protected override Task<ITransportReceiver> CreateReceiverAsync()
	{
		var consumer = A.Fake<IConsumer<byte[]>>();

		_ = A.CallTo(() => consumer.Receive(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (_inbound.Count == 0)
				{
					// The receiver drains until nothing is ready; cancellation is how it learns that.
					throw new OperationCanceledException();
				}

				var next = _inbound[0];
				_inbound.RemoveAt(0);
				return new ValueTask<IMessage<byte[]>>(next);
			});

		var client = A.Fake<IPulsarClient>();
		_ = A.CallTo(() => client.CreateConsumer(A<ConsumerOptions<byte[]>>._)).Returns(consumer);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKeyedSingleton(TransportName, client);
		_ = services.AddPulsarTransport(TransportName, _ => { });

		var provider = services.BuildServiceProvider();

		return Task.FromResult(provider.GetRequiredKeyedService<ITransportReceiver>(TransportName));
	}

	/// <inheritdoc />
	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		_inbound.Clear();

		foreach (var message in messages)
		{
			var properties = message.Properties.ToDictionary(
				static pair => pair.Key,
				static pair => pair.Value?.ToString() ?? string.Empty,
				StringComparer.Ordinal);

			// This transport puts the media type on the wire as an ordinary property, which is where its
			// own receiver looks for it. Mirroring that here is what makes the fixture faithful.
			if (message.ContentType is not null)
			{
				properties["content-type"] = message.ContentType;
			}

			var body = message.Body.ToArray();

			var pulsarMessage = A.Fake<IMessage<byte[]>>();
			_ = A.CallTo(() => pulsarMessage.Properties).Returns(properties);
			_ = A.CallTo(() => pulsarMessage.Value()).Returns(body);

			_inbound.Add(pulsarMessage);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		if (mode != CloudEventMode.Structured)
		{
			// This transport has no encoder for any other mode - it carries the shared structured
			// decorator and nothing else - so it cannot emit one, which is why its structured-only
			// receiver is not an emit-without-decode gap.
			throw new NotSupportedException($"Pulsar encodes structured mode only; asked for {mode}.");
		}

		TransportMessage? captured = null;

		var inner = A.Fake<ITransportSender>();
		_ = A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage sent, CancellationToken _) => captured = sent)
			.Returns(Task.FromResult(SendResult.Success("pulsar-conformance-1")));

		var encoding = inner.WithCloudEventEncoding();

		var outbound = new TransportMessage
		{
			Body = Payload,
			Properties = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["cloudevent"] = cloudEvent,
			},
		};

		_ = await encoding.SendAsync(outbound, CancellationToken.None);

		var wire = captured ?? outbound;

		return new TransportReceivedMessage
		{
			Id = "pulsar-wire-1",
			Body = wire.Body,
			// Structured mode is identified by its MEDIA TYPE, and this transport carries that on the
			// message envelope rather than among the properties. Dropping it here produced a false RED
			// that read exactly like a decode failure.
			ContentType = wire.ContentType,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = wire.Properties
				.Where(static pair => pair.Value is not CloudEvent)
				.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
		};
	}

	/// <summary>
	/// LIVENESS. Structured-mode round trip through Pulsar's own send and receive paths.
	/// </summary>
	[Fact]
	public Task Round_trip_a_structured_mode_event() =>
		VerifyStructuredModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// SAFETY. Ordinary Pulsar traffic is not reported as a CloudEvent.
	/// </summary>
	[Fact]
	public Task Leave_ordinary_traffic_alone() =>
		VerifyOrdinaryTrafficIsNotReportedAsCloudEvent();

	/// <summary>
	/// SAFETY. Binary is declined by this transport, so it must not decode one.
	/// </summary>
	[Fact]
	public Task Decline_no_mode_it_cannot_decode() =>
		VerifyDeclinedModesDoNotDecode();
}
