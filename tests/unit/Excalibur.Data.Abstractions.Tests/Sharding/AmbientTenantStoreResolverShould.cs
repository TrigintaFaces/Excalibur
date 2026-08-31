// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Data.Tests.Sharding;

/// <summary>
/// Regression lock for the ambient tenant store bridge (bead <c>8hkfol</c>) — the thin ambient-context
/// bridge over the existing sharding seam (SA ruling (a): reuse <see cref="ITenantStoreResolver{TStore}"/>,
/// not a new strategy). Proves emitted behavior of the real DI-registered
/// <see cref="IAmbientTenantStoreResolver{TStore}"/>: <c>ResolveCurrent()</c> reads the ambient
/// <see cref="ITenantContext.TenantId"/> and routes to <em>that tenant's</em> store via the underlying
/// tenant-keyed resolver — so a change of ambient tenant changes the resolved store.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AmbientTenantStoreResolverShould
{
    [Fact]
    public void RouteResolveCurrentToTheAmbientTenantsStore()
    {
        var context = A.Fake<ITenantContext>();
        var storeResolver = A.Fake<ITenantStoreResolver<string>>();
        A.CallTo(() => storeResolver.Resolve("tenant-a")).Returns("store-a");
        A.CallTo(() => storeResolver.Resolve("tenant-b")).Returns("store-b");

        var ambient = BuildAmbientResolver(context, storeResolver);

        // The ambient tenant selects the store — changing it re-routes ResolveCurrent.
        A.CallTo(() => context.TenantId).Returns("tenant-a");
        ambient.ResolveCurrent().ShouldBe("store-a");

        A.CallTo(() => context.TenantId).Returns("tenant-b");
        ambient.ResolveCurrent().ShouldBe("store-b");
    }

    // An unresolved ambient tenant used to be folded onto the empty key, which the underlying
    // resolver treats as an unknown tenant and answers with the configured default store. The caller
    // then read and wrote whichever tenant that store belongs to, with nothing raised. This lock goes
    // RED if that fold is reintroduced: the fold makes the call succeed, and it reaches the resolver.
    [Fact]
    public void FailClosed_AndNeverReachTheStoreResolver_WhenThereIsNoAmbientTenant()
    {
        var context = A.Fake<ITenantContext>();
        A.CallTo(() => context.TenantId).Returns(null); // no ambient tenant
        var storeResolver = A.Fake<ITenantStoreResolver<string>>();

        // The store a reintroduced empty-key fold would silently hand back.
        A.CallTo(() => storeResolver.Resolve(string.Empty)).Returns("default-store");

        var ambient = BuildAmbientResolver(context, storeResolver);

        _ = Should.Throw<TenantRequiredException>(() => ambient.ResolveCurrent());

        // Refused before routing: no tenant term was substituted and no store was selected.
        A.CallTo(() => storeResolver.Resolve(A<string>._)).MustNotHaveHappened();
    }

    // "Belongs to no tenant" is a value a caller states, not a tenant left unset. The untenanted
    // context carries the reserved partition term, so it routes like any other key.
    [Fact]
    public void RouteToTheUntenantedPartition_WhenTheCallerIsExplicitlyUntenanted()
    {
        var storeResolver = A.Fake<ITenantStoreResolver<string>>();
        A.CallTo(() => storeResolver.Resolve(TenantScope.UntenantedSentinel)).Returns("untenanted-store");

        var ambient = BuildAmbientResolver(UntenantedContext.Instance, storeResolver);

        ambient.ResolveCurrent().ShouldBe("untenanted-store");
    }

    private static IAmbientTenantStoreResolver<string> BuildAmbientResolver(
        ITenantContext context, ITenantStoreResolver<string> storeResolver)
    {
        // The impl is internal — resolve through the real DI registration (open-generic transient).
        var provider = new ServiceCollection()
            .AddSingleton(context)
            .AddSingleton(storeResolver)
            .AddAmbientTenantStoreResolver()
            .BuildServiceProvider();
        return provider.GetRequiredService<IAmbientTenantStoreResolver<string>>();
    }
}
