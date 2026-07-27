// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using static Excalibur.MultiTenancy.Tests.TestDoubles;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Coverage for bead b1c55r: the fail-closed <see cref="TenantScopedProjectionStore{TProjection}"/> decorator.
/// </summary>
/// <remarks>
/// Every operation is asserted in BOTH arms — safety (no ambient tenant → <see cref="TenantRequiredException"/>
/// AND the inner store is never touched) and liveness (an ambient tenant → the call delegates to the inner and
/// returns its result). The liveness arm is what fails a decorator that is silently inert (throws on every path).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TenantScopedProjectionStoreShould
{
    private readonly IProjectionStore<TestProjection> _inner = A.Fake<IProjectionStore<TestProjection>>();
    private readonly ITenantContext _tenant = A.Fake<ITenantContext>();

    private TenantScopedProjectionStore<TestProjection> CreateSut() => new(_inner, _tenant);

    private void NoTenant() => A.CallTo(() => _tenant.TenantId).Returns(null);

    private void WithTenant(string tenantId) => A.CallTo(() => _tenant.TenantId).Returns(tenantId);

    // ---- GetByIdAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnGetById_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(() => sut.GetByIdAsync("id-1", CancellationToken.None));
        A.CallTo(() => _inner.GetByIdAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateGetByIdToInnerAndReturnItsResult_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        var expected = new TestProjection { Value = "v" };
        A.CallTo(() => _inner.GetByIdAsync("id-1", A<CancellationToken>._)).Returns(expected);
        var sut = CreateSut();

        var result = await sut.GetByIdAsync("id-1", CancellationToken.None);

        result.ShouldBeSameAs(expected);
        A.CallTo(() => _inner.GetByIdAsync("id-1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    // ---- UpsertAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnUpsert_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.UpsertAsync("id-1", new TestProjection(), CancellationToken.None));
        A.CallTo(() => _inner.UpsertAsync(A<string>._, A<TestProjection>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateUpsertToInner_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        var projection = new TestProjection { Value = "v" };
        var sut = CreateSut();

        await sut.UpsertAsync("id-1", projection, CancellationToken.None);

        A.CallTo(() => _inner.UpsertAsync("id-1", projection, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    // ---- DeleteAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnDelete_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(() => sut.DeleteAsync("id-1", CancellationToken.None));
        A.CallTo(() => _inner.DeleteAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateDeleteToInner_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        var sut = CreateSut();

        await sut.DeleteAsync("id-1", CancellationToken.None);

        A.CallTo(() => _inner.DeleteAsync("id-1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    // ---- QueryAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnQuery_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(
            () => sut.QueryAsync(filters: null, options: null, CancellationToken.None));
        A.CallTo(() => _inner.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateQueryToInnerAndReturnItsResult_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        IReadOnlyList<TestProjection> expected = new List<TestProjection> { new() { Value = "v" } };
        A.CallTo(() => _inner.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, A<CancellationToken>._))
            .Returns(expected);
        var sut = CreateSut();

        var result = await sut.QueryAsync(filters: null, options: null, CancellationToken.None);

        result.ShouldBeSameAs(expected);
    }

    // ---- CountAsync ----

    [Fact]
    public async Task ThrowAndNotTouchInner_OnCount_WhenNoTenantIsResolved()
    {
        NoTenant();
        var sut = CreateSut();

        _ = await Should.ThrowAsync<TenantRequiredException>(() => sut.CountAsync(filters: null, CancellationToken.None));
        A.CallTo(() => _inner.CountAsync(A<IDictionary<string, object>?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DelegateCountToInnerAndReturnItsResult_WhenATenantIsResolved()
    {
        WithTenant("tenant-a");
        A.CallTo(() => _inner.CountAsync(A<IDictionary<string, object>?>._, A<CancellationToken>._)).Returns(42L);
        var sut = CreateSut();

        var result = await sut.CountAsync(filters: null, CancellationToken.None);

        result.ShouldBe(42L);
    }
}
