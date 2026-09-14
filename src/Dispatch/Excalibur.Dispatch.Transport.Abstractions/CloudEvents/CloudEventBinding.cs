// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// The CloudEvents protocol binding a transport speaks, which decides how binary-mode attributes are
/// named on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>Binary mode is exactly the thing a protocol binding defines, so a transport with no binding has no
/// <i>conformant</i> binary mode — and that is a limit on what we may CLAIM, not on what we may read.</b>
/// Structured mode is defined by the core specification rather than by any binding — the whole event as
/// JSON under <c>application/cloudevents+json</c> — so it is conformant anywhere bytes and a content type
/// travel together, and <see cref="StructuredOnly"/> is the honest answer where nothing binary is written
/// or received.
/// </para>
/// <para>
/// <b>Where this framework already writes attribute names on an unbound transport, it must also read
/// them.</b> That is <see cref="HouseConvention"/>, and it is not an invented prefix filling a gap: the
/// spelling is already on the wire, and refusing to recognise it would publish events this library
/// cannot itself consume. <b>What must never happen is calling such a spelling a protocol binding</b> —
/// a private convention described as spec compliance is the failure the distinction exists to prevent.
/// The convention is legitimate; the claim about it is what has to stay honest.
/// </para>
/// <para>
/// <b>The binding is supplied by the transport rather than looked up.</b> A lookup keyed by transport is
/// a second place to forget one; a required argument cannot be forgotten, because the registration does
/// not compile without it.
/// </para>
/// </remarks>
public sealed class CloudEventBinding
{
	private CloudEventBinding(string name, CloudEventAttributeMatch match, params string[] attributePrefixes)
	{
		Name = name;
		Match = match;
		AttributePrefixes = attributePrefixes;
	}

	/// <summary>
	/// Gets the AMQP 1.0 binding, used by transports whose wire protocol is AMQP 1.0.
	/// </summary>
	/// <remarks>
	/// Reads three spellings and they are <b>not</b> the same kind of thing:
	/// <list type="bullet">
	/// <item>
	/// <c>cloudEvents_</c> — what this framework writes, and what the binding's current revision prefers.
	/// The underscore is preferred for a concrete reason: a colon is not permitted in a JMS 2.0 property
	/// identifier, so the colon form cannot be used in a JMS message selector, and AMQP-to-JMS bridging
	/// is ordinary on these transports.
	/// </item>
	/// <item>
	/// <c>cloudEvents:</c> — the <b>only</b> spelling the released revision of the binding permitted.
	/// A publisher conformant to it writes the colon exclusively and stays conformant forever, so this
	/// read is <b>permanent</b>. It is compatibility with the standard, not with our own history, and it
	/// must never be scheduled for removal.
	/// </item>
	/// <item>
	/// <c>ce-</c> — the HTTP binding's prefix, which this framework wrongly emitted over AMQP in earlier
	/// releases. This one is compatibility with <i>our</i> past and is the only member of this set that
	/// may eventually be withdrawn.
	/// </item>
	/// </list>
	/// </remarks>
	public static CloudEventBinding Amqp10 { get; } = new("AMQP 1.0", CloudEventAttributeMatch.Prefixed, "cloudEvents_", "cloudEvents:", "ce-");

	/// <summary>
	/// Gets the Kafka binding, which assigns <c>ce_</c> because a Kafka header key cannot carry a hyphen.
	/// </summary>
	public static CloudEventBinding Kafka { get; } = new("Kafka", CloudEventAttributeMatch.Prefixed, "ce_");

	/// <summary>
	/// Gets the MQTT binding, which assigns bare attribute names carried as user properties.
	/// </summary>
	/// <remarks>
	/// Bare names are honoured <b>only</b> here. Probing them on a transport whose binding does not assign
	/// them is how an ordinary application property called <c>id</c> or <c>type</c> becomes a CloudEvents
	/// attribute by accident.
	/// </remarks>
	public static CloudEventBinding Mqtt { get; } = new("MQTT", CloudEventAttributeMatch.Bare);

	/// <summary>
	/// Gets the <c>ce-</c> spelling this framework uses on transports the CloudEvents specification
	/// assigns no protocol binding to.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A documented house convention, not a protocol binding, and it must not be described as one.</b>
	/// It applies where the specification is silent — AMQP 0-9-1, SQS/SNS, Pub/Sub and IBM MQ — and the
	/// spelling is borrowed from the HTTP binding because that is what this framework has always written
	/// there.
	/// </para>
	/// <para>
	/// <b>A binding answers two independent questions, and deriving both from one fact is what put
	/// these transports in the wrong row twice.</b>
	/// <list type="number">
	/// <item>
	/// <b>What do we EMIT?</b> A transport whose adapter writes these names and whose receiver refuses
	/// to read them publishes events it cannot itself consume. The format is on the wire either way, so
	/// declining to read it buys no conformance and costs the round trip.
	/// </item>
	/// <item>
	/// <b>What can we BE SENT?</b> Independent of the first, and it is the one that gets skipped —
	/// because emission is easy to check and a criterion that is easy to check gets applied to questions
	/// it does not answer. A transport that emits nothing may still receive these names from a
	/// third-party publisher, and its receiver may have been arranged deliberately to carry them.
	/// </item>
	/// </list>
	/// <b>Either answer alone justifies this binding.</b> Assign <see cref="StructuredOnly"/> only when
	/// <i>both</i> are no — nothing emits binary attributes and nothing can arrive carrying them.
	/// Checking only the first is how a transport that reads a house spelling, and emits none, gets
	/// assigned a binding that reads nothing.
	/// </para>
	/// </remarks>
	public static CloudEventBinding HouseConvention { get; } = new("house convention (ce-)", CloudEventAttributeMatch.Prefixed, "ce-");

	/// <summary>
	/// Gets the binding for a transport the specification assigns none to, which therefore offers
	/// structured mode only.
	/// </summary>
	/// <remarks>
	/// Carries no attribute prefixes, so no inbound message is read as a binary-mode CloudEvent. Structured
	/// mode is unaffected: it is recognised by content type and needs no binding at all, so these
	/// transports send and receive CloudEvents conformantly — just not in binary mode, which for them does
	/// not exist to be conformant with.
	/// </remarks>
	public static CloudEventBinding StructuredOnly { get; } = new("structured only", CloudEventAttributeMatch.None);

	/// <summary>Gets the binding's name, for diagnostics.</summary>
	internal string Name { get; }

	/// <summary>
	/// Gets how this binding names binary-mode attributes on the wire.
	/// </summary>
	/// <remarks>
	/// <b>Stated as a mode rather than inferred from an empty prefix.</b> "Bare names" and "no binary
	/// mode at all" are different facts about a transport, and expressing the first as a prefix that
	/// happens to be the empty string made them a one-element-versus-zero-element distinction that
	/// nothing named. A reader could not tell which was intended, and neither could a reviewer.
	/// </remarks>
	internal CloudEventAttributeMatch Match { get; }

	/// <summary>
	/// Gets the attribute-name prefixes this binding recognises inbound, most-preferred first.
	/// </summary>
	/// <remarks>
	/// Empty unless <see cref="Match"/> is <see cref="CloudEventAttributeMatch.Prefixed"/>.
	/// </remarks>
	internal IReadOnlyList<string> AttributePrefixes { get; }
}
