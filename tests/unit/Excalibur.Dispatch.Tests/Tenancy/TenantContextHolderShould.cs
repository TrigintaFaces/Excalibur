// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Excalibur.Dispatch.Tests.Tenancy;

/// <summary>
/// Behavioral regression lock for the tenancy-W1 ambient context (beads <c>ir6me6</c> + <c>zee37q</c>).
/// Binds the emitted behavior of the real DI-registered <see cref="ITenantContext"/> and the per-tenant
/// <see cref="ITenantOptions{TOptions}"/>, not construction: the ambient tenant is <c>AsyncLocal</c>-backed
/// (flows across <c>await</c>), scope-only (structurally read-only), nests, and restores on dispose;
/// missing-tenant is empty; per-tenant options resolve named options by the ambient tenant.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantContextHolderShould
{
    [Fact]
    public async Task FlowAmbientTenantAcrossAwaitNestAndRestore()
    {
        using var provider = new ServiceCollection().AddTenantContext().BuildServiceProvider();
        var context = provider.GetRequiredService<ITenantContext>();

        context.HasTenant.ShouldBeFalse(); // no scope => no tenant

        using (TenantContextHolder.BeginScope("t"))
        {
            context.TenantId.ShouldBe("t");
            context.HasTenant.ShouldBeTrue();

            await Task.Yield(); // AsyncLocal must flow across the await continuation
            context.TenantId.ShouldBe("t");

            using (TenantContextHolder.BeginScope("inner"))
            {
                context.TenantId.ShouldBe("inner"); // nested scope
            }

            context.TenantId.ShouldBe("t"); // inner disposed => restored
        }

        context.HasTenant.ShouldBeFalse(); // outer disposed => restored to none
    }

    [Fact]
    public void EstablishAmbientTenantFromTheMessageIdentityFeature()
    {
        // LIVENESS ARM for the removal of the message-items tenant resolver: the ambient tenant must
        // still establish through the path that actually works. The tenant travels on the message's
        // identity feature (stamped by TenantIdentityMiddleware) and is read back with GetTenantId(),
        // which is what the host wraps in a scope. This is the documented replacement, so it is bound
        // here: if this breaks, the guidance in the multi-tenancy docs is wrong.
        using var provider = new ServiceCollection().AddTenantContext().BuildServiceProvider();
        var tenantContext = provider.GetRequiredService<ITenantContext>();

        var message = new MessageContext();
        message.GetOrCreateIdentityFeature().TenantId = "acme";

        tenantContext.HasTenant.ShouldBeFalse(); // nothing established yet

        using (TenantContextHolder.BeginScope(message.GetTenantId()))
        {
            tenantContext.TenantId.ShouldBe("acme"); // the message's tenant is now ambient
            tenantContext.HasTenant.ShouldBeTrue();
        }

        tenantContext.HasTenant.ShouldBeFalse(); // and is restored on dispose
    }

    [Fact]
    public void ResolvePerTenantNamedOptionsByAmbientTenant()
    {
        var services = new ServiceCollection();
        services.Configure<TenantScopedOptions>("a", options => options.Name = "a");
        services.Configure<TenantScopedOptions>("b", options => options.Name = "b");
        services.AddTenantContext();
        services.AddTenantOptions<TenantScopedOptions>();
        using var provider = services.BuildServiceProvider();
        var tenantOptions = provider.GetRequiredService<ITenantOptions<TenantScopedOptions>>();

        using (TenantContextHolder.BeginScope("a"))
        {
            tenantOptions.Value.Name.ShouldBe("a");
        }

        using (TenantContextHolder.BeginScope("b"))
        {
            tenantOptions.Value.Name.ShouldBe("b");
        }
    }

    private sealed class TenantScopedOptions
    {
        public string? Name { get; set; }
    }
}
