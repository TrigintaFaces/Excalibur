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

    [Fact]
    public void RouteToEmptyTenant_WhenThereIsNoAmbientTenant()
    {
        var context = A.Fake<ITenantContext>();
        A.CallTo(() => context.TenantId).Returns(null); // no ambient tenant
        var storeResolver = A.Fake<ITenantStoreResolver<string>>();
        A.CallTo(() => storeResolver.Resolve(string.Empty)).Returns("default-store");

        var ambient = BuildAmbientResolver(context, storeResolver);

        // A null ambient tenant maps to the empty key (the underlying resolver owns default-shard policy).
        ambient.ResolveCurrent().ShouldBe("default-store");
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
