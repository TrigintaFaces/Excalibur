// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Proves over a real broker that rejecting a message means what the transport says it means: the message
/// comes back when the session resumes, and exactly once.
/// </summary>
/// <remarks>
/// <para>
/// This is the arm the sibling conformance suite explicitly declined to make. Rejection withholds the
/// acknowledgement and relies on the broker still holding the message, which is true only if the session
/// outlives the disconnect. The shipped configuration left the client library's defaults in place — a clean
/// start and a zero session-expiry interval — so under MQTT 5 the broker discarded the session the instant the
/// connection closed. There was nothing to resume and the rejected message was gone, with no error on any side.
/// </para>
/// <para>
/// A mocked client cannot fault this. The session is the BROKER's state, created by two flags on the CONNECT
/// packet and enforced by the broker's own rules, so a stub would only replay whatever it was told.
/// </para>
/// <para>
/// The two connections deliberately share one client id. A session is resumed by client id, so this is the
/// property under test rather than an incidental detail — a per-process random id cannot resume anything.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Transport")]
[Trait("Transport", "Mqtt")]
public sealed class MqttSessionRedeliveryTests : IClassFixture<MosquittoContainerFixture>
{
	private static readonly TimeSpan ReceiveBudget = TimeSpan.FromSeconds(20);

	private readonly MosquittoContainerFixture _fixture;

