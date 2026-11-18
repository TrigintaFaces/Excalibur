// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Delivery.ScheduleDurability;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "SCHEDULING")]
public sealed class SelectedScheduleStoreDurabilityShould
{
    [Fact]
    public void RefuseALaterVolatileSelectionDespiteTheEarlierAttestation()
    {
        var services = NewRuntime();
        services.AddDurableScheduleStore<DeclaredDurableStore>();
        services.AddSingleton<IScheduleStore, InMemoryScheduleStore>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<InMemoryScheduleStore>();
        provider.GetService<IDurableScheduleStoreCapability>().ShouldNotBeNull();
        Should.Throw<OptionsValidationException>(() => Resolve(provider));
        provider.GetRequiredService<StoreLedger>().Created.ShouldBe(0);
    }

    [Fact]
    public void RefuseAnUnattestedDifferentInstanceOfTheSameImplementation()
    {
        var services = NewRuntime();
        services.AddDurableScheduleStore<DeclaredDurableStore>();
        services.AddSingleton<IScheduleStore>(p => new DeclaredDurableStore(p.GetRequiredService<StoreLedger>()));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<DeclaredDurableStore>();
        Should.Throw<OptionsValidationException>(() => Resolve(provider));
        provider.GetRequiredService<StoreLedger>().Created.ShouldBe(1);
    }

    [Fact]
    public void KeepAttestationIndependentForTwoProvidersBuiltFromOneCollection()
    {
        var services = NewRuntime();
        services.AddDurableScheduleStore<DeclaredDurableStore>();
        using var first = services.BuildServiceProvider();
        using var second = services.BuildServiceProvider();
        var firstStore = first.GetRequiredService<IScheduleStore>();
        Should.NotThrow(() => Resolve(first));
        var secondStore = second.GetRequiredService<IScheduleStore>();
        Should.NotThrow(() => Resolve(second));
        ReferenceEquals(firstStore, secondStore).ShouldBeFalse();
        first.GetServices<IValidateOptions<ScheduleDurabilityOptions>>()
            .Single(v => v is ScheduleDurabilityValidator)
            .Validate(null, new ScheduleDurabilityOptions()).Succeeded.ShouldBeTrue(
                "provider B must not overwrite provider A attestation; bypass the cached options value");
        first.GetRequiredService<StoreLedger>().Created.ShouldBe(1);
        second.GetRequiredService<StoreLedger>().Created.ShouldBe(1);
    }

    [Fact]
    public void PermitTheSelectedDurableRegistrationWithoutCreatingItsReplacedPredecessor()
    {
        var services = NewRuntime();
        services.AddDurableScheduleStore<DeclaredDurableStore>();
        services.AddDurableScheduleStore<DeclaredDurableProxy>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<DeclaredDurableProxy>();
        Should.NotThrow(() => Resolve(provider));
        provider.GetRequiredService<StoreLedger>().Created.ShouldBe(1);
    }

    [Fact]
    public void RefuseAnUnattestedProxyEvenWhenAnotherStoreWasAttested()
    {
        var services = NewRuntime();
        services.AddDurableScheduleStore<DeclaredDurableStore>();
        services.AddSingleton<IScheduleStore, DeclaredDurableProxy>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<DeclaredDurableProxy>();
        Should.Throw<OptionsValidationException>(() => Resolve(provider));
        provider.GetRequiredService<StoreLedger>().Created.ShouldBe(1);
    }

    [Fact]
    public void PermitAnExplicitlyAttestedSelectedProxy()
    {
        var services = NewRuntime();
        services.AddDurableScheduleStore<DeclaredDurableProxy>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<DeclaredDurableProxy>();
        Should.NotThrow(() => Resolve(provider));
        provider.GetRequiredService<StoreLedger>().Created.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequireExplicitVolatileOptIn(bool allowVolatile)
    {
        var services = NewRuntime();
        services.Configure<ScheduleDurabilityOptions>(o => o.AllowVolatileScheduleStore = allowVolatile);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<InMemoryScheduleStore>();
        if (allowVolatile)
        {
            Should.NotThrow(() => Resolve(provider));
        }
        else
        {
            Should.Throw<OptionsValidationException>(() => Resolve(provider));
        }
    }

    [Fact]
    public void RefuseAMissingStoreWithoutItsAttestation()
    {
        var services = new ServiceCollection();
        services.AddScheduleDurabilityGate();
        using var provider = services.BuildServiceProvider();
        provider.GetService<IScheduleStore>().ShouldBeNull();
        Should.Throw<OptionsValidationException>(() => Resolve(provider));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeTheSelectedStoreExactlyOnce(bool asynchronously)
    {
        var services = NewRuntime();
        services.AddDurableScheduleStore<DeclaredDurableStore>();
        var provider = services.BuildServiceProvider();
        var ledger = provider.GetRequiredService<StoreLedger>();
        try
        {
            Should.NotThrow(() => Resolve(provider));
            provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<DeclaredDurableStore>();
            ledger.Created.ShouldBe(1);
        }
        finally
        {
            if (asynchronously)
            {
                await provider.DisposeAsync();
            }
            else
            {
                provider.Dispose();
            }
        }

        ledger.SyncDisposals.ShouldBe(asynchronously ? 0 : 1);
        ledger.AsyncDisposals.ShouldBe(asynchronously ? 1 : 0);
    }

    private static ServiceCollection NewRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<StoreLedger>();
        services.AddDispatchScheduling();
        services.AddTimeAwareScheduling();
        return services;
    }

    private static ScheduleDurabilityOptions Resolve(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<ScheduleDurabilityOptions>>().Value;

    private sealed class StoreLedger
    {
        public StoreLedger()
        {
        }

        public int Created { get; set; }
        public int SyncDisposals { get; set; }
        public int AsyncDisposals { get; set; }
    }

    // The test declaration stands in for a persistence provider; it is not a SQL/restart proof.
    private sealed class DeclaredDurableStore(StoreLedger ledger) : IScheduleStore, IDisposable, IAsyncDisposable
    {
        private readonly StoreLedger _ledger = Register(ledger);

        public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<IScheduledMessage>>([]);

        public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() => _ledger.SyncDisposals++;
        public ValueTask DisposeAsync()
        {
            _ledger.AsyncDisposals++;
            return ValueTask.CompletedTask;
        }

        private static StoreLedger Register(StoreLedger ledger)
        {
            ledger.Created++;
            return ledger;
        }
    }

    // Owns its manually constructed inner; there is no second DI registration/ownership alias.
    private sealed class DeclaredDurableProxy(StoreLedger ledger) : IScheduleStore, IDisposable, IAsyncDisposable
    {
        private readonly DeclaredDurableStore _inner = new(ledger);
        public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
            _inner.GetAllAsync(cancellationToken);
        public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken) =>
            _inner.StoreAsync(message, cancellationToken);
        public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken) =>
            _inner.CompleteAsync(scheduleId, cancellationToken);
        public void Dispose() => _inner.Dispose();
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
