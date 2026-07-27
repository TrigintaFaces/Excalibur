// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

using static Excalibur.MultiTenancy.Tests.TestDoubles;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Coverage for bead 1sgkuq: the <c>AddMultiTenancy</c> composition entry point (real-DI resolve) and the
/// <see cref="MultiTenancyOptions"/> validation seam.
/// </summary>
/// <remarks>
/// Every guard here is asserted in BOTH arms: the rejection (safety) is paired with the permitted-path
/// success (liveness), so a validator/guard that rejected <em>everything</em> — the cheapest way to look
/// safe — would fail the liveness arm.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AddMultiTenancyShould
{
    // ---- Real-DI resolve: AddMultiTenancy(RowDiscriminator) wraps a capable store with the fail-closed decorator ----

    [Fact]
    public void ResolveTheFailClosedDecorator_WrappingTheRegisteredStore_ForACapableEventStore()
    {
        var inner = A.Fake<IEventStore>();
        var services = new ServiceCollection();
        services.AddSingleton(inner);
        // The marker a tenant-aware provider's Add* registers to prove it honors the ambient tenant.
        // Emit the REAL tenant-scoping capability marker via the dep-gated seam (the old bare fake is
        // now structurally unimplementable). The seam registers a concrete NoopEventStore + the marker;
        // the AddSingleton<IEventStore> above is the interface the decorator wraps.
        services.AddTenantScopedStore<IEventStore, NoopEventStore>((_, _) => new NoopEventStore());

        services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // (liveness) the real container resolves the fail-closed decorator, not the bare store.
        var resolved = provider.GetRequiredService<IEventStore>();
        _ = resolved.ShouldBeOfType<TenantScopedEventStore>();
    }

    [Fact]
    public async Task ResolveADecoratorThatActuallyDelegatesToTheRegisteredInner_WhenATenantIsPresent()
    {
        var inner = A.Fake<IEventStore>();
        IReadOnlyList<StoredEvent> sentinel = new List<StoredEvent>();
        A.CallTo(() => inner.LoadAsync("agg-1", "Order", A<CancellationToken>._))
            .Returns(new ValueTask<IReadOnlyList<StoredEvent>>(sentinel));

        var services = new ServiceCollection();
        services.AddSingleton(inner);
        // Emit the REAL tenant-scoping capability marker via the dep-gated seam (the old bare fake is
        // now structurally unimplementable). The seam registers a concrete NoopEventStore + the marker;
        // the AddSingleton<IEventStore> above is the interface the decorator wraps.
        services.AddTenantScopedStore<IEventStore, NoopEventStore>((_, _) => new NoopEventStore());
        services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IEventStore>();

        // (liveness) with an ambient tenant, the resolved decorator forwards to the exact registered inner and
        // returns its result — proving it WRAPS the inner, not merely that a decorator type resolves.
        using (TenantContextHolder.BeginScope("tenant-a"))
        {
            var result = await store.LoadAsync("agg-1", "Order", CancellationToken.None);
            result.ShouldBeSameAs(sentinel);
        }

        A.CallTo(() => inner.LoadAsync("agg-1", "Order", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    // ---- k8gnb8: the capability gate rejects a tenancy-unaware IInboxStore (Cosmos/DynamoDb/InMemory) under RowDiscriminator ----

    [Fact]
    public void FailFastAtComposition_WhenAnInboxStoreLacksTheTenantScopingCapability()
    {
        var services = new ServiceCollection();
        // Models the CosmosDb/DynamoDb/InMemory inbox stores: a plain IInboxStore registration that emits NO
        // ITenantScopingCapability<IInboxStore> marker (those providers register via plain TryAddSingleton /
        // keyed singletons, never through the dep-gated AddTenantScopedStore seam).
        services.AddSingleton<IInboxStore>(A.Fake<IInboxStore>());

        // (safety) AddMultiTenancy(RowDiscriminator) rejects the tenancy-unaware inbox store at COMPOSITION time,
        // before any host can resolve it — so a multi-tenant host cannot silently dedup one tenant's message
        // against another tenant's inbox row.
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        // Non-vacuity: the throw is the capability gate for IInboxStore specifically, not an unrelated failure
        // (e.g. the "no store registered" guard). If the gate were deleted this assertion fails.
        ex.Message.ShouldContain(nameof(IInboxStore));
        ex.Message.ShouldContain("not tenant-scoping-capable");
    }

    [Fact]
    public void NotFailFast_WhenTheInboxStoreEmitsTheTenantScopingCapability()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IInboxStore>(A.Fake<IInboxStore>());
        // A tenant-aware inbox provider (e.g. SqlServer) registers its store through the dep-gated seam — the
        // ONLY path that emits ITenantScopingCapability<IInboxStore>. Emit it the same way here.
        services.AddTenantScopedStore<IInboxStore, NoopInboxStore>((_, _) => new NoopInboxStore());

        // (liveness) with the capability present the gate PASSES — proving it rejects the MISSING marker, not
        // every inbox store (a gate that rejected everything would be the cheapest way to look safe).
        Should.NotThrow(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        using var provider = services.BuildServiceProvider();
        // The inbox store still resolves — the gate gated, it did not block a conforming store from booting.
        _ = provider.GetRequiredService<IInboxStore>().ShouldNotBeNull();
    }

    // ---- Composition-time fail-fast on an unset/invalid strategy (mirrors the startup validator, earlier) ----

    [Fact]
    public void FailFastAtComposition_WhenStrategyIsUnspecified()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new NoopEventStore());

        // (safety) an unset strategy is rejected at AddMultiTenancy call time, before any store is wired.
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.Unspecified));
        ex.Message.ShouldContain(nameof(MultiTenancyOptions.Strategy));
    }

    [Fact]
    public void NotThrowAtComposition_ForAValidStrategy()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new NoopEventStore());
        // Emit the REAL tenant-scoping capability marker via the dep-gated seam (the old bare fake is
        // now structurally unimplementable). The seam registers a concrete NoopEventStore + the marker;
        // the AddSingleton<IEventStore> above is the interface the decorator wraps.
        services.AddTenantScopedStore<IEventStore, NoopEventStore>((_, _) => new NoopEventStore());

        // (liveness) a valid strategy composes without throwing — the guard rejects the bad value, not every value.
        Should.NotThrow(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));
    }

    // ---- MultiTenancyOptionsValidator: value-shape validation (safety + liveness) ----

    [Fact]
    public void RejectUnspecifiedStrategy_ViaTheOptionsValidator()
    {
        var validator = new MultiTenancyOptionsValidator();

        // (safety) the default (Unspecified) value fails validation.
        var result = validator.Validate(name: null, new MultiTenancyOptions());

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(MultiTenancyOptions.Strategy));
    }

    [Fact]
    public void RejectAnOutOfRangeStrategy_ViaTheOptionsValidator()
    {
        var validator = new MultiTenancyOptionsValidator();

        // (safety, boundary) an undefined enum value (e.g. a corrupt/cast int) is rejected.
        var result = validator.Validate(name: null, new MultiTenancyOptions { Strategy = (TenantIsolationStrategy)999 });

        result.Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(TenantIsolationStrategy.RowDiscriminator)]
    [InlineData(TenantIsolationStrategy.Sharding)]
    public void SucceedForAnExplicitlyChosenStrategy_ViaTheOptionsValidator(TenantIsolationStrategy strategy)
    {
        var validator = new MultiTenancyOptionsValidator();

        // (liveness) both valid strategies pass — the validator is not vacuously failing everything.
        var result = validator.Validate(name: null, new MultiTenancyOptions { Strategy = strategy });

        result.Succeeded.ShouldBeTrue();
    }

    // ---- The validator is honored by the Options pipeline (IValidateOptions integration) ----

    [Fact]
    public void ThrowOptionsValidationException_WhenTheOptionsPipelineMaterializesBadOptions()
    {
        // Mirror AddMultiTenancy's own options registration (Configure + ValidateOnStart + the validator),
        // then force materialization. Because AddMultiTenancy's composition guard rejects a bad strategy before
        // BuildServiceProvider, this proves the validator is correctly shaped as an IValidateOptions the Options
        // pipeline runs — the belt-and-suspenders startup arm behind that composition guard.
        var services = new ServiceCollection();
        _ = services.AddOptions<MultiTenancyOptions>()
            .Configure(static o => o.Strategy = TenantIsolationStrategy.Unspecified)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<MultiTenancyOptions>, MultiTenancyOptionsValidator>();

        using var provider = services.BuildServiceProvider();

        // (safety) materializing bad options runs the validator and throws.
        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<MultiTenancyOptions>>().Value);
    }

    [Fact]
    public void MaterializeValidOptions_WithoutThrowing_ThroughTheOptionsPipeline()
    {
        var services = new ServiceCollection();
        _ = services.AddOptions<MultiTenancyOptions>()
            .Configure(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<MultiTenancyOptions>, MultiTenancyOptionsValidator>();

        using var provider = services.BuildServiceProvider();

        // (liveness) valid options materialize and carry the chosen strategy — the pipeline is not rejecting everything.
        var options = provider.GetRequiredService<IOptions<MultiTenancyOptions>>().Value;
        options.Strategy.ShouldBe(TenantIsolationStrategy.RowDiscriminator);
    }
}
