// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Tenancy;

// Independent regression lock (author != implementer) for the composition-time context<->mode
// consistency seam.
//
// THE DEFECT (cross-tenant data LOSS, reachable single-state, identical on SqlServer/Postgres/Oracle):
//   Tenant isolation is structural ONLY when the deployment-mode flag (TenantContextOptions.RequireTenant)
//   and the ambient ITenantContext AGREE. The multi-tenant composition sets both together, but the
//   framework registers the tenant context with TryAdd, so a consumer can register their OWN resolving
//   ITenantContext WITHOUT opting into multi-tenant mode, keep RequireTenant == false, and run the
//   single-tenant (pair) schema. The startup schema handshake validates mode<->schema only, NOT
//   context<->mode, so that misconfiguration is a FALSE GREEN: no tenant term is emitted, two tenants
//   collide on the (MessageId, HandlerType) pair key, and the second write is silently deduped/ACKed.
//
// THE SEAM UNDER TEST (a composition-time, fail-BEFORE-first-message guard):
//   RequireTenant == false REQUIRES the resolved ITenantContext to BE the framework single-tenant default.
//   Any OTHER (resolving) ITenantContext paired with RequireTenant == false must THROW at startup.
//
// This lock asserts the PROPERTY, not the mechanism (testing-patterns S3):
//   - it never names the internal SingleTenantContext / AmbientTenantContext types; it distinguishes the
//     "custom resolving context" case by registering a DIFFERENT ITenantContext implementation (below),
//     which by construction is not the framework default;
//   - it drives the REAL DI container and the REAL registration surface (a WIRE lock,
//     verify-against-real-infra DI clause) and triggers the same eager options validation the startup
//     guard runs, by resolving IOptions<TenantContextOptions>.Value (accessing .Value runs every
//     registered IValidateOptions<TenantContextOptions> and throws OptionsValidationException on failure;
//     ValidateOnStart forces this at host start, resolving .Value forces it here).
//
// SAFETY + LIVENESS (a fail-closed guard is trivially satisfied by one that rejects EVERYTHING):
//   - SAFETY  : the unsafe pairing (resolving context + single-tenant mode) is REJECTED at startup.
//   - LIVENESS: the two SANCTIONED pairings still start clean — (a) the framework single-tenant default
//               in single-tenant mode, and (b) a resolving context in multi-tenant mode.
//
// NON-VACUITY: the SAFETY arm is RED on the pre-fix (convention-only) code, where the unsafe pairing
// starts clean; it goes GREEN once the composition-time guard lands. The LIVENESS arms are GREEN in both.
// Regression lock for u97iah.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantContextConsistencyGuardShould
{
    [Fact]
    public void RejectStartup_WhenAResolvingContextIsRegisteredWithoutMultiTenantMode()
    {
        // SAFETY (RED pre-fix): a consumer registers their own resolving ITenantContext but never opts into
        // multi-tenant mode, so RequireTenant stays false. The framework default is TryAdd, so the custom
        // context wins. This is the exact silent-loss configuration; startup MUST fail closed.
        using var provider = new ServiceCollection()
            .AddOptions()                                        // deterministic options infra (pre- and post-fix)
            .AddSingleton<ITenantContext>(new ResolvingTenantContext("tenant-from-request"))
            .AddDefaultTenantContext()                           // TryAdd -> the resolving context above wins
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(
            () => TriggerStartupValidation(provider),
            "a resolving/custom ITenantContext paired with single-tenant mode (RequireTenant == false) is the " +
            "silent cross-tenant LOSS configuration: no tenant term is emitted and two tenants collide on the " +
            "pair key. Startup MUST fail closed and tell the consumer to call the multi-tenant composition or " +
            "use the framework single-tenant default. Before the guard lands this pairing starts clean — that " +
            "false green is the defect this lock exists to catch.");
    }

    [Fact]
    public void StartCleanly_WhenTheFrameworkSingleTenantDefaultIsUsed()
    {
        // LIVENESS (a): the sanctioned single-tenant path — the framework default context in single-tenant
        // mode. A guard that rejected everything would break this; it must start clean.
        using var provider = new ServiceCollection()
            .AddOptions()
            .AddDefaultTenantContext()                           // the framework single-tenant default; RequireTenant == false
            .BuildServiceProvider();

        Should.NotThrow(
            () => TriggerStartupValidation(provider),
            "the framework single-tenant default paired with single-tenant mode is the primary sanctioned " +
            "configuration and MUST start. If this throws, the guard is over-broad (rejecting the default), " +
            "which would brick every single-tenant host.");
    }

    [Fact]
    public void StartCleanly_WhenAResolvingContextIsPairedWithMultiTenantMode()
    {
        // LIVENESS (b): the sanctioned multi-tenant path — a resolving context in multi-tenant mode
        // (RequireTenant == true). AddTenantContext replaces the context with the ambient resolver and, with
        // RequireTenant == true, mode and context AGREE, so the guard must not fire.
        using var provider = new ServiceCollection()
            .AddTenantContext(o => o.RequireTenant = true)       // resolving ambient context + multi-tenant mode
            .AddDefaultTenantContext()                           // idempotent; the resolver still wins
            .BuildServiceProvider();

        Should.NotThrow(
            () => TriggerStartupValidation(provider),
            "a resolving context paired with multi-tenant mode is the sanctioned multi-tenant configuration " +
            "(mode and context agree). If this throws, the guard is rejecting a legitimate multi-tenant host.");
    }

    // Force the eager options validation the startup guard performs. Accessing IOptions<T>.Value runs every
    // registered IValidateOptions<TenantContextOptions>.Validate and throws OptionsValidationException on a
    // failure — the same check ValidateOnStart runs at host start.
    private static void TriggerStartupValidation(IServiceProvider provider)
        => _ = provider.GetRequiredService<IOptions<TenantContextOptions>>().Value;

    // A consumer's own resolving ITenantContext — implemented directly from the interface (not derived from
    // any framework base), so the lock binds the ITenantContext contract itself and, by construction, is NOT
    // the framework single-tenant default. Represents a per-request resolver.
    private sealed class ResolvingTenantContext(string tenantId) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;

        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }
}
