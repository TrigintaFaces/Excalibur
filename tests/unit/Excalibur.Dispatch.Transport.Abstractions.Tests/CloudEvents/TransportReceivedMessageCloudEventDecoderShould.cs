// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.CloudEvents;

/// <summary>
/// Binds the decoder's contract as a total primitive over one message: null for a message that is not a
/// CloudEvent, a value for one that is, and a refusal for one that claims to be and is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The prefix arms are the load-bearing ones.</b> Transports do not agree on how a binary-mode
/// attribute is spelled on the wire — a broker whose header keys cannot carry a hyphen writes one form,
/// an AMQP application-property another, and a topic with no header concept writes the bare name. A
/// decoder that honoured only one spelling would decode some transports and pass the rest through
/// silently, per transport, looking exactly like "this message is not a CloudEvent". That failure is
/// invisible at the call site and is the reason a single shared decoder exists at all.
/// </para>
/// <para>
/// <b>Pass-through and refusal are different outcomes and the distinction is the point.</b> A message
/// without <c>specversion</c> is ordinary traffic on a shared transport, whatever else its properties
/// happen to be named. A message that declares <c>specversion</c> and then omits a required attribute is
/// a broken CloudEvent, and collapsing the two would let a corrupt event arrive looking like a plain
/// payload.
/// </para>
/// <para>
/// <b>The claim is <c>specversion</c> and nothing else</b>, because the bare spelling is a real binding:
/// with unprefixed names probed, detecting on id/type/source made two of the commonest property names in
/// messaging into CloudEvents markers on every transport. That is what the ordinary-traffic arm below
/// pins.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportReceivedMessageCloudEventDecoderShould
{
    private const string StructuredContentType = "application/cloudevents+json";

    // AMQP 1.0 carries the widest prefix set, so it serves every arm whose subject is not the prefix
    // itself. Arms that ARE about a prefix construct the binding that actually assigns it -- a decoder
    // no longer honours every binding's spelling, which is the property under test.
    private static readonly TransportReceivedMessageCloudEventDecoder Decoder =
        new(CloudEventBinding.Amqp10);

    private static TransportReceivedMessageCloudEventDecoder For(CloudEventBinding binding) => new(binding);

    /// <summary>
    /// LIVENESS. The hyphenated spelling decodes.
    /// </summary>
    /// <remarks>
    /// <b>Do not delete this arm when the write side stops emitting this spelling.</b> The read side
    /// accepts it for a reason the write side does not share: messages carrying it may already be sitting
    /// on a consumer's queue, written by a version we shipped. Dropping the prefix from the decoder would
    /// leave those messages arriving as ordinary traffic - undecoded, unreported, and indistinguishable
    /// from a message that never carried an event.
    /// <para>
    /// So this arm outlives the spelling's use on the write path, and it may only be removed once no
    /// supported upgrade path can still deliver a message written by a version that emitted it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Decode_a_binary_mode_event_spelled_with_the_hyphen_prefix()
    {
        var message = BinaryMessage("ce-");

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe("evt-1");
        decoded.Type.ShouldBe("order.placed");
    }

    /// <summary>
    /// LIVENESS. The short-underscore spelling decodes. The CloudEvents Kafka binding assigns "ce_"
    /// because a Kafka header key cannot carry a hyphen, and KafkaCloudEventAdapter writes exactly that.
    /// The decoder did not probe it, so a CloudEvent this framework emitted was passed through
    /// undecoded -- indistinguishable from a message that was never a CloudEvent.
    /// </summary>
    [Fact]
    public async Task Decode_a_binary_mode_event_spelled_with_the_kafka_short_underscore_prefix()
    {
        var message = BinaryMessage("ce_");

        var decoded = await For(CloudEventBinding.Kafka).TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe("evt-1");
        decoded.Type.ShouldBe("order.placed");
    }

    /// <summary>
    /// LIVENESS. The underscore spelling decodes — a broker whose header keys cannot carry a hyphen.
    /// </summary>
    [Fact]
    public async Task Decode_a_binary_mode_event_spelled_with_the_underscore_prefix()
    {
        var message = BinaryMessage("cloudEvents_");

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe("evt-1");
        decoded.Type.ShouldBe("order.placed");
    }

    /// <summary>
    /// LIVENESS. The colon spelling decodes. The AMQP binding permits BOTH separators and an earlier
    /// revision of it permitted only this one, so a conformant AMQP producer emits it and it is not a
    /// legacy spelling being tolerated. Probing only the underscore read that producer's events as
    /// ordinary messages -- the silent per-transport non-decode this decoder exists to prevent, which
    /// looks identical to a message that simply carried no CloudEvent.
    /// </summary>
    [Fact]
    public async Task Decode_a_binary_mode_event_spelled_with_the_colon_prefix()
    {
        var message = BinaryMessage("cloudEvents:");

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe("evt-1");
        decoded.Type.ShouldBe("order.placed");
    }

    /// <summary>
    /// LIVENESS. The bare spelling decodes — a transport with no header-prefix convention at all.
    /// </summary>
    [Fact]
    public async Task Decode_a_binary_mode_event_spelled_with_no_prefix()
    {
        var message = BinaryMessage(string.Empty);

        var decoded = await For(CloudEventBinding.Mqtt).TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe("evt-1");
        decoded.Type.ShouldBe("order.placed");
    }

    /// <summary>
    /// LIVENESS for the round trip on transports the specification assigns no binding: what our own
    /// adapters EMIT there must be readable by the binding those transports declare.
    /// </summary>
    /// <remarks>
    /// This arm exists because the opposite shipped: <b>two</b> transports whose adapters write
    /// <c>ce-</c> names — SQS and Pub/Sub — were given a binding that read nothing, so the framework
    /// published binary CloudEvents it could not itself consume. Refusing to read a format we emit buys
    /// no conformance, because the bytes are on the wire either way, and costs the round trip.
    /// <para>
    /// <b>Four transports declare this binding and only three of them emit.</b> IBM MQ ships no encoder
    /// at all; it declares the binding so that its receiver can read the attributes a third-party
    /// publisher sends, which is a claim about reading and must not be restated as one about writing.
    /// </para>
    /// <para>
    /// <b>SCOPE — this arm does NOT lock the fix that shipped, and it must not be cited as though it
    /// did.</b> It binds the binding's CONTENTS: change <c>ce-</c> here, or make this binding recognise
    /// nothing, and it reddens. It says nothing about WHICH transports select this binding, because that
    /// choice is made in nine registration files in other assemblies and the decoder holds it privately.
    /// <b>Measured: reverting a transport's registration to the structured-only binding — which is
    /// exactly the defect that shipped — leaves every arm in this file green.</b> The lock for that axis
    /// has to resolve a real registration and observe what the receiver decodes; it cannot live here.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Read_the_house_spelling_its_own_adapters_emit()
    {
        var message = BinaryMessage("ce-");

        var decoded = await For(CloudEventBinding.HouseConvention)
            .TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull(
            "SQS, Pub/Sub and RabbitMQ emit these names from their own adapters, so refusing to read them "
            + "would publish events this framework cannot itself consume");
        decoded.Id.ShouldBe("evt-1");
    }

    /// <summary>
    /// SAFETY, and the arm that makes the per-binding change mean anything. A message spelled for one
    /// binding must NOT be decoded by a transport whose binding does not assign that spelling.
    /// </summary>
    /// <remarks>
    /// Before the binding became a parameter every transport probed every spelling, so this message
    /// decoded everywhere. Without this arm the change is indistinguishable from the old behaviour on
    /// the transports it was written to protect, because every other arm here only checks that a
    /// spelling IS honoured somewhere.
    /// </remarks>
    [Fact]
    public async Task Refuse_a_spelling_its_own_binding_does_not_assign()
    {
        var amqpMessage = BinaryMessage("cloudEvents:");

        var onStructuredOnly = await For(CloudEventBinding.StructuredOnly)
            .TryDecodeAsync(amqpMessage, CancellationToken.None);

        onStructuredOnly.ShouldBeNull(
            "a transport the specification assigns no binding has no binary mode, so an AMQP-spelled "
            + "attribute is ordinary metadata to it");

        // CONTROL, same message: the binding that DOES assign this spelling still reads it, so the null
        // above is the binding discriminating rather than the decoder having stopped working.
        var onAmqp = await For(CloudEventBinding.Amqp10).TryDecodeAsync(amqpMessage, CancellationToken.None);
        _ = onAmqp.ShouldNotBeNull();
    }

    /// <summary>
    /// SAFETY, and this is the exact residual the per-binding change exists to close: ordinary traffic
    /// carrying a property literally called <c>specversion</c> must not be REFUSED on a transport whose
    /// binding does not assign bare attribute names.
    /// </summary>
    /// <remarks>
    /// The nastier half of the bare-name problem. A message with a bare <c>specversion</c> and none of
    /// the other required attributes was detected as a CloudEvent and then <b>thrown on</b> as malformed
    /// — so an ordinary send failed outright rather than merely being mislabelled. The arm below it
    /// passes a COMPLETE bare attribute set and so only proves the message is not decoded; this one
    /// proves it is not rejected, which is the behaviour a consumer actually noticed.
    /// </remarks>
    [Fact]
    public async Task Pass_through_ordinary_traffic_named_specversion_off_the_binding_that_assigns_bare()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            Body = Encoding.UTF8.GetBytes("an ordinary payload"),
            Properties = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                // No id, type or source: pre-change this was detected on the bare name and then refused.
                ["specversion"] = "not-a-cloudevent",
            },
        };

        var decoded = await For(CloudEventBinding.Kafka).TryDecodeAsync(message, CancellationToken.None);

        decoded.ShouldBeNull(
            "the Kafka binding assigns ce_, so a bare property name is ordinary metadata and the message "
            + "must pass through rather than be refused as a malformed CloudEvent");
    }

    /// <summary>
    /// SAFETY for the bare spelling, which is the one that turns ordinary traffic into CloudEvents.
    /// </summary>
    /// <remarks>
    /// Bare attribute names are assigned by one binding only. Honoured anywhere else, an application
    /// property innocently called <c>specversion</c> promotes a plain message to a malformed CloudEvent
    /// and the receive path starts refusing it.
    /// </remarks>
    [Fact]
    public async Task Refuse_bare_attribute_names_on_a_binding_that_does_not_assign_them()
    {
        var bare = BinaryMessage(string.Empty);

        var onAmqp = await For(CloudEventBinding.Amqp10).TryDecodeAsync(bare, CancellationToken.None);

        onAmqp.ShouldBeNull("only the MQTT binding assigns bare attribute names");

        var onMqtt = await For(CloudEventBinding.Mqtt).TryDecodeAsync(bare, CancellationToken.None);
        _ = onMqtt.ShouldNotBeNull("control: the binding that assigns bare names still reads them");
    }

    /// <summary>
    /// LIVENESS for ordinary traffic: a message with no CloudEvents markers is not a CloudEvent, and that
    /// is a normal answer rather than a failure.
    /// </summary>
    [Fact]
    public async Task Pass_through_a_message_carrying_no_markers()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            Body = Encoding.UTF8.GetBytes("an ordinary payload"),
            Properties = new Dictionary<string, object> { ["content-encoding"] = "identity" },
        };

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        decoded.ShouldBeNull("a message with no markers is ordinary traffic, not a broken CloudEvent");
    }

    /// <summary>
    /// The boundary between pass-through and refusal: an OPTIONAL attribute alone is not a claim to be a
    /// CloudEvent, so it passes through rather than being refused.
    /// </summary>
    /// <remarks>
    /// This is the arm that stops the refusal below from being over-eager. Binary mode is identified by
    /// <c>specversion</c>; a message carrying only something like a subject has made no claim, and
    /// throwing on it would turn unrelated traffic on a shared transport into an error.
    /// </remarks>
    [Fact]
    public async Task Pass_through_a_message_carrying_only_a_non_required_attribute()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            Body = Encoding.UTF8.GetBytes("an ordinary payload"),
            Properties = new Dictionary<string, object> { ["ce-subject"] = "orders/17" },
        };

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        decoded.ShouldBeNull(
            "specversion is what constitutes a binary-mode claim; a lone optional attribute does not, "
            + "and refusing it would break unrelated traffic");
    }

    /// <summary>
    /// REGRESSION. Ordinary traffic whose properties happen to be named <c>id</c> and <c>type</c> must
    /// pass through, not be treated as a broken CloudEvent.
    /// </summary>
    /// <remarks>
    /// The bare spelling is a real binding — MQTT carries CloudEvents attributes unprefixed — so the
    /// decoder must probe unprefixed names. Detecting on id/type/source therefore turned two of the
    /// commonest property names in messaging into CloudEvents markers on every transport, and a message
    /// carrying them without a source was treated as a partially-attributed event.
    /// <para>
    /// This arm binds the REQUIREMENT (ordinary traffic is not treated as an event) rather than the
    /// remedy, so it stays meaningful if detection changes again.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Pass_through_ordinary_traffic_whose_properties_are_named_id_and_type()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            Body = Encoding.UTF8.GetBytes("an ordinary business message"),
            Properties = new Dictionary<string, object>
            {
                ["id"] = "order-4711",
                ["type"] = "application/vnd.orders.v2+json",
            },
        };

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        decoded.ShouldBeNull(
            "a message with properties named id and type is ordinary traffic on a shared transport; "
            + "treating it as an event marks a well-formed business message as a corrupt CloudEvent");
    }

    /// <summary>
    /// SAFETY. A partially-attributed message has claimed to be a CloudEvent and is broken, so it is
    /// refused rather than passed through as ordinary traffic.
    /// </summary>
    /// <remarks>
    /// Passing this through would be the silent downgrade: a corrupt event indistinguishable from a plain
    /// message. The decorator above turns this refusal into a per-message annotation; the decoder's job is
    /// to raise it.
    /// </remarks>
    [Fact]
    public async Task Refuse_a_binary_mode_message_missing_a_required_attribute()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            Body = Encoding.UTF8.GetBytes("payload"),
            Properties = new Dictionary<string, object>
            {
                // specversion is what marks this as a CloudEvent at all; without it the message is
                // ordinary traffic and passing through is correct. Its presence is what makes the
                // MISSING source a refusal rather than an absence.
                ["ce-specversion"] = "1.0",
                ["ce-id"] = "evt-1",
                ["ce-type"] = "order.placed",
                // source deliberately absent
            },
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => Decoder.TryDecodeAsync(message, CancellationToken.None));
    }

    /// <summary>
    /// LIVENESS. Structured mode is identified by the content type and decoded from the body.
    /// </summary>
    [Fact]
    public async Task Decode_a_structured_mode_event_from_the_body()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            ContentType = StructuredContentType,
            Body = Encoding.UTF8.GetBytes(
                """{"id":"evt-1","type":"order.placed","source":"https://example.test/orders","specversion":"1.0"}"""),
        };

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe("evt-1");
        decoded.Type.ShouldBe("order.placed");
    }

    /// <summary>
    /// LIVENESS. The content type is matched case-insensitively and tolerates parameters after it.
    /// </summary>
    /// <remarks>
    /// Transports and brokers normalise header casing differently and routinely append a charset, so a
    /// case-sensitive or exact-match test would fail to recognise structured mode on some of them — the
    /// same silent per-transport pass-through the prefix arms guard against.
    /// </remarks>
    [Fact]
    public async Task Recognise_the_structured_content_type_regardless_of_case_or_parameters()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            ContentType = "APPLICATION/CloudEvents+JSON; charset=utf-8",
            Body = Encoding.UTF8.GetBytes(
                """{"id":"evt-1","type":"order.placed","source":"https://example.test/orders","specversion":"1.0"}"""),
        };

        var decoded = await Decoder.TryDecodeAsync(message, CancellationToken.None);

        _ = decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe("evt-1");
    }

    /// <summary>
    /// SAFETY. A message declaring the structured content type with nothing to decode is refused.
    /// </summary>
    [Fact]
    public async Task Refuse_a_structured_mode_message_with_an_empty_body()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            ContentType = StructuredContentType,
            Body = ReadOnlyMemory<byte>.Empty,
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => Decoder.TryDecodeAsync(message, CancellationToken.None));
    }

    /// <summary>
    /// SAFETY. A structured body that is not a JSON object is refused rather than half-decoded.
    /// </summary>
    [Fact]
    public async Task Refuse_a_structured_mode_message_whose_body_is_not_a_json_object()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            ContentType = StructuredContentType,
            Body = Encoding.UTF8.GetBytes("""["not","an","object"]"""),
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => Decoder.TryDecodeAsync(message, CancellationToken.None));
    }

    /// <summary>
    /// SAFETY. A structured envelope missing a required attribute is refused.
    /// </summary>
    [Fact]
    public async Task Refuse_a_structured_mode_message_missing_a_required_attribute()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            ContentType = StructuredContentType,
            Body = Encoding.UTF8.GetBytes("""{"id":"evt-1","specversion":"1.0"}"""),
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => Decoder.TryDecodeAsync(message, CancellationToken.None));
    }

    /// <summary>
    /// SAFETY. Cancellation is honoured before any decoding work is attempted.
    /// </summary>
    [Fact]
    public async Task Honour_a_cancelled_token()
    {
        var message = BinaryMessage("ce-");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => Decoder.TryDecodeAsync(message, cts.Token));
    }

    /// <summary>
    /// A binary-mode CloudEvent as a conformant producer actually emits one.
    /// </summary>
    /// <remarks>
    /// <c>specversion</c> is required in every mode by the specification, so a real producer always writes
    /// it — and it is the attribute the decoder detects on. An earlier version of this fixture carried only
    /// id, type and source because those were the attributes the decoder happened to read; that made the
    /// fixture a mirror of the implementation rather than a sample of the wire, and it is why these arms
    /// could not see a detection change.
    /// </remarks>
    private static TransportReceivedMessage BinaryMessage(string prefix) =>
        new()
        {
            Id = "m-1",
            Body = Encoding.UTF8.GetBytes("the payload"),
            Properties = new Dictionary<string, object>
            {
                [prefix + "specversion"] = "1.0",
                [prefix + "id"] = "evt-1",
                [prefix + "type"] = "order.placed",
                [prefix + "source"] = "https://example.test/orders",
            },
        };
}