	public MqttSessionRedeliveryTests(MosquittoContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY. A rejected message is redelivered after the connection is lost and resumed — exactly once.
	/// </summary>
	/// <remarks>
	/// RED against the shipped defaults for the strongest possible reason: with a clean start there is no
	/// session to resume, so the redelivery count is zero rather than merely wrong.
	/// </remarks>
	[Fact]
	public async Task Redeliver_a_rejected_message_exactly_once_when_the_session_resumes()
	{
		Assert.SkipUnless(_fixture.Available, "Mosquitto container unavailable.");

		var identity = $"resume-{Guid.NewGuid():N}";
		var topic = $"redelivery/{Guid.NewGuid():N}";
		var payload = $"payload-{Guid.NewGuid():N}";

		// FIRST SESSION: subscribe, receive, and REJECT — which withholds the acknowledgement.
		await using (var first = Host(identity, topic))
		{
			var receiver = Receiver(first);
			await WarmAsync(receiver).ConfigureAwait(false);

			_ = await Sender(first).SendAsync(TransportMessage.FromString(payload), CancellationToken.None)
				.ConfigureAwait(false);

			var delivered = await ReceiveOneAsync(receiver).ConfigureAwait(false);
			_ = delivered.ShouldNotBeNull("the broker never delivered the message, so the rejection path was "
				+ "never reached and this arm would pass without testing anything.");

			await receiver.RejectAsync(delivered, "under test", requeue: true, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// The connection is now gone. Under the defect the broker dropped the session with it.

		// SECOND SESSION: same client id, so the broker resumes rather than starting fresh.
		await using var second = Host(identity, topic);
		var resumed = Receiver(second);

		var redelivered = await ReceiveOneAsync(resumed).ConfigureAwait(false);

		_ = redelivered.ShouldNotBeNull("the rejected message was not redelivered when the session resumed, "
			+ "so withholding the acknowledgement lost it.");
		redelivered.Body.ToArray().ShouldBe(Encoding.UTF8.GetBytes(payload));

		// EXACTLY once, not merely at-least-once: a resumed session that replayed its whole queue, or a
		// duplicated receive handler, would deliver it again here.
		await resumed.AcknowledgeAsync(redelivered, CancellationToken.None).ConfigureAwait(false);
		var extra = await ReceiveOneAsync(resumed).ConfigureAwait(false);
		extra.ShouldBeNull("the message was redelivered more than once.");
	}

	/// <summary>
	/// LIVENESS CONTROL. An ACKNOWLEDGED message is not redelivered when the session resumes.
	/// </summary>
	/// <remarks>
	/// Without this arm the one above is satisfied by a broker, or a configuration, that redelivers everything
	/// on every reconnect — which would make acknowledgement meaningless and would look identical from the arm
	/// above. This is the half that distinguishes "redelivery works" from "nothing is ever settled".
	/// </remarks>
	[Fact]
	public async Task Not_redeliver_a_message_that_was_acknowledged_before_the_disconnect()
	{
		Assert.SkipUnless(_fixture.Available, "Mosquitto container unavailable.");

		var identity = $"settled-{Guid.NewGuid():N}";
		var topic = $"redelivery/{Guid.NewGuid():N}";

		await using (var first = Host(identity, topic))
		{
			var receiver = Receiver(first);
			await WarmAsync(receiver).ConfigureAwait(false);

			_ = await Sender(first).SendAsync(TransportMessage.FromString("settled"), CancellationToken.None)
				.ConfigureAwait(false);

			var delivered = await ReceiveOneAsync(receiver).ConfigureAwait(false);
			_ = delivered.ShouldNotBeNull("nothing arrived, so there was nothing to acknowledge and this "
				+ "control proves nothing.");

			await receiver.AcknowledgeAsync(delivered, CancellationToken.None).ConfigureAwait(false);
		}

		await using var second = Host(identity, topic);

		var again = await ReceiveOneAsync(Receiver(second)).ConfigureAwait(false);

		again.ShouldBeNull("an acknowledged message came back on session resume, so acknowledgement is not "
			+ "settling anything and the redelivery arm above is vacuous.");
	}

	/// <summary>
	/// SAFETY. A message rejected with <c>requeue: false</c> is NOT redelivered when the session resumes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that detects the defect the whole settlement contract exists to forbid: the receiver
	/// ignoring <c>requeue</c> and silently substituting the opposite outcome while reporting success. It is
	/// RED by construction against the previous implementation, which withheld the acknowledgement for
	/// <b>both</b> values of the flag — so the message came back, this assertion saw it, and the failure
	/// message below is exactly what a consumer's poison-message arm experiences as an unbounded redelivery
	/// loop.
	/// </para>
	/// <para>
	/// <b>It cannot be satisfied by a test double.</b> The assertion is about what the BROKER does after the
	/// connection is dropped and resumed; an in-memory receiver cannot exhibit a redelivery at all, so it
	/// would pass this arm without exercising anything. That is why it lives here against a real Mosquitto
	/// rather than in the conformance kit.
	/// </para>
	/// <para>
	/// It is paired with <see cref="Redeliver_a_rejected_message_exactly_once_when_the_session_resumes"/>,
	/// which is its liveness half: without that arm, a broker that redelivered nothing at all — or a
	/// receiver that discarded every message — would satisfy this one trivially.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_redeliver_a_message_rejected_without_requeue()
	{
		Assert.SkipUnless(_fixture.Available, "Mosquitto container unavailable.");

		var identity = $"dropped-{Guid.NewGuid():N}";
		var topic = $"redelivery/{Guid.NewGuid():N}";
		var payload = $"payload-{Guid.NewGuid():N}";

		await using (var first = Host(identity, topic))
		{
			var receiver = Receiver(first);
			await WarmAsync(receiver).ConfigureAwait(false);

			_ = await Sender(first).SendAsync(TransportMessage.FromString(payload), CancellationToken.None)
				.ConfigureAwait(false);

			var delivered = await ReceiveOneAsync(receiver).ConfigureAwait(false);
			_ = delivered.ShouldNotBeNull("the broker never delivered the message, so the rejection path was "
				+ "never reached and this arm would pass without testing anything.");

			// The caller states the outcome it requires: do NOT deliver this again.
			await receiver.RejectAsync(delivered, "poison", requeue: false, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Same client id, so the broker RESUMES the session rather than starting a fresh one. Anything the
		// first session left unacknowledged is redelivered here — which is precisely what must not happen.
		await using var second = Host(identity, topic);

		var returned = await ReceiveOneAsync(Receiver(second)).ConfigureAwait(false);

		returned.ShouldBeNull("a message rejected with requeue:false came back on session resume, so the "
			+ "receiver substituted requeue:true and reported success for an outcome it did not deliver. A "
			+ "poison-message arm rejecting this message would loop on it, and a dead-letter decorator would "
			+ "write a fresh copy on every pass.");
	}

	private ServiceProvider Host(string clientId, string topic) =>
		new ServiceCollection()
			.AddLogging()
			.AddMqttTransport("mqtt-redelivery", mqtt =>
			{
				mqtt.Host = _fixture.Host;
				mqtt.Port = _fixture.Port;

				// Stable across both connections: this IS the property under test.
				mqtt.ClientId = clientId;
				mqtt.Topic = topic;
				mqtt.QualityOfService = MqttQualityOfService.AtLeastOnce;

				// The container listens in the clear, so the secure-by-default posture is opted out here
				// deliberately; the posture itself has its own lock.
				mqtt.RequireTls = false;
			})
			.BuildServiceProvider();

	private static ITransportReceiver Receiver(ServiceProvider host) =>
		host.GetRequiredKeyedService<ITransportReceiver>("mqtt-redelivery");

	private static ITransportSender Sender(ServiceProvider host) =>
		host.GetRequiredKeyedService<ITransportSender>("mqtt-redelivery");

	/// <summary>
	/// Triggers the lazy connect and subscribe so the broker has a session and a subscription before anything
	/// is published. The call is EXPECTED to come back empty.
	/// </summary>
	private static async Task WarmAsync(ITransportReceiver receiver)
	{
		using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try
		{
			_ = await receiver.ReceiveAsync(1, budget.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected: nothing has been published yet. The connect and subscribe are the point.
		}
	}

	/// <summary>
	/// Receives one message within a bounded budget, returning <see langword="null"/> when none arrives.
	/// </summary>
	/// <remarks>
	/// EVERY receive is bounded. The receiver blocks until a message arrives or the token trips, so an
	/// unbounded token here does not fail the test — it hangs the run, which costs far more than a red.
	/// </remarks>
	private static async Task<TransportReceivedMessage?> ReceiveOneAsync(ITransportReceiver receiver)
	{
		using var budget = new CancellationTokenSource(ReceiveBudget);
		try
		{
			var batch = await receiver.ReceiveAsync(1, budget.Token).ConfigureAwait(false);
			return batch.Count > 0 ? batch[0] : null;
		}
		catch (OperationCanceledException)
		{
			return null;
		}
	}
}
