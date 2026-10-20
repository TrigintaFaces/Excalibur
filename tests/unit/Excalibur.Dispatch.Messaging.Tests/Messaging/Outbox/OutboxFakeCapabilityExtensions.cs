// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Delivery.Registry;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

// FIXTURE HONESTY for the l0qpxo capability seam. Consumers (MessageBusOutboxPublisher, OutboxProcessor)
// discover optional outbox capabilities by probing store.GetService(typeof(T)), not by an `is`-cast. A bare
// FakeItEasy fake answers GetService(object-returning) with a NON-NULL dummy that is NOT the requested interface,
// so a capability the fake genuinely `Implements<T>()` is reported ABSENT — the consumer then throws
// "requires an IMultiTransportOutboxStore implementation" (or silently degrades) against correct code.
//
// A real store returns ITSELF for a capability it implements and null otherwise. This helper teaches a capability-
// implementing fake that same contract, so a test that relies on the capability resolving is not a false RED.
// Applied ONLY to fakes whose test expects the capability to resolve — never to a fake whose test asserts the
// absence path (e.g. WhenAdapterMissing_Throws), which must keep answering null for the unimplemented capability.
internal static class OutboxFakeCapabilityExtensions
{
	public static string RegisteredMessageType
	{
		get
		{
			MessageTypeRegistry.RegisterType<FixtureEvent>();
			return typeof(FixtureEvent).AssemblyQualifiedName!;
		}
	}

	// This fixture models decoding only; codec round trips are proved separately through a real adapter.
	public static IPayloadSerializer CreateRegisteredSerializer()
	{
		_ = RegisteredMessageType;
		var serializer = A.Fake<IPayloadSerializer>();
		A.CallTo(() => serializer.DeserializeObject(A<byte[]>._, typeof(FixtureEvent)))
			.ReturnsLazily((byte[] payload, Type _) => new FixtureEvent { Payload = payload });
		return serializer;
	}

	internal sealed class FixtureEvent : IDispatchEvent
	{
		public byte[] Payload { get; init; } = [];
	}

	public static IOutboxStore WithHonestCapabilities(this IOutboxStore fake)
	{
		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);
		return fake;
	}
}
