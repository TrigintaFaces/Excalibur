// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Tests.Diagnostics;

/// <summary>Public test-only declaration discovered through its actual assembly, never manually appended.</summary>
public interface IInventorySentinelOutboxStore
{
    bool Marker { get; }
}

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxCapabilityCatalogShould
{
    [Fact]
    public void Discover_and_reject_a_new_unclassified_interface_without_a_source_probe()
    {
        var entries = Build(FixturePolicy());
        entries.Select(static entry => entry.Type).ShouldContain(typeof(IInventorySentinelOutboxStore));
        var diagnostics = OutboxCapabilityCatalog.Analyze(entries, static _ => false, static _ => false);
        diagnostics.ShouldContain("Unclassified: " + typeof(IInventorySentinelOutboxStore).AssemblyQualifiedName);
    }

    [Fact]
    public void Report_a_dropped_new_interface_after_classification_then_clear_only_with_mediation()
    {
        var policy = FixturePolicy();
        policy.Add(typeof(IInventorySentinelOutboxStore), new("Payload-free test sentinel must mirror its actual inner.", true, true));
        var entries = Build(policy);
        var inner = HonestInner();
        var dropped = new SentinelMediator(inner, forward: false);
        OutboxCapabilityCatalog.Analyze(entries, type => Exposes(inner, type), type => Exposes(dropped, type))
            .ShouldContain("DroppedCapability: " + typeof(IInventorySentinelOutboxStore).AssemblyQualifiedName);

        var mediated = new SentinelMediator(inner, forward: true);
        OutboxCapabilityCatalog.Analyze(entries, type => Exposes(inner, type), type => Exposes(mediated, type)).ShouldBeEmpty();

        var absent = A.Fake<IOutboxStore>();
        A.CallTo(() => absent.GetService(A<Type>._)).Returns(null);
        var absentMediator = new SentinelMediator(absent, forward: true);
        OutboxCapabilityCatalog.Analyze(entries, type => Exposes(absent, type), type => Exposes(absentMediator, type)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Refuse_a_known_capability_without_exactly_one_behavior_classification(bool overlappingCategories)
    {
        var policy = FixturePolicy();
        policy.Add(typeof(IInventorySentinelOutboxStore), new("A reason alone cannot replace an actual behavior category.",
            true, MirrorsInner: overlappingCategories, LegacyUnconditional: overlappingCategories));
        var entries = Build(policy);
        var inner = HonestInner();
        var mediated = new SentinelMediator(inner, forward: true);
        // Both actually expose the interface: a forwarding/drop assertion alone would pass.
        Exposes(inner, typeof(IInventorySentinelOutboxStore)).ShouldBeTrue();
        Exposes(mediated, typeof(IInventorySentinelOutboxStore)).ShouldBeTrue();
        OutboxCapabilityCatalog.Analyze(entries, type => Exposes(inner, type), type => Exposes(mediated, type))
            .ShouldContain("InvalidCapabilityPolicy: " + typeof(IInventorySentinelOutboxStore).AssemblyQualifiedName);
    }
    private static Dictionary<Type, OutboxCapabilityPolicy> FixturePolicy()
    {
        var policy = OutboxCapabilityCatalog.ProductionPolicy.ToDictionary(static item => item.Key, static item => item.Value);
        policy.Add(typeof(Excalibur.Outbox.Tests.SqlServer.IOutboxTestDb),
            new("Existing public test DI database marker, not a store capability. Discovered from the injected test assembly."));
        return policy;
    }
    private static IReadOnlyList<OutboxCapabilityEntry> Build(IReadOnlyDictionary<Type, OutboxCapabilityPolicy> policy) =>
        OutboxCapabilityCatalog.Build([typeof(IOutboxStore).Assembly, typeof(ICloudNativeOutboxStoreBatch).Assembly,
            typeof(IInventorySentinelOutboxStore).Assembly], policy);

    private static bool Exposes(IOutboxStore store, Type type) => type.IsInstanceOfType(store) || store.GetService(type) is not null;

    private static IOutboxStore HonestInner()
    {
        var fake = A.Fake<IOutboxStore>(options => options.Implements<IInventorySentinelOutboxStore>());
        A.CallTo(() => fake.GetService(A<Type>._)).ReturnsLazily((Type type) => type.IsInstanceOfType(fake) ? fake : null);
        return fake;
    }

    private sealed class SentinelMediator(IOutboxStore inner, bool forward) : OutboxStoreDecorator(inner)
    {
        public override object? GetService(Type serviceType) => serviceType == typeof(IInventorySentinelOutboxStore)
            ? forward ? Inner.GetService(serviceType) : null
            : base.GetService(serviceType);
    }
}
