// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Excalibur.Dispatch.Tests.Tenancy;

/// <summary>
/// Behavioral regression lock for the tenancy-W1 ambient context (beads <c>ir6me6</c> + <c>zee37q</c>).
/// Binds the emitted behavior of the real DI-registered <see cref="ITenantContext"/>/<see cref="ITenantResolver"/>
/// and the per-tenant <see cref="ITenantOptions{TOptions}"/>, not construction: the ambient tenant is
/// <c>AsyncLocal</c>-backed (flows across <c>await</c>), scope-only (structurally read-only), nests, and
/// restores on dispose; missing-tenant is empty; the resolver reads the message-carried tenant then the
/// configured default; per-tenant options resolve named options by the ambient tenant.
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
    public async Task ResolveTenantFromMessageItemsThenConfiguredDefault()
    {
        using var provider = new ServiceCollection()
            .AddTenantContext(options => options.DefaultTenantId = "fallback")
            .BuildServiceProvider();
        var resolver = provider.GetRequiredService<ITenantResolver>();

        var withItem = A.Fake<IMessageContext>();
        A.CallTo(() => withItem.Items).Returns(new Dictionary<string, object>
        {
            [TenantContextHolder.TenantIdItemKey] = "t",
        });
        (await resolver.ResolveAsync(withItem, CancellationToken.None)).ShouldBe("t");

        var withoutItem = A.Fake<IMessageContext>();
        A.CallTo(() => withoutItem.Items).Returns(new Dictionary<string, object>());
        (await resolver.ResolveAsync(withoutItem, CancellationToken.None)).ShouldBe("fallback");
    }

    [Fact]
    public async Task FailFast_WhenRequireTenantAndNoTenantResolvedAndNoDefault()
    {
        using var provider = new ServiceCollection()
            .AddTenantContext(options => options.RequireTenant = true) // no DefaultTenantId
            .BuildServiceProvider();
        var resolver = provider.GetRequiredService<ITenantResolver>();

        var withoutTenant = A.Fake<IMessageContext>();
        A.CallTo(() => withoutTenant.Items).Returns(new Dictionary<string, object>());

        // RequireTenant must REJECT an unscoped operation rather than silently proceed with false
        // isolation. RED on the pre-fix inert resolver (which returned null and never read RequireTenant).
        await Should.ThrowAsync<TenantRequiredException>(
            () => resolver.ResolveAsync(withoutTenant, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task NotFailFast_WhenRequireTenantButAnExplicitDefaultIsConfigured()
    {
        using var provider = new ServiceCollection()
            .AddTenantContext(options =>
            {
                options.RequireTenant = true;
                options.DefaultTenantId = "fallback"; // explicit opt-in default
            })
            .BuildServiceProvider();
        var resolver = provider.GetRequiredService<ITenantResolver>();

        var withoutTenant = A.Fake<IMessageContext>();
        A.CallTo(() => withoutTenant.Items).Returns(new Dictionary<string, object>());

        // An explicitly-configured default is the opt-in escape hatch — no throw.
        (await resolver.ResolveAsync(withoutTenant, CancellationToken.None)).ShouldBe("fallback");
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
