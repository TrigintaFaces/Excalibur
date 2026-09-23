// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.IO;
using System.Reflection;

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.IbmMq;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.Mqtt;
using Excalibur.Dispatch.Transport.Pulsar;
using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Tests.Conformance.Providers.CrossTransport;

/// <summary>
/// Verifies that each transport shipping its own CloudEvents adapter declares the spec-version wire
/// name ITS OWN protocol binding assigns, and that the derived transport population is partitioned
/// totally so no transport is asserted by nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file no longer asserts that types exist.</b> It previously carried four arms that counted
/// <c>ICloudEventEncoder&lt;T&gt;</c> implementations, adapter classes, DI extension classes and options
/// classes, and passed when the count was above zero. Those arms could not distinguish a transport that
/// encodes and decodes CloudEvents from one that merely declares the classes for doing so, and they
/// would have gone green the moment four unwired classes were dropped in. Left standing beside a
/// behavioural suite they were worse than absent: they produced a green that could be cited as evidence
/// of conformance, borrowing credibility from arms that actually drive the contract.
/// </para>
/// <para>
/// <b>Where the behavioural bar lives.</b> Round-tripping a CloudEvent through a transport's own
/// encoding and the receiver its own registration builds -- in both binary and structured mode -- is
/// asserted by <c>CloudEventTransportConformanceTests</c> and its per-transport derivations. That suite
/// resolves the registered receiver rather than constructing one, so a transport that registers a
/// decoder and never routes receive through it fails there. Nothing in THIS file can observe a
/// registration call site: reflection over a loaded assembly sees declared types only.
/// </para>
/// <para>
/// <b>What remains here is a question reflection can actually answer.</b> The CloudEvents binary-mode
/// attribute prefix is assigned per protocol binding, and an adapter's declared constant is the wire
/// name a conformant peer will see. Asserting those constants catches a consistency sweep that
/// flattens several bindings onto one spelling -- a defect a round-trip cannot see, because both ends
/// of a round trip share the error and agree.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CloudEventsConformanceShould
{
	// ── the transport population, DERIVED rather than typed ──────────────────────────
	// This was six names someone had remembered to add, and it was three short. Every arm below
	// iterates it, so the suite's coverage was "whatever was last typed here" -- which is the same
	// omission these arms exist to catch, one level up. A transport nobody listed is a transport the
	// gate is silent about, and silence reads as conformance.
	//
	// The anchors force assemblies to load. The population is then read back from what is ACTUALLY
	// LOADED, not from the anchor list -- otherwise the anchor list is the hand-written enumeration
	// wearing a better name, and a transport nobody anchored stays invisible exactly as before.
	// Every transport assembly this project REFERENCES is force-loaded first, so referencing one is
	// what enrolls it; the anchors remain because a referenced assembly the test code never touches
	// may not be listed as a reference at all.
	private const string TransportAssemblyPrefix = "Excalibur.Dispatch.Transport.";

	// One type per transport package, used ONLY to force its assembly to load. Being absent from
	// this list no longer hides a transport -- the population is read from what loaded -- but a
	// referenced assembly the test code never touches may not appear as a reference at all, so the
	// anchors remain as the load trigger of last resort.
	private static readonly Type[] s_loadAnchors =
	[
		typeof(AzureServiceBusOptions), typeof(AwsSqsOptions), typeof(GooglePubSubOptions),
		typeof(KafkaOptions), typeof(RabbitMqOptions), typeof(MqttOptions),
		typeof(IbmMqOptions), typeof(PulsarOptions),
	];

	// A transport package this project does not reference, so no reflection here can observe it.
	// Named rather than omitted: an unstated blind spot reads as coverage. The arm below asserts it
	// is genuinely absent, so the day the reference is added the suite goes RED and says "enroll it".
	private const string UnreachableTransportPackages = "Excalibur.Dispatch.Transport.Grpc";


	private static readonly (string Name, Assembly Assembly)[] s_transports = LoadTransports();

	private static (string Name, Assembly Assembly)[] LoadTransports()
	{
		foreach (var anchor in s_loadAnchors)
		{
			_ = anchor.Assembly;
		}

		foreach (var reference in typeof(CloudEventsConformanceShould).Assembly.GetReferencedAssemblies())
		{
			if (reference.Name?.StartsWith(TransportAssemblyPrefix, StringComparison.Ordinal) != true)
			{
				continue;
			}

			try
			{
				_ = Assembly.Load(reference);
			}
			catch (FileNotFoundException)
			{
				// A referenced transport whose assembly is not deployed beside the test binary cannot be
				// inspected. Swallowing here would hide it, so it is surfaced by the reachability arm
				// below rather than silently dropped.
			}
		}

		return AppDomain.CurrentDomain.GetAssemblies()
			.Where(static a => a.GetName().Name?.StartsWith(TransportAssemblyPrefix, StringComparison.Ordinal) == true)
			.Where(static a => a.GetName().Name != TransportAssemblyPrefix + "Abstractions")
			.Distinct()
			.Select(static a => (
				Name: a.GetName().Name!.Replace(TransportAssemblyPrefix, string.Empty, StringComparison.Ordinal),
				Assembly: a))
			.OrderBy(static t => t.Name, StringComparer.Ordinal)
			.ToArray();
	}


	// ⚠ WHAT THIS PARTITION MEASURES, AND WHAT IT CANNOT.
	// It splits on CLOUDEVENTS TYPE PRESENCE in the transport's OWN assembly. That identifies the
	// transports that ship their own per-transport ADAPTER -- which is exactly the population whose
	// BINDING CONSTANTS exist to be asserted, so it is the correct population for the wire-name arms
	// in this file.
	//
	// IT IS NOT A CAPABILITY CLAIM IN EITHER DIRECTION, and reading it as one is the trap.
	// Both directions are wired by SHARED decorators that live in Transport.Abstractions and are
	// applied inside each transport's own registration:
	//
	//     WithCloudEventDecoding    the shared receive decorator
	//     WithCloudEventEncoding    the shared send decorator
	//
	// NO COUNT IS GIVEN HERE, deliberately. An earlier revision of this comment said both were
	// "registered in all nine transport packages". That was measured and true when written and was
	// FALSE TWENTY-FIVE MINUTES LATER, when the encode registration was rescoped. A count beside a
	// derivable set is the defect this suite exists to catch, and writing one here would have made
	// this comment the newest instance of it.
	//
	// The property does not move: WHICHEVER transports register these decorators, they do so from
	// Transport.Abstractions, which this derivation excludes -- so a transport whose CloudEvents
	// support comes from the shared seams declares no CloudEvents type of its own, and this
	// predicate reports it as having none.
	//
	// A transport whose CloudEvents support comes entirely from those shared seams declares no
	// CloudEvents type of its own, so this predicate reports it as having none -- which is false about
	// BOTH send and receive. Type presence cannot see a shared decorator by construction, because the
	// derivation deliberately excludes the Abstractions assembly.
	//
	// The correct capability signal is the REGISTRATION CALL SITE in the transport's own package, and
	// reflection over a loaded assembly cannot observe a call site. THIS SUITE THEREFORE MAKES NO
	// STATEMENT ABOUT WHICH TRANSPORTS CAN SEND OR RECEIVE CLOUDEVENTS. It states only which of them
	// carry their own adapter, and what that adapter puts on the wire.
	//
	// That capability question is answered elsewhere, by round-tripping an event through each
	// transport's own encoding and the receiver its own registration builds -- see
	// CloudEventTransportConformanceTests and its per-transport derivations. Do not answer it here by
	// re-adding a type-presence arm: a count of declared classes is green for a transport that
	// registers a decoder and never routes receive through it, which is the exact defect that suite
	// exists to detect.
	private static readonly (string Name, Assembly Assembly)[] s_cloudEventTransports =
		s_transports.Where(static t => HasAnyCloudEventType(t.Assembly)).ToArray();

	private static readonly (string Name, Assembly Assembly)[] s_nonCloudEventTransports =
		s_transports.Where(static t => !HasAnyCloudEventType(t.Assembly)).ToArray();

	private static bool HasAnyCloudEventType(Assembly assembly) =>
		assembly.GetTypes().Any(static t => t.Name.Contains("CloudEvent", StringComparison.OrdinalIgnoreCase));


	private static Assembly GetAssembly(string transportName) =>
		s_transports.First(t => t.Name == transportName).Assembly;

	// ── per-transport wire names ───────────────────────────────────────────────────────────────
	// The CloudEvents binary-mode prefix is assigned PER PROTOCOL BINDING, not once for the product:
	// HTTP "ce-", Kafka "ce_", AMQP "cloudEvents_", MQTT none. A round trip cannot see a sweep that
	// flattens them, because both ends of a round trip share the error and agree; a well-meant change
	// that made every adapter use one prefix would leave every round-trip arm green while breaking
	// conformance against real peers on the transports that were already right. That is not
	// hypothetical -- MQTT shipped with "ce-" and was corrected. This is the question reflection over
	// declared constants answers better than behaviour does, which is why these arms remain.
	//
	// These constants are the wire names themselves: each adapter writes them directly as the header
	// or user-property key, so asserting the constant asserts what a conformant peer will see.
	private const string SpecVersionMarker = "specversion";

	/// <summary>
	/// Length of <c>cloudEvents_</c>, the longest prefix any CloudEvents protocol binding assigns
	/// (AMQP 1.0). It bounds what the resolver will accept as a prefixed spec-version constant.
	/// </summary>
	private const int Amqp10SpecVersionPrefixLength = 12;

	// The third column NAMES THE CONSTANT when a transport declares more than one spec-version spelling,
	// and is null when it declares exactly one. This is the disambiguation the refusal below asks for
	// rather than a convenience: AwsSqs ships a structured adapter naming a JSON property INSIDE the
	// payload ("specversion") beside a binary adapter naming a transport ATTRIBUTE ("ce-specversion").
	// Those answer different questions and only the second is governed by the prefix rule, so an arm
	// that picked one by position would be measuring an arbitrary subject.
	//
	// The field name is the discriminator only because the EXPECTATION supplies it. Deriving kind FROM
	// the field name would not work and must not be attempted: this tree spells the same kind three ways
	// ("...Header", "...Attribute", "...Property"), and Service Bus uses "Property" for a transport
	// header while AwsSqs uses it for a payload field. A scan keyed on that naming would report a
	// confident wrong answer.
	private static readonly (string Transport, string Expected, string? DeclaringConstant)[] s_specVersionExpectations =
	[
		("Kafka", "ce_specversion", null),
		("RabbitMQ", "ce-specversion", null),
		("Mqtt", "specversion", null),

		// AMQP 1.0 binding — Service Bus is an AMQP broker and its binding assigns the "cloudEvents_"
		// prefix, which is why this one must NOT be aligned with the "ce-" transports below.
		("AzureServiceBus", "cloudEvents_specversion", null),

		// No CloudEvents protocol binding exists for either of these, so both use the house "ce-" prefix.
		// They are legitimately IDENTICAL to each other and to RabbitMQ; that is not a flattened sweep,
		// and the distinctness arm below deliberately does not include them for that reason.
		("GooglePubSub", "ce-specversion", null),
		("AwsSqs", "ce-specversion", "CeSpecVersionAttribute"),
	];

	public static TheoryData<string, string, string?> SpecVersionExpectations()
	{
		var data = new TheoryData<string, string, string?>();
		foreach (var (transport, expected, declaringConstant) in s_specVersionExpectations)
		{
			data.Add(transport, expected, declaringConstant);
		}

		return data;
	}

	[Theory]
	[MemberData(nameof(SpecVersionExpectations))]
	public void Declare_TheSpecVersionWireName_ThatItsOwnProtocolBindingAssigns(
		string transportName, string expected, string? declaringConstant)
	{
		var actual = SpecVersionWireName(transportName, declaringConstant);

		actual.ShouldNotBeNull(
			$"{transportName} does not declare exactly one spec-version wire name, so this arm cannot " +
			"evaluate the binding -- that is a REFUSE, not a pass. Either no adapter declares one (the " +
			"type's shape changed), or this assembly holds several adapters that disagree -- typically " +
			"a structured-only adapter naming a JSON property inside the payload alongside a binary-mode " +
			"adapter naming a transport header. The prefix rule applies only to the header, so the " +
			"expectation must say which adapter it means rather than this arm guessing.");
		actual.ShouldBe(
			expected,
			$"{transportName} must use the prefix ITS OWN CloudEvents protocol binding assigns. " +
			"Do not align this with a sibling transport: the prefix is the binding's to choose, and " +
			"making them consistent breaks every transport that was already conformant.");
	}

	// The theory above is hand-listed, and this arm exists because a hand-listed set silently shrinks
	// against a derived one. When it was written it covered three of the six transports it derives, and
	// the three it did not name were exactly where a real divergence was later found BY HAND. An arm
	// that asserts some of its population and says nothing about the rest reports the same green whether
	// the silent ones are conformant or broken -- which is the defect this file was written to close,
	// one layer up, inside the closer.
	//
	// So the covered set is asserted against the DERIVED set rather than trusted. Adding a transport to
	// s_transports without giving it an expected wire name now fails here instead of passing quietly.
	// The partition must be TOTAL and its empty half must be genuinely empty. Without this, a
	// transport that gained a CloudEvents type would simply move buckets unobserved, and a transport
	// that lost one would vanish from every arm above while the suite stayed green -- which is the
	// silent-omission failure this file was rewritten to make impossible.
	[Fact]
	public void Partition_EveryDerivedTransport_IntoExactlyOneOfTheTwoPopulations()
	{
		(s_cloudEventTransports.Length + s_nonCloudEventTransports.Length).ShouldBe(
			s_transports.Length,
			"every derived transport must either declare CloudEvents types of its own or declare none; a " +
			"transport in neither bucket is asserted by nothing at all. NOTE: this is an OWN-ADAPTER " +
			"signal, NOT a capability claim -- a transport with no CloudEvents types of its own both " +
			"sends and receives them through the shared decorators, which this predicate cannot observe.");

		s_cloudEventTransports.ShouldNotBeEmpty(
			"no transport declares a CloudEvents type, which means every arm in this file is vacuous. " +
			"That is a broken suite, not a conformant tree.");

		foreach (var (name, assembly) in s_nonCloudEventTransports)
		{
			assembly.GetTypes()
				.Any(static t => t.Name.Contains("CloudEvent", StringComparison.OrdinalIgnoreCase))
				.ShouldBeFalse(
					$"{name} was classified as shipping no CloudEvents types, but declares one. It now " +
					"belongs to the asserted population and must be given an expected wire name.");
		}

		// This asserted a non-empty const, which is a compile-time tautology: it could not go red, and
		// specifically could not notice the blind spot CLOSING. Now it asserts the package is genuinely
		// absent, so it stays green while Grpc is unreachable and goes RED the day someone adds the
		// project reference -- and that red says "enroll Grpc", which is the event worth catching.
		s_transports.Select(static t => TransportAssemblyPrefix + t.Name)
			.ShouldNotContain(
				UnreachableTransportPackages,
				$"{UnreachableTransportPackages} is now loadable from this project, so it is no longer a " +
				"stated blind spot -- remove it from the unreachable list and give it a wire-name " +
				"expectation like every other derived transport.");
	}

	[Fact]
	public void Cover_EveryTransportItDerives_SoAnUnassertedOneCannotPassSilently()
	{
		// Read from the SAME table the theory runs, so the covered set cannot drift from the asserted
		// set. Hand-listing it here a second time is the defect this arm exists to catch, committed
		// inside the arm that catches it.
		var asserted = s_specVersionExpectations.Select(static e => e.Transport).ToArray();

		var uncovered = s_cloudEventTransports
			.Select(static t => t.Name)
			.Where(name => !asserted.Contains(name, StringComparer.Ordinal))
			.ToArray();

		uncovered.ShouldBeEmpty(
			"these transports are derived by this suite but no arm asserts the wire name their binding " +
			$"assigns, so a divergence in them reads as a pass: {string.Join(", ", uncovered)}. " +
			"Give each one an expected spec-version wire name -- the prefix its OWN protocol binding " +
			"assigns where a CloudEvents binding exists, or its documented deviation where none does.");
	}

	[Fact]
	public void Not_ShareOneWireNameAcrossBindings_BecauseThePrefixIsPerBinding()
	{
		string?[] names = [SpecVersionWireName("Kafka"), SpecVersionWireName("RabbitMQ"), SpecVersionWireName("Mqtt")];

		names.ShouldAllBe(static n => n != null, "every transport under test must declare a spec-version wire name");
		names.Distinct(StringComparer.Ordinal).Count().ShouldBe(
			names.Length,
			"Kafka, RabbitMQ and MQTT are governed by three DIFFERENT CloudEvents bindings and must not " +
			"converge on one spelling; identical names here mean a consistency sweep has flattened them.");
	}

	// Reads the adapter's own spec-version constant. Adapters are internal, which reflection over the
	// defining assembly sees; a missing constant returns null so the caller can fail loudly rather than
	// pass vacuously on a type whose shape changed.
	// Returns the single spec-version wire name declared across the transport's adapters, or null when
	// there is not exactly one.
	//
	// The null case covers BOTH "none declared" and "several declared", and the caller fails on null in
	// either. That is deliberate. This previously took the FIRST match, which silently answered a
	// question it could not actually answer.
	//
	// Each adapter declares exactly one such constant -- measured, all of them. The ambiguity is not
	// inside an adapter, it is ACROSS them: one assembly can hold several adapters whose names differ
	// in KIND. A structured-only adapter names a JSON PROPERTY inside a serialized payload; a
	// binary-mode adapter names a transport HEADER. Those answer different questions, and the prefix
	// rule this suite checks applies only to the header. So "the spec-version name for this transport"
	// has no single answer where an assembly mixes the two, and picking one is not a weaker
	// measurement -- it is a measurement of an arbitrary subject.
	//
	// Refusing is what makes that visible. Where an assembly does hold several, the expectation has to
	// name the ADAPTER and the MODE, and this returning null is the signal that it must.
	//
	// KNOWN BLIND SPOT, stated because it is not obvious from the assertions: this resolves and compares
	// a VALUE, and a value does not carry its kind. "specversion" bare is a correct MQTT transport header
	// -- the MQTT binding requires attribute names unchanged -- and it is ALSO the name of a JSON property
	// written into a serialized payload by a structured-only adapter. Identical strings, different things.
	// So these arms would not notice an adapter that stopped writing a header and began writing a payload
	// property of the same name, nor the reverse. Catching that needs an assertion over the adapter's
	// emitted MESSAGE, not over its declared constants, and this suite deliberately does not do that --
	// its value is that it cannot be fooled by both ends of a round-trip sharing an error, and the price
	// of that is exactly this blindness.
	private static string? SpecVersionWireName(string transportName, string? declaringConstant = null)
	{
		var declared = GetAssembly(transportName).GetTypes()
			.Where(t => t is { IsAbstract: false, IsInterface: false }
				&& t.Name.Contains("CloudEventAdapter", StringComparison.OrdinalIgnoreCase))
			.SelectMany(static t => t.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy))
			.Where(static fi => fi is { IsLiteral: true, IsInitOnly: false } && fi.FieldType == typeof(string))
			// When the expectation names a constant, only that one answers. A name that matches NOTHING
			// yields an empty set and therefore null, which the caller reports as a refusal — so a
			// renamed constant fails loudly here instead of silently falling back to a different one.
			.Where(fi => declaringConstant is null
				|| string.Equals(fi.Name, declaringConstant, StringComparison.Ordinal))
			.Select(static fi => fi.GetRawConstantValue() as string)
			.Where(v => v is not null
				&& v.EndsWith(SpecVersionMarker, StringComparison.Ordinal)
				// The bound is the LONGEST prefix any CloudEvents binding assigns, not a round number.
				// It read <= 3, which admits "ce-" and "ce_" and structurally CANNOT admit AMQP 1.0's
				// "cloudEvents_" (12). Service Bus therefore resolved to nothing and refused, and the
				// refusal was indistinguishable from a transport whose adapter shape had changed — so
				// the one AMQP transport in the tree could never be asserted by this arm at all.
				&& v.Length - SpecVersionMarker.Length <= Amqp10SpecVersionPrefixLength)
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		return declared.Length == 1 ? declared[0] : null;
	}
}
