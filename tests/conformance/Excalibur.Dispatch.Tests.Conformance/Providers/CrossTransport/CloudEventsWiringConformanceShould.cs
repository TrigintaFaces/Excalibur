// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Tests.Conformance.Providers.CrossTransport;

/// <summary>
/// w7avib. <see cref="CloudEventsConformanceShould"/> only checks that a mapper/adapter/DI-extension
/// CLASS EXISTS per transport assembly -- it is presence, not behavior, and it reported "conformant" for
/// Kafka right up to the 2hgehp fix, when Kafka's message bus injected the mapper, null-checked it,
/// logged that it had "resolved" the mapper for the publish path, and never called any of its methods.
/// </summary>
/// <remarks>
/// <para>
/// This checks a stronger, still-safe signal: does the DEPENDENCY SHAPE of each candidate type even
/// admit a bridge to call? A first attempt at this check scanned compiled method bodies (IL) for a call
/// to <see cref="IEnvelopeCloudEventBridge"/>'s methods, the same technique
/// <c>NoTransportMethodCallsOnlyItselfShould</c> uses elsewhere in this project -- but resolving IL
/// tokens against the AWS/Azure/Google SDK client assemblies these transports depend on crashed the test
/// host outright (a native stack overflow, which .NET cannot catch, twice, even after narrowing the
/// scanned population). That approach is abandoned here; do not reintroduce raw IL-token scanning over
/// these assemblies without isolating it in its own process.
/// </para>
/// <para>
/// The actual behavioral proof for SEND lives in per-transport unit tests that construct the REAL
/// production mapper, bridge, and envelope converter (only the provider SDK client is faked) and assert
/// the emitted wire message carries CloudEvent attributes:
/// <c>AwsSqsMessageBusShould.PublishEvent_WhenCloudEventsConfigured_EmitsCloudEventAttributesOnDefaultSend</c>
/// and <c>KafkaMessageBusShould.PublishEvent_WhenCloudEventsConfigured_EmitsCloudEventOnDefaultSend</c> in
/// <c>Excalibur.Dispatch.Transport.Tests</c>. What this class adds is the STRUCTURAL half those two don't
/// cover: that every other send-wired message bus has the same dependency shape (so the same proof
/// generalizes), and -- the w7avib headline -- that no receive-side type anywhere can even reach the
/// bridge, because none of them accept one.
/// </para>
/// <para>
/// Non-vacuity is structural, not by mutation: <see cref="TypeWithBridgeConstructorParameter"/> and
/// <see cref="TypeWithNoBridgeConstructorParameter"/> below are synthetic positive/negative controls, and
/// the same detector must tell them apart. If it ever stops being able to, those two arms fail first.
/// </para>
/// <para>
/// Measured 2026-09-07 at HEAD (post-2hgehp-partial): every constructor below that accepts
/// <see cref="IEnvelopeCloudEventBridge"/> is a message bus (a SEND surface). NONE of the subscriber or
/// receiver types -- the only place a RECEIVE decode could live -- accept one. This is the finding
/// w7avib exists to keep visible: it will start failing the moment any receive-surface constructor gains
/// a bridge parameter, and it must then be updated by a human who read why (moving that transport's
/// receive surface into a real round-trip test), not silently left green.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CloudEventsWiringConformanceShould
{
	/// <summary>
	/// Message-bus types verified (2026-09-07, and behaviorally proven for AWS SQS and Kafka by the unit
	/// tests named in the class remarks) to be SEND-wired.
	/// </summary>
	private static readonly (string Transport, Type MessageBusType)[] s_sendWired =
	[
		("RabbitMQ", GetType(typeof(RabbitMqOptions).Assembly, "Excalibur.Dispatch.Transport.RabbitMQ.RabbitMqMessageBus")),
		("Kafka", GetType(typeof(KafkaOptions).Assembly, "Excalibur.Dispatch.Transport.Kafka.KafkaMessageBus")),
		("AwsSqs", GetType(typeof(AwsSqsOptions).Assembly, "Excalibur.Dispatch.Transport.Aws.AwsSqsMessageBus")),
		("AwsSns", GetType(typeof(AwsSqsOptions).Assembly, "Excalibur.Dispatch.Transport.Aws.AwsSnsMessageBus")),
		("AwsEventBridge", GetType(typeof(AwsSqsOptions).Assembly, "Excalibur.Dispatch.Transport.Aws.AwsEventBridgeMessageBus")),
		("AzureServiceBus", GetType(typeof(AzureServiceBusOptions).Assembly, "Excalibur.Dispatch.Transport.Azure.AzureServiceBusMessageBus")),
		("AzureEventHubs", GetType(typeof(AzureServiceBusOptions).Assembly, "Excalibur.Dispatch.Transport.Azure.AzureEventHubMessageBus")),
		("GooglePubSub", GetType(typeof(GooglePubSubOptions).Assembly, "Excalibur.Dispatch.Transport.Google.GooglePubSubMessageBus")),
	];

	/// <summary>
	/// Every named receive-surface type per mapped transport: its subscriber and its receiver. The
	/// message bus is deliberately EXCLUDED here even though it now legitimately accepts
	/// <see cref="IEnvelopeCloudEventBridge"/> for SEND -- <c>IMessageBus</c> declares only
	/// <c>PublishAsync</c> overloads, no subscribe/receive method, so accepting the bridge on a message
	/// bus is never evidence of receive-capability; it would just make this check fail on the transports
	/// this lane JUST fixed for send. Receive, if it existed, could only live in the subscriber/receiver.
	/// </summary>
	private static readonly (string Transport, Type[] CandidateTypes)[] s_receiveSurfaces =
	[
		("RabbitMQ", GetTypes(typeof(RabbitMqOptions).Assembly,
			"Excalibur.Dispatch.Transport.RabbitMQ.RabbitMqTransportSubscriber",
			"Excalibur.Dispatch.Transport.RabbitMQ.RabbitMqTransportReceiver")),
		("Kafka", GetTypes(typeof(KafkaOptions).Assembly,
			"Excalibur.Dispatch.Transport.Kafka.KafkaTransportSubscriber",
			"Excalibur.Dispatch.Transport.Kafka.KafkaTransportReceiver")),
		("AwsSqs", GetTypes(typeof(AwsSqsOptions).Assembly,
			"Excalibur.Dispatch.Transport.Aws.SqsTransportSubscriber",
			"Excalibur.Dispatch.Transport.Aws.SqsTransportReceiver")),
		("AzureServiceBus", GetTypes(typeof(AzureServiceBusOptions).Assembly,
			"Excalibur.Dispatch.Transport.Azure.ServiceBusTransportSubscriber",
			"Excalibur.Dispatch.Transport.Azure.ServiceBusTransportReceiver")),
		("GooglePubSub", GetTypes(typeof(GooglePubSubOptions).Assembly,
			"Excalibur.Dispatch.Transport.Google.PubSubTransportSubscriber",
			"Excalibur.Dispatch.Transport.Google.PubSubTransportReceiver")),
	];

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

	[Theory]
	[MemberData(nameof(SendWiredTransportNames))]
	public void HaveTheSendPathAcceptTheBridge(string transportName)
	{
		var messageBusType = s_sendWired.First(t => t.Transport == transportName).MessageBusType;

		AcceptsBridgeAsConstructorParameter(messageBusType).ShouldBeTrue(
			$"{messageBusType.FullName} has no constructor accepting IEnvelopeCloudEventBridge, so it "
			+ "cannot call it regardless of what its method bodies claim to do.");
	}

	[Theory]
	[MemberData(nameof(ReceiveSurfaceTransportNames))]
	public void HaveNoReceiveSurfaceAcceptTheBridgeYet(string transportName)
	{
		var candidates = s_receiveSurfaces.First(t => t.Transport == transportName).CandidateTypes;

		var wired = candidates.Where(AcceptsBridgeAsConstructorParameter).ToArray();

		// The w7avib non-vacuity requirement: this assertion must be TRUE today (no receive surface can
		// even reach the bridge) -- and it is written so that the moment it stops being true for some
		// transport, this line fails and must be edited by a human who read why, rather than staying
		// silently green forever. Do NOT "fix" a red here by weakening the assertion; fix it by moving
		// that transport out of s_receiveSurfaces once its receive path is genuinely wired and behaviorally
		// tested (a real round trip, per the class remarks), not merely constructor-shaped for it.
		wired.ShouldBeEmpty(
			$"{string.Join(", ", wired.Select(t => t.FullName))} now accept(s) IEnvelopeCloudEventBridge -- "
			+ $"if this is genuine new receive-path wiring, move \"{transportName}\" out of "
			+ "CloudEventsWiringConformanceShould.s_receiveSurfaces and add real send+receive round-trip "
			+ "coverage for it instead of leaving this assertion to rot.");
	}

	public static TheoryData<string> SendWiredTransportNames()
	{
		var data = new TheoryData<string>();
		foreach (var (name, _) in s_sendWired)
		{
			data.Add(name);
		}

		return data;
	}

	public static TheoryData<string> ReceiveSurfaceTransportNames()
	{
		var data = new TheoryData<string>();
		foreach (var (name, _) in s_receiveSurfaces)
		{
			data.Add(name);
		}

		return data;
	}

	private static Type GetType(Assembly assembly, string fullName) =>
		assembly.GetType(fullName, throwOnError: true)!;

	private static Type[] GetTypes(Assembly assembly, params string[] fullNames) =>
		[.. fullNames.Select(n => GetType(assembly, n))];

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
