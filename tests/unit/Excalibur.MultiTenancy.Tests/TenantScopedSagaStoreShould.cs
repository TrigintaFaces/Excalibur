// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using static Excalibur.MultiTenancy.Tests.TestDoubles;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Coverage for bead b1c55r: the fail-closed <see cref="TenantScopedSagaStore"/> decorator.
/// </summary>
/// <remarks>
/// Each handling operation is asserted in BOTH arms — safety (no ambient tenant → <see cref="TenantRequiredException"/>
/// AND the inner store is never touched) and liveness (an ambient tenant → the call delegates to the inner and
/// returns its result). A decorator that threw on <em>every</em> path would pass the safety arm while inert; the
/// liveness arm is what fails against that.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TenantScopedSagaStoreShould
{
    private readonly ISagaStore _inner = A.Fake<ISagaStore>();
    private readonly ITenantContext _tenant = A.Fake<ITenantContext>();

    private TenantScopedSagaStore CreateSut() => new(_inner, _tenant);

    private void NoTenant() => A.CallTo(() => _tenant.TenantId).Returns(null);

    private void WithTenant(string tenantId) => A.CallTo(() => _tenant.TenantId).Returns(tenantId);

    // ---- LoadAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnLoad_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        // (safety) refuse the unscoped load...
        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None));

        // ...and prove the inner store was never reached (no false-isolation read escaped).
        A.CallTo(() => _inner.LoadAsync<TestSagaState>(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateLoadToInnerAndReturnItsResult_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        var id = Guid.NewGuid();
        var expected = new TestSagaState();
        A.CallTo(() => _inner.LoadAsync<TestSagaState>(id, A<CancellationToken>._)).Returns(expected);
        var sut = CreateSut();

        // (liveness) the permitted load still happens and returns the inner store's exact result.
        var result = await sut.LoadAsync<TestSagaState>(id, CancellationToken.None);

        result.ShouldBeSameAs(expected);
        A.CallTo(() => _inner.LoadAsync<TestSagaState>(id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    // ---- SaveAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnSave_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.SaveAsync(new TestSagaState(), CancellationToken.None));

        A.CallTo(() => _inner.SaveAsync(A<TestSagaState>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateSaveToInner_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        var state = new TestSagaState();
        var sut = CreateSut();

        await sut.SaveAsync(state, CancellationToken.None);

        A.CallTo(() => _inner.SaveAsync(state, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ThrowOnSave_WhenTheResolvedTenantIsEmpty()
    {
        // Boundary: the saga store's guard is IsNullOrEmpty — an empty tenant id is still "no tenant".
        WithTenant(string.Empty);
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.SaveAsync(new TestSagaState(), CancellationToken.None));
        A.CallTo(() => _inner.SaveAsync(A<TestSagaState>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    // ---- PurgeCompletedBeforeAsync: the global background drain is deliberately NOT tenant-gated ----

    [Fact]
    public async Task DelegatePurgeToInner_EvenWithoutATenant()
    {
        // The retention purge is a global drain over the shared table, not a tenant-facing op: it must run
        // without an ambient tenant. This pairs with the require-tenant arms above — it proves the decorator
        // does not OVER-apply the guard and stall a legitimate global operation.
        NoTenant();
        A.CallTo(() => _inner.PurgeCompletedBeforeAsync(A<DateTimeOffset>._, A<CancellationToken>._)).Returns(7);
        var sut = CreateSut();

        var purged = await sut.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        purged.ShouldBe(7);
        A.CallTo(() => _inner.PurgeCompletedBeforeAsync(A<DateTimeOffset>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
