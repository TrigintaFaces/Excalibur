// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;
using System.Text;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Testing.Transport;

/// <summary>
/// Behavioural conformance tests for a transport's CloudEvents round trip: what the transport's own
/// encoder writes, the receiver a consumer resolves reads back.
/// </summary>
/// <remarks>
/// <para>
/// To use, inherit and supply three things — the receiver a consumer of your transport actually resolves,
/// a way to put a message in front of it, and your transport's own encoding. Every assertion lives here,
/// so no transport can drift into a weaker version of the contract.
/// </para>
/// <para>
/// <b>Why this is separate from the core transport kit.</b> That kit is keyed on
/// <c>IChannelReceiver</c> and its own remarks state that CloudEvents protocol binding is out of scope
/// because it "requires a richer receive context than IChannelReceiver exposes". That is exactly right,
/// and it is the reason the conformance gate that came before this file reflected over TYPE NAMES
/// instead: the abstraction it had could not express the property, so it asserted the only thing it
/// could see. This kit is keyed on <see cref="ITransportReceiver"/>, whose
/// <see cref="TransportReceivedMessage.ProviderData"/> carries the decoded event — production surface,
/// not a test-only seam.
/// </para>
/// <para>
/// <b>Presence is not consultation.</b> Nothing here asserts that a type exists, is registered, or is
/// resolvable. A transport that ships a fully-formed encoder, registers a decoder, and never routes
/// receive through it passes every presence check ever written and fails these arms. That distinction is
/// the whole purpose of the kit, so a derived class that cannot satisfy it must not be given a weaker
/// override to satisfy instead.
/// </para>
/// <para>
/// <b>Non-vacuity is proved by mutation, per transport, at any HEAD.</b> Severing the decode decoration
/// on exactly one transport must turn that transport's derived class RED and leave every other
/// transport's GREEN. That proof does not depend on the tree being in any particular state, which is the
/// property a criterion phrased as "fails on today's HEAD" lacks the moment today's HEAD changes.
/// </para>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes; add the
/// attributes your framework requires on thin overrides in the derived class, as the sibling kits do.
/// </para>
/// </remarks>
public abstract class CloudEventTransportConformanceTests
{
	/// <summary>
	/// Creates the receiver a CONSUMER of this transport resolves — the registered one, with whatever
	/// decoration the transport's own registration applied.
	/// </summary>
	/// <returns>The receiver under test.</returns>
	/// <remarks>
	/// Returning a hand-constructed receiver rather than the registered one would test a receiver nobody
	/// runs. The registration is the subject: it is where a decode decoration is applied or forgotten.
	/// </remarks>
	protected abstract Task<ITransportReceiver> CreateReceiverAsync();

