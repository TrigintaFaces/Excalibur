// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.Conformance.Providers.CrossTransport;

/// <summary>
/// Holds the constructor-parameter detector used to ask whether a type's dependency shape even admits a
/// call to <see cref="IEnvelopeCloudEventBridge"/>, together with the synthetic positive and negative
/// controls that prove the detector can answer both ways.
/// </summary>
/// <remarks>
/// <para>
/// <b>This class no longer asserts anything about the transports.</b> It previously carried two arms
/// over a hand-listed roster: that every send-wired message bus accepts the bridge as a constructor
/// parameter, and that no receive surface accepts one. Both were removed, for different reasons.
/// </para>
/// <para>
/// The send arm was constructor-shape presence -- a type that accepts a dependency and never calls it
/// satisfies it -- which is the same class of check as the type-counting arms removed from
/// <see cref="CloudEventsConformanceShould"/>.
/// </para>
/// <para>
/// <b>The receive arm was worse than vacuous, and that is the case worth remembering.</b> It asserted
/// that no subscriber or receiver accepts the bridge, on the stated premise that a constructor parameter
/// was "the only place a RECEIVE decode could live". That premise stopped being true: decoding ships as
/// a DECORATOR wrapping the receiver, taking a decoder, applied inside each transport's own
/// registration. So the assertion stayed green, was documented in prose as meaning receive was unwired,
/// and went on reading that way after receive was wired on every transport. A vacuous arm asserts
/// nothing and teaches nothing; this one asserted something false to its reader, and its careful
/// explanation of why the green mattered was exactly what would stop the next person checking. It was
/// also wrong in the other direction: had anyone wired receive through the bridge legitimately, it would
/// have gone red and directed them to add round-trip coverage that already exists.
/// </para>
/// <para>
/// Whether a transport can send and receive CloudEvents is answered by round-tripping an event through
/// that transport's own encoding and the receiver its own registration builds -- see
/// <c>CloudEventTransportConformanceTests</c> and its per-transport derivations. Do not re-add a
/// dependency-shape arm here to answer it.
/// </para>
/// <para>
/// <b>What remains, and why it is not dead.</b> The detector below and its two synthetic controls stay
/// because they are consumed outside this class: the positive control is the PASS fixture of a CI gate's
/// own non-vacuity self-test, chosen because it needs no container. The negative control stays with it
/// -- keeping the fixture that proves the detector can say yes while discarding the one that proves it
/// can say no would retain the arm that cannot fail and drop the arm that can.
/// </para>
/// <para>
/// A first attempt at the removed arms scanned compiled method bodies (IL) for calls to
/// <see cref="IEnvelopeCloudEventBridge"/>'s methods, the same technique
/// <c>NoTransportMethodCallsOnlyItselfShould</c> uses elsewhere in this project -- but resolving IL
/// tokens against the AWS/Azure/Google SDK client assemblies crashed the test host outright (a native
/// stack overflow, which .NET cannot catch, twice, even after narrowing the scanned population). Do not
/// reintroduce raw IL-token scanning over those assemblies without isolating it in its own process.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CloudEventsWiringConformanceShould
{
	[Fact]
	public void DetectorFindsAGenuineBridgeConstructorParameter()
	{
		AcceptsBridgeAsConstructorParameter(typeof(TypeWithBridgeConstructorParameter)).ShouldBeTrue(
			"the detector cannot recognise a real IEnvelopeCloudEventBridge constructor parameter, so a "
			+ "clean report below is meaningless");
	}

	[Fact]
	public void DetectorRejectsATypeWithNoBridgeConstructorParameter()
	{
		AcceptsBridgeAsConstructorParameter(typeof(TypeWithNoBridgeConstructorParameter)).ShouldBeFalse(
			"the detector reports a bridge parameter that is not there, so a red report below would be "
			+ "meaningless too");
	}

	/// <summary>
	/// Whether any constructor on <paramref name="type"/> declares a parameter of type
	/// <see cref="IEnvelopeCloudEventBridge"/>. Reads <see cref="ParameterInfo"/> only -- no method-body
	/// IL, no <see cref="Module.ResolveMethod(int)"/> -- so it cannot trigger the JIT/metadata-loading
	/// path that crashed the IL-scanning attempt described in the class remarks.
	/// </summary>
	private static bool AcceptsBridgeAsConstructorParameter(Type type) =>
		type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
			.SelectMany(c => c.GetParameters())
			.Any(p => p.ParameterType == typeof(IEnvelopeCloudEventBridge));

	/// <summary>The detector's positive control: a real constructor parameter of the bridge type.</summary>
	private sealed class TypeWithBridgeConstructorParameter(IEnvelopeCloudEventBridge bridge)
	{
		public IEnvelopeCloudEventBridge Bridge { get; } = bridge;
	}

	/// <summary>The detector's negative control: no such parameter anywhere.</summary>
	private sealed class TypeWithNoBridgeConstructorParameter(string somethingElse)
	{
		public string SomethingElse { get; } = somethingElse;
	}
}
