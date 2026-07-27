// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Coverage for bead f1vlvs (decorator arm): the fail-closed <see cref="TenantScopedEventStore"/> decorator's
/// require-tenant behavior at the unit level. (The DI capability-guard is covered by
/// RowDiscriminatorTenantCapabilityGuardShould; this asserts the decorator's own runtime contract.)
/// </summary>
/// <remarks>
/// Every operation is asserted in BOTH arms — safety (no tenant → <see cref="TenantRequiredException"/> AND the
/// inner store is never touched) and liveness (a tenant → the call delegates to the inner and returns its
/// result). Unlike the saga/projection decorators (IsNullOrEmpty), the event-store decorator's guard is
/// IsNullOrWhiteSpace — a whitespace tenant is also refused; that difference is locked below.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TenantScopedEventStoreShould
{
    private readonly IEventStore _inner = A.Fake<IEventStore>();
    private readonly ITenantContext _tenant = A.Fake<ITenantContext>();

    private TenantScopedEventStore CreateSut() => new(_inner, _tenant);

    private void NoTenant() => A.CallTo(() => _tenant.TenantId).Returns(null);

    private void WithTenant(string tenantId) => A.CallTo(() => _tenant.TenantId).Returns(tenantId);

    // ---- LoadAsync (no fromVersion) ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnLoad_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.LoadAsync("agg-1", "Order", CancellationToken.None).AsTask());
        A.CallTo(() => _inner.LoadAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateLoadToInnerAndReturnItsResult_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        IReadOnlyList<StoredEvent> sentinel = new List<StoredEvent>();
        A.CallTo(() => _inner.LoadAsync("agg-1", "Order", A<CancellationToken>._))
            .Returns(new ValueTask<IReadOnlyList<StoredEvent>>(sentinel));
        var sut = CreateSut();

        var result = await sut.LoadAsync("agg-1", "Order", CancellationToken.None);

        result.ShouldBeSameAs(sentinel);
        A.CallTo(() => _inner.LoadAsync("agg-1", "Order", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    // ---- LoadAsync (fromVersion overload) ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnLoadFromVersion_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.LoadAsync("agg-1", "Order", 3L, CancellationToken.None).AsTask());
        A.CallTo(() => _inner.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateLoadFromVersionToInner_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        IReadOnlyList<StoredEvent> sentinel = new List<StoredEvent>();
        A.CallTo(() => _inner.LoadAsync("agg-1", "Order", 3L, A<CancellationToken>._))
            .Returns(new ValueTask<IReadOnlyList<StoredEvent>>(sentinel));
        var sut = CreateSut();

        var result = await sut.LoadAsync("agg-1", "Order", 3L, CancellationToken.None);

        result.ShouldBeSameAs(sentinel);
        A.CallTo(() => _inner.LoadAsync("agg-1", "Order", 3L, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    // ---- AppendAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnAppend_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.AppendAsync("agg-1", "Order", [], 0L, CancellationToken.None).AsTask());
        A.CallTo(() => _inner.AppendAsync(
            A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateAppendToInnerAndReturnItsResult_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        var expected = AppendResult.CreateSuccess(5, null);
        A.CallTo(() => _inner.AppendAsync("agg-1", "Order", A<IEnumerable<IDomainEvent>>._, 0L, A<CancellationToken>._))
            .Returns(new ValueTask<AppendResult>(expected));
        var sut = CreateSut();

        var result = await sut.AppendAsync("agg-1", "Order", [], 0L, CancellationToken.None);

        result.ShouldBe(expected);
        A.CallTo(() => _inner.AppendAsync("agg-1", "Order", A<IEnumerable<IDomainEvent>>._, 0L, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    // ---- Whitespace boundary (event-store guard is IsNullOrWhiteSpace) ----

    [Fact]
    public async Task ThrowOnLoad_WhenTheResolvedTenantIsWhitespace()
    {
        // (safety, boundary) the event-store decorator refuses a whitespace tenant id — distinct from the
        // saga/projection decorators, which use IsNullOrEmpty and would treat " " as present.
        WithTenant("   ");
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.LoadAsync("agg-1", "Order", CancellationToken.None).AsTask());
        A.CallTo(() => _inner.LoadAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
