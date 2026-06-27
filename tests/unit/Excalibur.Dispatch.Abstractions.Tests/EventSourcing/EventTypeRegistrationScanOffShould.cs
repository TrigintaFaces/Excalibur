// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Author≠impl regression lock for S851 fast-follow <c>c6wd6f</c> — the secure-by-default
/// <see cref="JsonEventSerializer"/> (wpynky: scan-off rejects unregistered types) must also be
/// <b>functional</b>: a type registered via the public <c>AddEventTypes&lt;T&gt;()</c> helper resolves
/// <em>without</em> the assembly scan, while an unregistered (attacker-chosen) type still throws — so the
/// registry adds a functional path WITHOUT re-opening the gadget-chain vector.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (BackendDeveloper) against the committed-skeleton/working-tree
/// c6wd6f surface, through the <b>public</b> <c>AddEventTypes</c> registration path (consumer-faithful;
/// <see cref="IEventTypeRegistry"/> stays internal).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> <see cref="RegisteredType_ResolvesScanOff_ViaAddEventTypesHelper"/> (registered →
/// resolves) and <see cref="UnregisteredType_StillRejectedScanOff_EvenWithRegistry"/> (unregistered →
/// throws) use the <em>same</em> serializer with scan OFF, differing only by registration — so they bind
/// the registry-consult as load-bearing: remove the consult (pre-c6wd6f) and the registered case goes RED
/// (scan-off throws); ignore registration and the unregistered case goes RED (resolves).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventTypeRegistrationScanOffShould
{
	private sealed record RegisteredTestEvent(string Id);
	private sealed record SecondRegisteredTestEvent(int Value);

	// A concrete IDomainEvent in THIS (the test) assembly, so AddEventTypesFromAssembly's
	// IDomainEvent-filtered scan registers it.
	private sealed record AssemblyScannedTestEvent : IDomainEvent
	{
		public string EventId { get; init; } = "evt-1";
		public string AggregateId { get; init; } = "agg-1";
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; } = nameof(AssemblyScannedTestEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	public void RegisteredType_ResolvesScanOff_ViaAddEventTypesHelper()
	{
		// Arrange — register the type through the PUBLIC consumer helper; resolve the registry it created.
		var services = new ServiceCollection();
		_ = services.AddEventTypes<RegisteredTestEvent>();
		var registry = services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>();

		var serializer = new JsonEventSerializer(registry, options: null, allowAssemblyScan: false);
		var registeredName = registry.GetTypeName(typeof(RegisteredTestEvent));
		_ = registeredName.ShouldNotBeNull();

		// Act — secure default (scan OFF) must resolve the registered type via the registry.
		var resolved = serializer.ResolveType(registeredName);

		// Assert — registered ⇒ functional without any reflection scan (the c6wd6f fix).
		resolved.ShouldBe(typeof(RegisteredTestEvent));
	}

	[Fact]
	[RequiresDynamicCode("Resolution of an assembly-qualified name may touch reflection paths")]
	public void UnregisteredType_StillRejectedScanOff_EvenWithRegistry()
	{
		// Arrange — a populated registry (one type) but scan still OFF.
		var registry = new EventTypeRegistry();
		registry.Register(typeof(RegisteredTestEvent));
		var serializer = new JsonEventSerializer(registry, options: null, allowAssemblyScan: false);

		// typeof(string) is loaded/scannable but NOT registered — the wpynky non-vacuity anchor.
		// The registry must NOT weaken the secure default: unregistered still rejected.
		Should.Throw<UnknownEventTypeException>(
			() => serializer.ResolveType(typeof(string).AssemblyQualifiedName!));
	}

	[Fact]
	[RequiresDynamicCode("Explicit assembly-scan opt-in uses reflection")]
	public void ScanStillOptIn_ResolvesUnregistered_WhenExplicitlyEnabled()
	{
		// Arrange — registry present but the consumer also opts into the trusted-environment scan.
		var registry = new EventTypeRegistry();
		registry.Register(typeof(RegisteredTestEvent));
		var serializer = new JsonEventSerializer(registry, options: null, allowAssemblyScan: true);

		// Act — the opt-in escape hatch still resolves a real loaded but unregistered type.
		var resolved = serializer.ResolveType(typeof(string).AssemblyQualifiedName!);

		// Assert — scan opt-in is not regressed by the registry path.
		resolved.ShouldBe(typeof(string));
	}

	[Fact]
	public void AddEventTypesParams_RegistersAllTypes_ResolveScanOff()
	{
		// Arrange — the params Type[] overload registers multiple types in one call.
		var services = new ServiceCollection();
		_ = services.AddEventTypes(typeof(RegisteredTestEvent), typeof(SecondRegisteredTestEvent));
		var registry = services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>();
		var serializer = new JsonEventSerializer(registry, options: null, allowAssemblyScan: false);

		// Act & Assert — both registered types resolve scan-off.
		serializer.ResolveType(registry.GetTypeName(typeof(RegisteredTestEvent))!).ShouldBe(typeof(RegisteredTestEvent));
		serializer.ResolveType(registry.GetTypeName(typeof(SecondRegisteredTestEvent))!).ShouldBe(typeof(SecondRegisteredTestEvent));
	}

	[Fact]
	public void AddEventTypes_AccumulatesIntoSingleRegistry_AcrossCalls()
	{
		// Arrange — repeated AddEventTypes calls must accumulate into ONE allow-list (single registry).
		var services = new ServiceCollection();
		_ = services.AddEventTypes<RegisteredTestEvent>();
		_ = services.AddEventTypes<SecondRegisteredTestEvent>();
		var provider = services.BuildServiceProvider();

		// Assert — exactly one registry instance, holding both types.
		var registries = provider.GetServices<IEventTypeRegistry>().ToList();
		registries.Count.ShouldBe(1);
		registries[0].GetTypeName(typeof(RegisteredTestEvent)).ShouldNotBeNull();
		registries[0].GetTypeName(typeof(SecondRegisteredTestEvent)).ShouldNotBeNull();
	}

	[Fact]
	[RequiresUnreferencedCode("AddEventTypesFromAssembly scans the assembly via reflection")]
	public void AddEventTypesFromAssembly_RegistersDomainEvents_ResolveScanOff()
	{
		// Arrange — the bulk overload scans the consumer's OWN assembly (this test assembly) and
		// registers every concrete IDomainEvent type, so a sample can't silently miss one.
		var services = new ServiceCollection();
		_ = services.AddEventTypesFromAssembly(typeof(AssemblyScannedTestEvent).Assembly);
		var registry = services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>();
		var serializer = new JsonEventSerializer(registry, options: null, allowAssemblyScan: false);

		// Act & Assert — a concrete IDomainEvent from the scanned assembly resolves scan-off.
		var name = registry.GetTypeName(typeof(AssemblyScannedTestEvent));
		_ = name.ShouldNotBeNull();
		serializer.ResolveType(name).ShouldBe(typeof(AssemblyScannedTestEvent));
	}

	[Fact]
	[RequiresUnreferencedCode("AddEventTypesFromAssembly scans the assembly via reflection")]
	public void AddEventTypesFromAssembly_DoesNotRegisterNonDomainEvents_StillRejectedScanOff()
	{
		// Arrange — after the IDomainEvent-filtered scan, a NON-event type (typeof(string)) must NOT have
		// been registered: the filter is load-bearing, so the wpynky secure default holds (no over-register).
		var services = new ServiceCollection();
		_ = services.AddEventTypesFromAssembly(typeof(AssemblyScannedTestEvent).Assembly);
		var registry = services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>();
		var serializer = new JsonEventSerializer(registry, options: null, allowAssemblyScan: false);

		// Act & Assert — string is not an IDomainEvent ⇒ unregistered ⇒ still thrown scan-off.
		registry.GetTypeName(typeof(string)).ShouldBeNull();
		Should.Throw<UnknownEventTypeException>(
			() => serializer.ResolveType(typeof(string).AssemblyQualifiedName!));
	}
}
