// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.Logging.Abstractions;

using MQTTnet;
using MQTTnet.Packets;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// Unit tests for <see cref="MqttTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="MqttTransportSenderShould"/> for why the pre-connect <c>null</c> is intended behavior,
/// not a silent capability decline.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class MqttTransportReceiverShould : IAsyncDisposable
{
	private readonly IMqttConnectionProvider _fakeConnectionProvider = A.Fake<IMqttConnectionProvider>();
	private readonly MqttOptions _options = new() { Host = "broker.example.com", ClientId = "test-client", Topic = "orders/topic" };
	private readonly MqttTransportReceiver _sut;

	public MqttTransportReceiverShould()
	{
		_sut = new MqttTransportReceiver(_fakeConnectionProvider, _options, NullLogger<MqttTransportReceiver>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_source_from_options_topic()
	{
		_sut.Source.ShouldBe(_options.Topic);
	}

	[Fact]
	public void Throw_when_connection_provider_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportReceiver(null!, _options, NullLogger<MqttTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_options_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportReceiver(_fakeConnectionProvider, null!, NullLogger<MqttTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MqttTransportReceiver(_fakeConnectionProvider, _options, null!));
	}

	[Fact]
	public void Return_null_before_connecting()
	{
		// The client connects lazily on first receive; before that, no capability has been established yet.
		var result = _sut.GetService(typeof(MQTTnet.IMqttClient));
		result.ShouldBeNull();
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_when_GetService_type_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	[Fact]
	public async Task PopulateContentTypeAndProperties_FromTheWireMessage()
	{
		// esue64: the receiver used to discard ContentType and UserProperties entirely, making
		// binary-mode CloudEvents (which live in MQTT v5 user properties) structurally undetectable.
		var message = new MqttApplicationMessage
		{
			Topic = _options.Topic,
			ContentType = "application/cloudevents+json",
			UserProperties = [new MqttUserProperty("ce-type", "com.excalibur.test.v1")],
		};
		var args = new MqttApplicationMessageReceivedEventArgs(
			"client-1",
			message,
			new MqttPublishPacket(),
			static (_, _) => Task.CompletedTask);

		var handler = typeof(MqttTransportReceiver).GetMethod(
			"OnMessageReceivedAsync",
			BindingFlags.NonPublic | BindingFlags.Instance)!;
		await (Task)handler.Invoke(_sut, [args])!;

		var received = await _sut.ReceiveAsync(maxMessages: 1, CancellationToken.None);

		received.Count.ShouldBe(1);
		received[0].ContentType.ShouldBe("application/cloudevents+json");
		received[0].Properties.ShouldContainKey("ce-type");
		received[0].Properties["ce-type"].ShouldBe("com.excalibur.test.v1");
	}

	/// <summary>
	/// SAFETY. Two deliveries that share a correlation id settle independently: acknowledging one must
	/// acknowledge that one and leave the other outstanding.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A correlation id is <b>shared by related messages by design</b> — that is what it is for — so when
	/// the delivery handle was keyed by it, two ordinary related messages collided in the pending map. The
	/// second overwrote the first, acknowledging the first acknowledged the <b>second</b>, and the first
	/// was left with no handle at all: one message settled without ever being processed, the other
	/// redelivered on session resume. No concurrency and no network fault were required.
	/// </para>
	/// <para>
	/// <b>Why this arm exists when a delivery-id-uniqueness arm already does.</b> Unique ids are the
	/// mechanism; independent settlement is the requirement. An id generator can be perfect while the
	/// settlement map is keyed off something else entirely, so proving the generator proves the proxy. This
	/// drives two real deliveries through the receiver's own receive path and settles them through its own
	/// public contract, which is the property a consumer depends on.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Acknowledge_only_the_requested_delivery_when_two_share_a_correlation_id()
	{
		var acknowledged = new List<string>();
		const string SharedCorrelation = "order-42";

		await DeliverAsync("first", SharedCorrelation, acknowledged);
		await DeliverAsync("second", SharedCorrelation, acknowledged);

		var received = await _sut.ReceiveAsync(maxMessages: 2, CancellationToken.None);
		received.Count.ShouldBe(2, "both deliveries must be buffered before either is settled, or the "
			+ "collision this arm exists to catch cannot occur.");

		var first = Delivery(received, "first");
		var second = Delivery(received, "second");

		// Both carry the SAME correlation -- this is the condition, not an accident of the fixture.
		first.CorrelationId.ShouldBe(SharedCorrelation);
		second.CorrelationId.ShouldBe(SharedCorrelation);
		first.Id.ShouldNotBe(second.Id);

		await _sut.AcknowledgeAsync(first, CancellationToken.None);

		acknowledged.ShouldBe(["first"], "acknowledging the first delivery settled a different packet. "
			+ "The broker has now PUBACK'd a message the consumer never processed.");

		// LIVENESS: the other handle still exists and still works. Without this the arm is satisfied by a
		// receiver that drops every handle on first settle -- safe, and useless.
		await _sut.AcknowledgeAsync(second, CancellationToken.None);
		acknowledged.ShouldBe(["first", "second"]);
	}

	/// <summary>
	/// SAFETY. The same independence holds when the deliveries are settled in reverse arrival order.
	/// </summary>
	/// <remarks>
	/// Settling newest-first is the ordinary case for a consumer that completes work out of order, and it
	/// is the direction in which a last-write-wins map looks correct: the surviving handle happens to be
	/// the one being asked for, so an arm that only ever acknowledges the newest delivery passes against
	/// the defect.
	/// </remarks>
	[Fact]
	public async Task Acknowledge_only_the_requested_delivery_when_settled_in_reverse_order()
	{
		var acknowledged = new List<string>();
		const string SharedCorrelation = "order-99";

		await DeliverAsync("first", SharedCorrelation, acknowledged);
		await DeliverAsync("second", SharedCorrelation, acknowledged);

		var received = await _sut.ReceiveAsync(maxMessages: 2, CancellationToken.None);
		received.Count.ShouldBe(2);

		await _sut.AcknowledgeAsync(Delivery(received, "second"), CancellationToken.None);
		acknowledged.ShouldBe(["second"]);

		await _sut.AcknowledgeAsync(Delivery(received, "first"), CancellationToken.None);
		acknowledged.ShouldBe(["second", "first"]);
	}

	/// <summary>
	/// SAFETY. Rejecting one delivery withholds that acknowledgement only — its sibling stays settleable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Keyed by correlation, a reject dropped the sibling's handle too, so rejecting one message silently
	/// stranded another: never acknowledged, never redelivered under that handle, and invisible to the
	/// consumer. The property is that a settlement reaches exactly the delivery it names.
	/// </para>
	/// <para>
	/// This arm uses <c>requeue: true</c>, which is the outcome that withholds the acknowledgement, so
	/// "acknowledges nothing" is the right assertion for it. The <c>requeue: false</c> outcome deliberately
	/// DOES acknowledge — carrying a failure reason code, which is how MQTT suppresses a redelivery — and is
	/// covered by the arm below.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Reject_only_the_requested_delivery_and_leave_its_sibling_settleable()
	{
		var acknowledged = new List<string>();
		const string SharedCorrelation = "order-7";

		await DeliverAsync("first", SharedCorrelation, acknowledged);
		await DeliverAsync("second", SharedCorrelation, acknowledged);

		var received = await _sut.ReceiveAsync(maxMessages: 2, CancellationToken.None);
		received.Count.ShouldBe(2);

		await _sut.RejectAsync(Delivery(received, "first"), "poison", requeue: true, CancellationToken.None);

		// SAFETY: a requeue withholds the acknowledgement, so nothing is acknowledged.
		acknowledged.ShouldBeEmpty();

		// LIVENESS: the sibling's handle survived the reject and still settles.
		await _sut.AcknowledgeAsync(Delivery(received, "second"), CancellationToken.None);
		acknowledged.ShouldBe(["second"]);
	}

	/// <summary>
	/// SAFETY. Rejecting WITHOUT requeue acknowledges exactly the rejected delivery — and only it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the outcome that suppresses redelivery, and it does so by sending the acknowledgement with a
	/// failure reason code rather than by withholding it. The receiver previously withheld the
	/// acknowledgement for BOTH values of <c>requeue</c>, so a caller asking for no redelivery got a
	/// redelivery and was told it had succeeded.
	/// </para>
	/// <para>
	/// The sibling assertion is the half that matters here: settling by a failure code must still reach
	/// exactly one delivery. A reject that acknowledged the whole correlation group would satisfy the first
	/// assertion and strand the sibling.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Acknowledge_only_the_rejected_delivery_when_requeue_is_refused()
	{
		var acknowledged = new List<string>();
		const string SharedCorrelation = "order-9";

		await DeliverAsync("first", SharedCorrelation, acknowledged);
		await DeliverAsync("second", SharedCorrelation, acknowledged);

		var received = await _sut.ReceiveAsync(maxMessages: 2, CancellationToken.None);
		received.Count.ShouldBe(2);

		await _sut.RejectAsync(Delivery(received, "first"), "poison", requeue: false, CancellationToken.None);

		// SAFETY: the settlement reached the rejected delivery and no other.
		acknowledged.ShouldBe(
			["first"],
			"rejecting without requeue must settle exactly the delivery it names: withholding the "
			+ "acknowledgement instead redelivers a message the caller asked never to see again, and "
			+ "settling the sibling too strands a message that was never processed");

		// LIVENESS: the sibling is untouched and still settles under its own handle.
		await _sut.AcknowledgeAsync(Delivery(received, "second"), CancellationToken.None);
		acknowledged.ShouldBe(["first", "second"]);
	}

	/// <summary>
	/// PRECISION. Deliveries carrying no correlation at all are also settled independently.
	/// </summary>
	/// <remarks>
	/// With the defect present the empty correlation hashed to one shared key, so every uncorrelated
	/// delivery collided with every other — the widest form of the same fault, and the one a producer that
	/// never sets a correlation id would hit on every pair of messages.
	/// </remarks>
	[Fact]
	public async Task Settle_uncorrelated_deliveries_independently()
	{
		var acknowledged = new List<string>();

		await DeliverAsync("first", correlationId: null, acknowledged);
		await DeliverAsync("second", correlationId: null, acknowledged);

		var received = await _sut.ReceiveAsync(maxMessages: 2, CancellationToken.None);
		received.Count.ShouldBe(2);
		Delivery(received, "first").Id.ShouldNotBe(Delivery(received, "second").Id);

		await _sut.AcknowledgeAsync(Delivery(received, "first"), CancellationToken.None);
		acknowledged.ShouldBe(["first"]);
	}

	/// <summary>
	/// LIVENESS. An ordinary single delivery is acknowledged, and its handle is RELEASED afterwards.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other arm in this file asserts that settling one delivery does NOT settle another. All of them
	/// are satisfied by a receiver that never acknowledges anything at all, and equally by one that never
	/// stores a handle in the first place — inaction is the cheapest way to keep two deliveries from
	/// interfering. This arm is the counterweight: the ordinary case must still work.
	/// </para>
	/// <para>
	/// The second half is the one worth having. The pending map is private, so "the handle was removed" is
	/// asserted through the property it produces rather than by reaching for the field: settling the SAME
	/// delivery twice must acknowledge the underlying packet exactly once. A receiver that acknowledges and
	/// then leaves the handle in place would pass the first assertion and fail this one, and it is a real
	/// defect — the map grows for the life of the connection and a late second settle re-acknowledges a
	/// packet the broker has already released.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Acknowledge_an_ordinary_delivery_once_and_release_its_handle()
	{
		var acknowledged = new List<string>();

		await DeliverAsync("only", correlationId: "corr-ordinary", acknowledged);

		var received = await _sut.ReceiveAsync(maxMessages: 1, CancellationToken.None);
		received.Count.ShouldBe(1);

		await _sut.AcknowledgeAsync(received[0], CancellationToken.None);

		// LIVENESS: the ordinary path still settles.
		acknowledged.ShouldBe(["only"], "an ordinary single delivery was not acknowledged at all");

		// AND the handle is gone: a repeat settle must not reach the packet a second time. The repeat now
		// RAISES rather than returning quietly -- the packet is still acknowledged exactly once, and the
		// caller is additionally told that its second settlement did not happen. Returning quietly made
		// "already settled" and "settled just now" the same observation.
		_ = await Should.ThrowAsync<TransportSettlementException>(
			() => _sut.AcknowledgeAsync(received[0], CancellationToken.None));

		acknowledged.ShouldBe(
			["only"],
			"the delivery handle survived its own acknowledgement, so the packet was acknowledged twice");
	}

	/// <summary>
	/// SAFETY. A second connection attempt must not subscribe the receive handler again: one packet from
	/// the broker must produce exactly one delivery.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The client is deliberately reused across reconnects, so an unconditional <c>+=</c> ran again on every
	/// attempt — after a disconnect, or after a connect that threw. A .NET event is multicast, so one broker
	/// callback was then handled twice, and because each handling takes a fresh delivery id the two copies
	/// are not recognisable as the same packet by anything downstream: the consumer executes it twice, and
	/// the second acknowledgement finds the underlying packet already settled.
	/// </para>
	/// <para>
	/// <b>This is the arm the other four cannot be.</b> They invoke the receive path directly, once per
	/// delivery, so the number of handler invocations per packet is something the fixture supplies rather
	/// than something it measures — they bind the delivery-id keying and are blind to duplicate
	/// registration. This one goes through the connect path twice and lets the event decide how many times
	/// the handler runs, which is the property.
	/// </para>
	/// <para>
	/// <b>Scope, stated so this is not read as more than it is.</b> The fake reproduces multicast
	/// invocation, which is what this property needs. It is not evidence about MQTTnet's own
	/// <c>AsyncEvent</c> internals; the real-broker reconnect case belongs in the conformance suite.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Subscribe_the_receive_handler_once_across_repeated_connection_attempts()
	{
		var client = A.Fake<IMqttClient>();
		var connections = A.Fake<IMqttConnectionProvider>();
		_ = A.CallTo(() => connections.CreateClient()).Returns(client);
		_ = A.CallTo(() => connections.BuildClientOptions(A<string>._))
			.Returns(new MqttClientOptions { ClientId = "sub" });

		await using var sut = new MqttTransportReceiver(
			connections, _options, NullLogger<MqttTransportReceiver>.Instance);

		var connect = typeof(MqttTransportReceiver).GetMethod(
			"EnsureSubscribedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

		// TWO attempts. The fake reports IsConnected = false throughout, which is exactly the state after a
		// dropped connection or a failed connect — the case the defect needed.
		await (Task)connect.Invoke(sut, [CancellationToken.None])!;
		await (Task)connect.Invoke(sut, [CancellationToken.None])!;

		// The client is created once and reused, which is intended — it is the handler that must not repeat.
		A.CallTo(() => connections.CreateClient()).MustHaveHappenedOnceExactly();

		// ONE packet from the broker.
		var message = new MqttApplicationMessage
		{
			Topic = _options.Topic,
			UserProperties = [new MqttUserProperty(DeliveryTag, "only")],
		};
		client.ApplicationMessageReceivedAsync += Raise.FreeForm<Func<MqttApplicationMessageReceivedEventArgs, Task>>
			.With(new MqttApplicationMessageReceivedEventArgs(
				"client-1", message, new MqttPublishPacket(), static (_, _) => Task.CompletedTask));

		using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds a receive that blocks on an empty buffer; the message was already raised, so the bound is never reached
		var received = await sut.ReceiveAsync(maxMessages: 5, bounded.Token);

		received.Count.ShouldBe(1, "one packet produced more than one delivery, so the receive handler is "
			+ "subscribed more than once. The consumer executes the message twice and the second "
			+ "acknowledgement settles a packet that is already settled.");
	}

	/// <summary>
	/// SAFETY. Two deliveries carrying the same application message id and the same MQTT packet identifier
	/// are settled independently.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Both identifiers repeat in ordinary traffic: an application message id is whatever the producer put
	/// on the message and a producer that resends or fans out reuses it, while the MQTT packet identifier
	/// is a 16-bit field the protocol explicitly recycles once a packet is settled. Neither identifies a
	/// delivery.
	/// </para>
	/// <para>
	/// <b>The correlations here differ deliberately.</b> That is what keeps this arm from being a second
	/// copy of the correlation arms: with distinct correlations the shipped defect cannot make it fail, so
	/// what it binds is the wider contract the fix actually establishes — a delivery's identity is derived
	/// from <i>no</i> application metadata, not merely from something other than correlation. The next
	/// person to key the map off a convenient field fails here.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Settle_deliveries_sharing_an_application_message_id_independently()
	{
		var acknowledged = new List<string>();
		const string RepeatedMessageId = "app-message-1";

		await DeliverAsync("first", "corr-a", acknowledged, RepeatedMessageId, packetIdentifier: 7);
		await DeliverAsync("second", "corr-b", acknowledged, RepeatedMessageId, packetIdentifier: 7);

		using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds a receive whose two deliveries were already raised, so the bound is never reached
		var received = await _sut.ReceiveAsync(maxMessages: 2, bounded.Token);
		received.Count.ShouldBe(2, "both deliveries must be buffered before either is settled, or the "
			+ "collision this arm exists to catch cannot occur.");

		var first = Delivery(received, "first");
		var second = Delivery(received, "second");

		// The condition under test, asserted rather than assumed by the fixture.
		first.Properties[ApplicationMessageId].ShouldBe(RepeatedMessageId);
		second.Properties[ApplicationMessageId].ShouldBe(RepeatedMessageId);
		first.Id.ShouldNotBe(second.Id);

		await _sut.AcknowledgeAsync(first, CancellationToken.None);
		acknowledged.ShouldBe(["first"], "acknowledging the first delivery settled a different packet. "
			+ "Two messages sharing an application id are ordinary traffic, so the broker has PUBACK'd a "
			+ "message the consumer never processed.");

		// LIVENESS: the sibling's handle was not consumed by the first settle. Without this the arm is
		// satisfied by a receiver that drops every handle on first settle — safe, and useless.
		await _sut.AcknowledgeAsync(second, CancellationToken.None);
		acknowledged.ShouldBe(["first", "second"]);
	}

	/// <summary>
	/// SAFETY. The same independence holds through the CloudEvent-decoding wrapper that
	/// <c>AddMqttTransport</c> registers, which is the receiver a consumer actually resolves.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The registration never hands out the bare receiver — it returns
	/// <c>new MqttTransportReceiver(...).WithCloudEventDecoding(...)</c>. So every arm above binds a type no
	/// consumer holds, and a wrapper that substituted the message or swallowed the settle would leave them
	/// all green while the shipped path stayed broken. This arm resolves the real registration to prove the
	/// wrapper is there, then puts the settlement property through that same composition.
	/// </para>
	/// <para>
	/// <b>Preservation of the original message is what makes the settle work at all.</b> Acknowledging
	/// through the wrapper can only reach the inner receiver's pending handle if the wrapper passed the
	/// message through with the delivery identity the receiver assigned it; a substituted message would
	/// find no handle and settle nothing. The correlation and the delivery tag are asserted alongside it so
	/// that a wrapper which preserved the id but rewrote the body would also fail.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Settle_independently_through_the_registered_decoding_wrapper()
	{
		var services = new ServiceCollection().AddLogging();
		_ = services.AddMqttTransport("mqtt", mqtt =>
		{
			mqtt.Host = _options.Host;
			mqtt.ClientId = _options.ClientId;
			mqtt.Topic = _options.Topic;
			mqtt.RequireTls = false;
		});

		await using (var provider = services.BuildServiceProvider())
		{
			var resolved = provider.GetRequiredKeyedService<ITransportReceiver>("mqtt");

			resolved.ShouldBeAssignableTo<DelegatingTransportReceiver>(
				"AddMqttTransport must hand out the decoding wrapper. If it returns the bare receiver this "
				+ "arm silently stops testing the wrapper while still passing.");
			resolved.ShouldNotBeOfType<MqttTransportReceiver>();
		}

		// The same composition the registration performs, over a connection provider this arm controls.
		// Resolving from the container above would reach the real broker on the first receive.
		var acknowledged = new List<string>();
		const string SharedCorrelation = "order-13";

		var inner = new MqttTransportReceiver(
			_fakeConnectionProvider, _options, NullLogger<MqttTransportReceiver>.Instance);
		await using var wrapped = inner.WithCloudEventDecoding(CloudEventBinding.Mqtt);

		await DeliverAsync("first", SharedCorrelation, acknowledged, target: inner);
		await DeliverAsync("second", SharedCorrelation, acknowledged, target: inner);

		using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds a receive whose two deliveries were already raised, so the bound is never reached
		var received = await wrapped.ReceiveAsync(maxMessages: 2, bounded.Token);
		received.Count.ShouldBe(2, "the wrapper dropped a message; it must deliver every message the inner "
			+ "receiver produced, decodable or not.");

		var first = Delivery(received, "first");
		var second = Delivery(received, "second");

		// The wrapper preserved the original message rather than substituting one.
		first.CorrelationId.ShouldBe(SharedCorrelation);
		second.CorrelationId.ShouldBe(SharedCorrelation);
		first.Id.ShouldNotBe(second.Id);

		// SAFETY: settled through the WRAPPER, only the requested packet is acknowledged.
		await wrapped.AcknowledgeAsync(first, CancellationToken.None);
		acknowledged.ShouldBe(["first"], "settling through the registered wrapper acknowledged a different "
			+ "packet than the one asked for.");

		// LIVENESS: the wrapper delegates the second settle too, rather than swallowing it.
		await wrapped.AcknowledgeAsync(second, CancellationToken.None);
		acknowledged.ShouldBe(["first", "second"]);
	}

	/// <summary>
	/// Drives one delivery through the receiver's own receive path, tagged so the arms can tell the
	/// deliveries apart, and recording which packet the broker was actually told to acknowledge.
	/// </summary>
	/// <remarks>
	/// The acknowledge callback is the fourth constructor argument of the real MQTTnet event args and is
	/// per-delivery, so recording <paramref name="tag"/> from inside it reports which <b>packet</b>
	/// settled — not merely that some settle call was made. An arm that asserted only "one
	/// acknowledgement happened" passes against the defect, because exactly one always did.
	/// </remarks>
	/// <param name="tag">Distinguishes this delivery from its siblings within an arm.</param>
	/// <param name="correlationId">The producer's correlation id, or <see langword="null"/> for none.</param>
	/// <param name="acknowledged">Collects the tag of whichever packet the broker is told to settle.</param>
	/// <param name="applicationMessageId">
	/// An application-level message identifier, carried as an MQTT 5 user property. Two deliveries may
	/// legitimately share one, so it must not reach the delivery identity.
	/// </param>
	/// <param name="packetIdentifier">
	/// The MQTT packet identifier. It is a 16-bit field the protocol recycles once a packet is settled, so
	/// it repeats in ordinary traffic and must not reach the delivery identity either.
	/// </param>
	/// <param name="target">The receiver to deliver into; the arm's own receiver when not supplied.</param>
	private async Task DeliverAsync(
		string tag,
		string? correlationId,
		List<string> acknowledged,
		string? applicationMessageId = null,
		ushort packetIdentifier = 0,
		MqttTransportReceiver? target = null)
	{
		List<MqttUserProperty> userProperties = [new MqttUserProperty(DeliveryTag, tag)];
		if (applicationMessageId is not null)
		{
			userProperties.Add(new MqttUserProperty(ApplicationMessageId, applicationMessageId));
		}

		var message = new MqttApplicationMessage
		{
			Topic = _options.Topic,
			CorrelationData = correlationId is null ? null : System.Text.Encoding.UTF8.GetBytes(correlationId),
			UserProperties = userProperties,
		};

		var args = new MqttApplicationMessageReceivedEventArgs(
			"client-1",
			message,
			new MqttPublishPacket { PacketIdentifier = packetIdentifier },
			(_, _) =>
			{
				acknowledged.Add(tag);
				return Task.CompletedTask;
			});

		var handler = typeof(MqttTransportReceiver).GetMethod(
			"OnMessageReceivedAsync",
			BindingFlags.NonPublic | BindingFlags.Instance)!;

		await (Task)handler.Invoke(target ?? _sut, [args])!;
	}

	private const string DeliveryTag = "x-delivery-tag";

	private const string ApplicationMessageId = "x-message-id";

	private static TransportReceivedMessage Delivery(
		IReadOnlyList<TransportReceivedMessage> received, string tag) =>
		received.Single(m => m.Properties.TryGetValue(DeliveryTag, out var value) && (string)value == tag);
}