	/// <summary>
	/// Puts messages in front of the receiver, by whatever means this transport allows.
	/// </summary>
	/// <param name="receiver">The receiver returned by <see cref="CreateReceiverAsync"/>.</param>
	/// <param name="messages">The messages the receiver must subsequently yield.</param>
	protected abstract Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages);

	/// <summary>
	/// Encodes a CloudEvent the way THIS transport encodes it, returning the transport-native properties
	/// it would put on the wire.
	/// </summary>
	/// <param name="cloudEvent">The event to encode.</param>
	/// <param name="mode">Binary or structured.</param>
	/// <returns>The inbound message this transport's own encoding would produce.</returns>
	/// <remarks>
	/// <b>Honour the mode, or throw <see cref="NotSupportedException"/>.</b> Returning an encoding in a
	/// DIFFERENT mode than the one asked for makes every mode-sensitive arm meaningless, because the arm
	/// believes it is holding one wire shape and is holding another. A transport with no encoder for a
	/// mode should refuse: that refusal is itself the answer the declined-mode arm needs.
	/// <b>Return everything the transport puts on the wire, not just the properties.</b> Structured mode is
	/// identified by its MEDIA TYPE, and transports differ in where they carry it: some place it among the
	/// properties, others on the message envelope. An implementation that copies only properties and body
	/// loses it for the second kind and produces a RED that is indistinguishable from a genuine decode
	/// failure - measured, on the first transport of that kind to be enrolled.
	/// The kit never names a spelling. Whatever this returns is what the arms feed back, so an encoder
	/// that changes what it writes cannot break the arms by disagreeing with a literal in this file — it
	/// can only break them by writing something its own receiver cannot read, which is the defect.
	/// <para>
	/// This returns a whole message rather than a property bag because the two modes do not put the
	/// event in the same place: binary spreads the attributes across transport-native properties, while
	/// structured carries the entire event in the BODY under a content type. A seam that returned only
	/// properties would be empty for every structured encoding, and an arm fed an empty encoding passes
	/// for a receiver that decodes nothing.
	/// </para>
	/// </remarks>
	protected abstract Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode);

	/// <summary>
	/// Gets the modes this transport supports, defaulting to both.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Narrowing this costs an assertion rather than buying a free pass.</b> A transport that declares
	/// one mode must FAIL to decode the other - the arm below requires it - so a declaration narrowed to
	/// dodge a failing round trip turns that dodge into a different failure. A transport really is
	/// entitled to support one mode: a registration that binds structured-only decoding cannot read a
	/// binary encoding, and calling that a defect would be the conformance suite misreading a deliberate
	/// contract.
	/// </para>
	/// <para>
	/// <b>This is the one thing the kit takes on trust, and it is stated rather than hidden.</b> The
	/// binding a registration chose is not observable from outside the transport package - the decoding
	/// receiver holds its decoder privately - so the kit cannot read the supported set from the wiring and
	/// must be told. The paired arm is what keeps the telling honest.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyCollection<CloudEventMode> SupportedModes =>
		[CloudEventMode.Binary, CloudEventMode.Structured];

	/// <summary>
	/// Gets the body the arms send when the encoding does not supply one.
	/// </summary>
	protected virtual byte[] Payload => Encoding.UTF8.GetBytes("conformance payload");

	/// <summary>
	/// Builds the event the round-trip arms send. Override only if this transport cannot carry one of
	/// these attributes, and say why in the override.
	/// </summary>
	/// <returns>The event under test.</returns>
	protected virtual CloudEvent CreateCloudEvent() =>
		new(CloudEventsSpecVersion.V1_0)
		{
			Id = "conformance-1",
			Type = "conformance.roundtrip",
			Source = new Uri("https://conformance.excalibur.test/source"),
			Data = "conformance payload",
			DataContentType = "text/plain",
		};

	/// <summary>
	/// LIVENESS. What this transport's encoder writes in binary mode, its own receiver reads back.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual Task VerifyBinaryModeRoundTripsThroughItsOwnReceiver() =>
		RoundTripAsync(CloudEventMode.Binary);

	/// <summary>
	/// SAFETY. A mode this transport declares it does not support must not decode.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The pair to the declaration itself. Without this, narrowing <see cref="SupportedModes"/> is a way
	/// to delete a failing arm, and the cheapest response to a red round trip becomes editing the
	/// declaration rather than fixing the transport.
	/// <para>
	/// <b>What it is really hunting is emit-without-decode.</b> A transport that can PRODUCE a mode it
	/// cannot READ sends a CloudEvent its own receiver will hand to a consumer as ordinary traffic - the
	/// defect that prompted this whole area. A transport that cannot produce the mode at all is safe, and
	/// says so by refusing to encode it.
	/// </para>
	/// </remarks>
	public virtual async Task VerifyDeclinedModesDoNotDecode()
	{
		foreach (var mode in new[] { CloudEventMode.Binary, CloudEventMode.Structured })
		{
			if (SupportedModes.Contains(mode))
			{
				continue;
			}

			TransportReceivedMessage wire;

			try
			{
				wire = await EncodeAsync(CreateCloudEvent(), mode).ConfigureAwait(false);
			}
			catch (NotSupportedException)
			{
				// The transport cannot PRODUCE this mode, so this framework never emits one on it and
				// there is nothing for its receiver to mis-claim. That is the safe shape, and it is the
				// reason the arm asks the encoder first rather than assuming a wire format.
				continue;
			}

			var receiver = await CreateReceiverAsync().ConfigureAwait(false);
			await SeedMessagesAsync(receiver, [wire]).ConfigureAwait(false);

			var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

			// ANY message carrying a decoded event fails this, not just the first: a declined mode that
			// decodes is the defect whether it lands on one message or several, and keying on index 0
			// would let a second message carry it past the arm.
			if (received.Any(static m => m.ProviderData.ContainsKey(
					CloudEventDecodingTransportReceiver.CloudEventProviderDataKey)))
			{
				throw new InvalidOperationException(
					$"this transport declares it does not support {mode} mode, and it decoded one anyway. "
					+ "Either the declaration is wrong - in which case the round-trip arm for that mode "
					+ "should be running and is being skipped - or the transport decodes a mode its own "
					+ "registration does not claim. Both are worth knowing; neither is a passing state.");
			}
		}
	}

	/// <summary>
	/// LIVENESS. The same property in structured mode, which is a different wire shape and a different
	/// decode path, so passing one says nothing about the other.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual Task VerifyStructuredModeRoundTripsThroughItsOwnReceiver() =>
		RoundTripAsync(CloudEventMode.Structured);

	/// <summary>
	/// SAFETY. Ordinary traffic is not reported as a CloudEvent.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The pair to both arms above, and the one that fails for the cheapest wrong fix: a receiver that
	/// attached an event to every inbound message would satisfy both round trips completely while telling
	/// a consumer nothing about any of them.
	/// </remarks>
	public virtual async Task VerifyOrdinaryTrafficIsNotReportedAsCloudEvent()
	{
		var receiver = await CreateReceiverAsync().ConfigureAwait(false);

		var plain = new TransportReceivedMessage
		{
			Id = "plain-1",
			Body = Payload,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = new Dictionary<string, object>(StringComparer.Ordinal),
		};

		await SeedMessagesAsync(receiver, [plain]).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

		if (received.Count != 1)
		{
			throw new InvalidOperationException(
				$"one message was seeded and {received.Count} came back. Zero means the seeding never "
				+ "reached the receiver and the arm proves nothing; more than one means the arm would be "
				+ "reading a message it did not seed, and for a SAFETY arm that reads as a pass while the "
				+ "decoded event sits on a message it never inspected.");
		}

		if (received[0].ProviderData.ContainsKey(
			CloudEventDecodingTransportReceiver.CloudEventProviderDataKey))
		{
			throw new InvalidOperationException(
				"a message carrying none of this transport's CloudEvents properties was reported as a "
				+ "CloudEvent. A consumer branching on that flag would treat ordinary traffic as an event, "
				+ "and the round-trip arms cannot detect this because they are satisfied by a receiver "
				+ "that reports an event unconditionally.");
		}
	}

	private async Task RoundTripAsync(CloudEventMode mode)
	{
		if (!SupportedModes.Contains(mode))
		{
			// Declining is not free: VerifyDeclinedModesDoNotDecode asserts this mode really cannot be
			// decoded, so a declaration made to dodge this arm fails that one instead.
			return;
		}

		var sent = CreateCloudEvent();
		var wire = await EncodeAsync(sent, mode).ConfigureAwait(false);

		if (wire.Properties.Count == 0 && wire.Body.Length == 0)
		{
			throw new InvalidOperationException(
				$"the encoder produced neither properties nor a body in {mode} mode, so nothing was put on "
				+ "the wire for the receiver to read. An arm fed an empty encoding passes for a receiver "
				+ "that decodes nothing, which is the defect this kit exists to catch.");
		}

		var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		await SeedMessagesAsync(receiver, [wire]).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

		if (received.Count != 1)
		{
			throw new InvalidOperationException(
				$"one message was seeded in {mode} mode and {received.Count} came back. Zero means the "
				+ "seeding never reached the receiver a consumer resolves; more than one means index 0 is "
				+ "not necessarily the message under test, so the verdict below would be about traffic "
				+ "this arm did not put there.");
		}

		if (!received[0].ProviderData.TryGetValue(
				CloudEventDecodingTransportReceiver.CloudEventProviderDataKey, out var decodedValue)
			|| decodedValue is not CloudEvent decoded)
		{
			throw new InvalidOperationException(
				$"this transport encoded the event in {mode} mode and the receiver its own registration "
				+ "builds did not read it back, so a CloudEvent this framework emitted arrives at a "
				+ "consumer of this framework as ordinary traffic - indistinguishable from a message that "
				+ "never carried CloudEvents markers. Properties written: "
				+ (wire.Properties.Count == 0 ? "(none)" : string.Join(", ", wire.Properties.Keys))
				+ $"; body bytes: {wire.Body.Length}.");
		}

		if (!string.Equals(decoded.Id, sent.Id, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"the event decoded in {mode} mode but its id is '{decoded.Id}' rather than '{sent.Id}'. "
				+ "A round trip that returns a DIFFERENT event is not a round trip, and a consumer "
				+ "correlating on id would follow the wrong one.");
		}

		if (!string.Equals(decoded.Type, sent.Type, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"the event decoded in {mode} mode but its type is '{decoded.Type}' rather than "
				+ $"'{sent.Type}'. Type is what a consumer routes on, so a lost or altered type sends the "
				+ "event to the wrong handler or to none.");
		}
	}
}
