// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateTheRawDiagnosticsResultAtTheFencingRefusalLookup(bool incompatible)
    {
        var store = A.Fake<IOutboxStore>();
        var fenced = A.Fake<IFencedOutboxStore>();
        var diagnostics = A.Fake<IFencedOutboxStoreDiagnostics>();
        A.CallTo(() => store.GetService(typeof(IFencedOutboxStore))).Returns(fenced);
        // Preparation needs a real diagnostics facet to mint and restore the arm's token.
        // The second lookup is the public arm's diagnostic check, not preparation.
        A.CallTo(() => store.GetService(typeof(IFencedOutboxStoreDiagnostics)))
            .ReturnsNextFromSequence(diagnostics, incompatible ? new object() : null);
        A.CallTo(() => diagnostics.GetFencingHighWaterAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<long?>(0));
        A.CallTo(() => diagnostics.ResetFencingHighWaterAsync(A<long>._, true, A<CancellationToken>._))
            .Returns(Task.CompletedTask);
        A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
            .Returns(ValueTask.CompletedTask);
        A.CallTo(() => fenced.MarkSentAsync(A<string>._, A<long>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied),
                new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused));
        var probe = new OutboxProbe(store);
        if (incompatible)
        {
            await Should.ThrowAsync<TestFixtureAssertionException>(probe.Fencing_Refusal_ShouldReportTheHighWaterMark);
        }
        else
        {
            await probe.Fencing_Refusal_ShouldReportTheHighWaterMark();
        }

        A.CallTo(() => store.GetService(typeof(IFencedOutboxStoreDiagnostics))).MustHaveHappenedTwiceExactly();
        A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fenced.MarkSentAsync(A<string>._, A<long>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => diagnostics.ResetFencingHighWaterAsync(A<long>._, true, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    private sealed class OutboxProbe(IOutboxStore store) : OutboxStoreConformanceTestKit
    {
        protected override Task<IOutboxStore> CreateStoreAsync() => Task.FromResult(store);

        protected override Task ResetDataAsync() => Task.CompletedTask;
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
