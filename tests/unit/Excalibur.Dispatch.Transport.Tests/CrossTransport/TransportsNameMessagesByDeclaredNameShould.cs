// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Grpc;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;

using global::RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// A message identity that leaves the process is the name the type DECLARES, never a name derived from
/// the CLR type. A CLR FullName carries the namespace and assembly, so a consumer refactoring their own
/// code silently changes the string the far side matches on -- and for RabbitMQ the far side may be
/// another organisation's broker. gRPC and RabbitMQ were the last two transports still stamping a
/// FullName; these arms hold the line.
/// </summary>
/// <remarks>
/// <para>
/// The safety arm uses a message whose declared name is deliberately NOT derivable from its CLR type,
/// so a regression to <c>GetType().FullName</c> cannot accidentally satisfy it.
/// </para>
/// <para>
/// The liveness arms matter as much: a type declaring no name must still send (falling back to the CLR
/// name) rather than throw, because <c>MessageNameHelper.GetName</c> throws and a send that has always
/// worked must not start failing. Without those arms the suite would be satisfied by a world in which
/// every publish threw.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TransportsNameMessagesByDeclaredNameShould
{
	private const string DeclaredName = "contoso.sales.order-placed.v1";

	[Fact]
	public async Task Grpc_StampsTheDeclaredName_NotTheClrFullName()
	{
		var sender = A.Fake<ITransportSender>();
		TransportMessage? sent = null;
		_ = A.CallTo(() => sender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes(call => sent = call.Arguments.Get<TransportMessage>(0))
			// The adapter refuses a send the sender reports as rejected. An unconfigured fake returns a
			// default SendResult, whose IsSuccess is false, so it reads as a refusal and this arm fails
			// before reaching its assertion. Returning a success keeps the arm about NAMING.
			.Returns(Task.FromResult(SendResult.Success("cross-transport-naming")));

		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		await using var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		await adapter.SendAsync(
			new DeclaredNameEvent(),
			"orders",
			A.Fake<IMessageContext>(),
			CancellationToken.None);

		_ = sent.ShouldNotBeNull();
		sent.MessageType.ShouldBe(DeclaredName);
		sent.MessageType.ShouldNotBe(typeof(DeclaredNameEvent).FullName);
	}

	[Fact]
	public async Task Grpc_FallsBackToTheClrName_WhenTheTypeDeclaresNone()
	{
		// Liveness: an undeclared type must still send. MessageNameHelper.GetName THROWS for such a
		// type, so a fix that reached for it would turn every publish of an unnamed message into a
		// failure -- a worse defect than the one being fixed.
		var sender = A.Fake<ITransportSender>();
		TransportMessage? sent = null;
		_ = A.CallTo(() => sender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes(call => sent = call.Arguments.Get<TransportMessage>(0))
			// The adapter refuses a send the sender reports as rejected. An unconfigured fake returns a
			// default SendResult, whose IsSuccess is false, so it reads as a refusal and this arm fails
			// before reaching its assertion. Returning a success keeps the arm about NAMING.
			.Returns(Task.FromResult(SendResult.Success("cross-transport-naming")));

		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		await using var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		await adapter.SendAsync(
			new UndeclaredNameEvent(),
			"orders",
			A.Fake<IMessageContext>(),
			CancellationToken.None);

		_ = sent.ShouldNotBeNull();
		sent.MessageType.ShouldBe(typeof(UndeclaredNameEvent).FullName);
	}

	[Fact]
	public async Task RabbitMq_StampsTheDeclaredName_WhenTheContextCarriesNoMessageType()
	{
		var messageType = await PublishThroughRabbitCloudEventsAsync(new DeclaredNameEvent(), contextMessageType: null);

		messageType.ShouldBe(DeclaredName);
		messageType.ShouldNotBe(typeof(DeclaredNameEvent).FullName);
	}

	[Fact]
	public async Task RabbitMq_FallsBackToTheClrName_WhenTheTypeDeclaresNone()
	{
		// Liveness, as above: an undeclared type must still publish.
		var messageType = await PublishThroughRabbitCloudEventsAsync(new UndeclaredNameEvent(), contextMessageType: null);

		messageType.ShouldBe(typeof(UndeclaredNameEvent).FullName);
	}

	[Fact]
	public async Task RabbitMq_KeepsAnInboundMessageTypeOverOurOwnDeclaredName()
	{
		// On a receive-then-re-emit round trip the context holds the ORIGINATING publisher's type
		// string. Restating that as our own declared name would rewrite another organisation's event
		// identity, so the context still wins where it has a value. CloudEventEnvelopeConverter makes
		// the same call for the same reason.
		var messageType = await PublishThroughRabbitCloudEventsAsync(
			new DeclaredNameEvent(), contextMessageType: "com.acme.order-placed.v3");

		messageType.ShouldBe("com.acme.order-placed.v3");
	}

	private static async Task<string?> PublishThroughRabbitCloudEventsAsync(
		IDispatchEvent evt,
		string? contextMessageType)
	{
		// The envelope is disposed by the bus in a finally block, so its MessageType is read inside the
		// bridge call -- while the envelope is still live -- rather than after the publish returns.
		var bridge = A.Fake<IEnvelopeCloudEventBridge>();
		var captured = false;
		string? capturedMessageType = null;
		_ = A.CallTo(() => bridge.ToTransportAsync<(IBasicProperties properties, ReadOnlyMemory<byte> body)>(
				A<MessageEnvelope>._, A<CloudEventMode>._, A<CancellationToken>._))
			.Invokes(call =>
			{
				capturedMessageType = call.Arguments.Get<MessageEnvelope>(0)!.MessageType;
				captured = true;
			})
			.Returns(Task.FromResult<(IBasicProperties, ReadOnlyMemory<byte>)>(
				(new global::RabbitMQ.Client.BasicProperties(), ReadOnlyMemory<byte>.Empty)));

		var channel = A.Fake<IChannel>();
		var mapper = A.Fake<ICloudEventEncoder<(IBasicProperties properties, ReadOnlyMemory<byte> body)>>();
		_ = A.CallTo(() => mapper.Options).Returns(new CloudEventOptions());

		var bus = new RabbitMqMessageBus(
			channel,
			A.Fake<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions { Exchange = "ex", RoutingKey = "rk" }),
			NullLogger<RabbitMqMessageBus>.Instance,
			bridge,
			mapper,
			// No CloudEvent options => no publisher-confirm tracker. The envelope's MessageType is
			// captured at the bridge, before any broker round trip, so confirms are not part of the
			// property under test and a fake IChannel never acks one.
			cloudEventOptions: null);

		var context = A.Fake<IMessageContext>();
		// IMessageContext.Items is IDictionary<string, object>, so the fake must return the
		// non-nullable value type. Supplying object? here differs only in nullability, which the
		// compiler reports as CS8620 rather than an error, so it would otherwise ride along unseen.
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		if (contextMessageType is not null)
		{
			context.SetMessageType(contextMessageType);
		}

		await bus.PublishAsync(evt, context, CancellationToken.None);

		captured.ShouldBeTrue("the CloudEvents publish path must reach the envelope bridge");
		return capturedMessageType;
	}

	[MessageName(DeclaredName)]
	private sealed record DeclaredNameEvent : IDispatchEvent;

	private sealed record UndeclaredNameEvent : IDispatchEvent;
}
