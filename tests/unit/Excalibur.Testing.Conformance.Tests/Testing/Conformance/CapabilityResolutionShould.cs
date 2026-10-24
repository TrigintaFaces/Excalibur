// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using FakeItEasy;

namespace Excalibur.Tests.Testing.Conformance;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class CapabilityResolutionShould
{
    [Fact]
    public void RejectHiddenDirectCapability() =>
        Should.Throw<TestFixtureAssertionException>(() => Probe.Validate(new Facet(), null));

    [Fact]
    public void RejectWrongTypeLookupResult() =>
        Should.Throw<TestFixtureAssertionException>(() => Probe.Validate(new object(), new object()));

    [Fact]
    public void AcceptSeparateCapabilityAdapter()
    {
        var adapter = new Facet();
        Probe.Validate(new object(), adapter).ShouldBeSameAs(adapter);
    }

    [Fact]
    public void AllowDecliningAnInnerOnlyCapability() => Probe.Validate(new object(), null).ShouldBeNull();

    [Fact]
    public async Task RejectRawWrongTypeBeforeDeadLetterResolverCast()
    {
        var store = A.Fake<IDeadLetterStore>();
        A.CallTo(() => store.GetService(typeof(IDeadLetterStoreAdmin))).Returns(new object());
        await Should.ThrowAsync<TestFixtureAssertionException>(() => new DeadLetterProbe(store).GetCountAsync_EmptyStore_ShouldReturnZero());
    }

    [Fact]
    public async Task RejectDirectDeadLetterFacetHiddenByLookup()
    {
        var store = A.Fake<IDeadLetterStore>(options => options.Implements<IDeadLetterStoreAdmin>());
        A.CallTo(() => store.GetService(typeof(IDeadLetterStoreAdmin))).Returns((object?)null);
        await Should.ThrowAsync<TestFixtureAssertionException>(() => new DeadLetterProbe(store).GetCountAsync_EmptyStore_ShouldReturnZero());
    }

    [Fact]
    public async Task RejectDirectDeadLetterFacetHiddenByResolverOverride()
    {
        var store = A.Fake<IDeadLetterStore>(options => options.Implements<IDeadLetterStoreAdmin>());
        A.CallTo(() => store.GetService(typeof(IDeadLetterStoreAdmin))).Returns(store);
        await Should.ThrowAsync<TestFixtureAssertionException>(() => new DeadLetterProbe(store, hide: true).GetCountAsync_EmptyStore_ShouldReturnZero());
    }

    private sealed class Facet;

    private sealed class Probe : ConformanceTestKit
    {
        internal static Facet? Validate(object provider, object? resolved) => ValidateCapability<Facet>(provider, resolved);
    }

    private sealed class DeadLetterProbe(IDeadLetterStore store, bool hide = false) : DeadLetterStoreConformanceTestKit
    {
        protected override IDeadLetterStore CreateStore(ITenantContext tenantContext) => store;

        protected override IDeadLetterStoreAdmin? ResolveAdminFacet(IDeadLetterStore component) => hide ? null : base.ResolveAdminFacet(component);
    }
}
