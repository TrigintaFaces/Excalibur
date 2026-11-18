// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

/// <summary>
/// The CloudEvents registration split, bound through a REAL service provider rather than by inspecting
/// descriptors.
/// <para>
/// Asserting that a descriptor was added proves only that something was registered; it does not prove
/// the container can build it, and a registration whose dependency is never registered looks identical
/// to a working one until something resolves it. So every arm here resolves the service.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.CloudEvents")]
public sealed class CloudEventRegistrationShould
{
	[Fact]
	public void UseTheConsumerResolverWhenOneIsSupplied()
	{
		var services = new ServiceCollection();
		_ = services.AddDispatch(d => d.AddCloudEvents(PayloadContext.Default));

		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<CloudEventSerializer>();

		serializer.Options.IsReadOnly.ShouldBeTrue();
		serializer.Options.TypeInfoResolver!
			.GetTypeInfo(typeof(ConformancePayload), serializer.Options)
			.ShouldNotBeNull("the consumer's payload metadata must be reachable");
		serializer.Options.TypeInfoResolver!
			.GetTypeInfo(typeof(CloudEvent), serializer.Options)
			.ShouldNotBeNull("the framework envelope metadata must still be reachable");
	}

	[Fact]
	[SuppressMessage(
		"AOT", "IL3050:Calls can break functionality when AOT compiling",
		Justification = "Exercising the reflection overload deliberately; that is this arm's subject.")]
	[SuppressMessage(
		"Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
		Justification = "Exercising the reflection overload deliberately; that is this arm's subject.")]
	public void ResolveAnUndeclaredTypeOnTheReflectionOverload()
	{
		// The majority configuration. ConformanceUndeclared appears in no JsonSerializerContext in this
		// assembly, so this arm fails if the reflection overload stops being reflective -- which is
		// what makes it a check rather than a restatement of the registration.
		var services = new ServiceCollection();
		_ = services.AddDispatch(d => d.AddCloudEvents());

		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<CloudEventSerializer>();

		serializer.Options.TypeInfoResolver!
			.GetTypeInfo(typeof(ConformanceUndeclared), serializer.Options)
			.ShouldNotBeNull();
	}

	[Fact]
	public void TellTheConsumerWhatToCallWhenSerializationWasNeverConfigured()
	{
		// A transport registering CloudEvents support on its own cannot choose the consumer's
		// serialization strategy, so it registers a guard. Reaching the guard must produce an
		// instruction, not a bare unresolved-service error naming two internal types.
		var services = new ServiceCollection();
		_ = services.AddCloudEventSerializerGuard();

		using var provider = services.BuildServiceProvider();
		var ex = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<CloudEventSerializer>());

		ex.Message.ShouldContain("AddCloudEvents");
		ex.Message.ShouldContain("IJsonTypeInfoResolver");
	}

	[Theory]
	[InlineData(true, "guard registered FIRST, then the real registration")]
	[InlineData(false, "real registration FIRST, then the guard")]
	public void LetTheRealRegistrationWinRegardlessOfOrder(bool guardFirst, string order)
	{
		// LIVENESS for the guard, and the property that makes it safe to put in every transport entry
		// point: it must never displace a real registration. Both orders are asserted because TryAdd
		// is order-sensitive by design -- first-one-wins -- so a guard added before the real
		// registration is exactly the case that would break a correctly-configured consumer.
		var services = new ServiceCollection();

		if (guardFirst)
		{
			_ = services.AddCloudEventSerializerGuard();
			_ = services.AddDispatch(d => d.AddCloudEvents(PayloadContext.Default));
		}
		else
		{
			_ = services.AddDispatch(d => d.AddCloudEvents(PayloadContext.Default));
			_ = services.AddCloudEventSerializerGuard();
		}

		using var provider = services.BuildServiceProvider();

		// Resolving must not throw, and must yield the consumer's configured serializer.
		var serializer = provider.GetRequiredService<CloudEventSerializer>();
		serializer.Options.TypeInfoResolver!
			.GetTypeInfo(typeof(ConformancePayload), serializer.Options)
			.ShouldNotBeNull($"with {order}, the consumer's resolver must survive");
	}

	[Fact]
	public void SerializeAPayloadThroughTheRegisteredSerializerWithoutReflection()
	{
		// End to end through the container: the decided options are usable, not merely well-formed.
		var services = new ServiceCollection();
		_ = services.AddDispatch(d => d.AddCloudEvents(PayloadContext.Default));

		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<CloudEventSerializer>();

		var json = serializer.SerializePayload(new ConformancePayload { Name = "alpha", Count = 22 });

		json.ShouldContain("alpha");
		json.ShouldContain("22");
	}

	[Fact]
	public void FailLoudlyAndNameTheTypeWhenTheResolverDoesNotCoverAPayload()
	{
		// The diagnostic a consumer actually needs. Emitting a rendered type name as the payload -- the
		// behaviour a ToString() fallback would give -- is the silent-failure alternative, and nothing
		// downstream could detect it.
		var services = new ServiceCollection();
		_ = services.AddDispatch(d => d.AddCloudEvents(PayloadContext.Default));

		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<CloudEventSerializer>();

		var ex = Should.Throw<NotSupportedException>(
			() => serializer.SerializePayload(new ConformanceUndeclared { Value = "x" }));

		ex.Message.ShouldContain(nameof(ConformanceUndeclared));
	}
}
