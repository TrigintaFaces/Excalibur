// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Pattern", "Regression")]
public sealed class KeyedStoreDecorationOwnershipShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveAsyncOnlyMixedTypeOwnershipAndTheCallersChosenCleanupMode(bool asyncCleanup)
    {
        var counters = new Counts();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        services.AddSingleton<IOwnedProbe, AsyncRaw>();
        services.AddKeyedSingleton<IOwnedProbe>("other", new Plain("other"));
        services.DecorateKeyedStores<IOwnedProbe>((inner, _) => new BorrowingLayer(inner, "A"));
        var provider = services.BuildServiceProvider();
        AsyncRaw? raw = null;
        Exception? primary = null;
        try
        {
            _ = provider.GetRequiredService<IOwnedProbe>();
            raw = counters.Async.Single();
            raw.EffectiveReleases.ShouldBe(0);
            if (asyncCleanup)
            {
                await provider.DisposeAsync();
                raw.DisposeInvocations.ShouldBeGreaterThan(0);
                raw.EffectiveReleases.ShouldBe(1);
            }
            else
            {
                Should.Throw<InvalidOperationException>(() => provider.Dispose());
                raw.DisposeInvocations.ShouldBe(0, "sync disposal must not block on an async-only owned inner");
                raw.EffectiveReleases.ShouldBe(0);
            }
        }
        catch (Exception error) { primary = error; }
        finally
        {
            // MSDI can mark its scope disposed before rejecting sync disposal. The test owns fallback teardown.
            primary = await FinishAsyncCleanupAsync(provider, counters.Async, primary);
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    [Fact]
    public async Task KeepExternalAsyncOnlyInstanceBorrowedDuringSynchronousProviderDisposal()
    {
        var counters = new Counts();
        var external = new AsyncRaw(counters);
        var services = new ServiceCollection();
        services.AddSingleton<IOwnedProbe>(external);
        services.DecorateKeyedStores<IOwnedProbe>((inner, _) => new BorrowingLayer(inner, "A"));
        var provider = services.BuildServiceProvider();
        Exception? primary = null;
        try
        {
            provider.GetRequiredService<IOwnedProbe>().RootId.ShouldBe(external.RootId);
            provider.Dispose();
            external.DisposeInvocations.ShouldBe(0);
            external.EffectiveReleases.ShouldBe(0);
        }
        catch (Exception error) { primary = error; }
        finally { primary = await FinishAsyncCleanupAsync(provider, counters.Async, primary); }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        external.EffectiveReleases.ShouldBe(1);
    }

    [Fact]
    public void KeepConstructedMixedRawOwnedWhenTheOuterDecoratorThrows()
    {
        var counters = new Counts();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        services.AddSingleton<IOwnedProbe, DisposableRaw>();
        services.AddKeyedSingleton<IOwnedProbe>("other", new Plain("other"));
        var expected = new IOException("outer decorator construction fault");
        services.DecorateKeyedStores<IOwnedProbe>((_, _) => throw expected);
        using var provider = services.BuildServiceProvider();
        Should.Throw<IOException>(() => provider.GetRequiredService<IOwnedProbe>()).ShouldBeSameAs(expected);
        var raw = counters.Sync.Single();
        raw.EffectiveReleases.ShouldBe(0);
        provider.Dispose();
        raw.DisposeInvocations.ShouldBeGreaterThan(0);
        raw.EffectiveReleases.ShouldBe(1);
    }

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void PreserveMixedRawTypeScopeAndTransientCleanupAcrossTwoBorrowingLayers(ServiceLifetime lifetime)
    {
        var counters = new Counts();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        ((IServiceCollection)services).Add(ServiceDescriptor.Describe(typeof(IOwnedProbe), typeof(DisposableRaw), lifetime));
        services.AddKeyedSingleton<IOwnedProbe>("other", new Plain("other"));
        services.DecorateKeyedStores<IOwnedProbe>((inner, _) => new BorrowingLayer(inner, "A"));
        services.DecorateKeyedStores<IOwnedProbe>((inner, _) => new BorrowingLayer(inner, "B"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IOwnedProbe>();
        var second = scope.ServiceProvider.GetRequiredService<IOwnedProbe>();
        first.Trace.ShouldBe("B(A(raw))");
        second.Trace.ShouldBe("B(A(raw))");
        if (lifetime == ServiceLifetime.Scoped) { second.ShouldBeSameAs(first); counters.Sync.Count.ShouldBe(1); }
        else { second.ShouldNotBeSameAs(first); second.RootId.ShouldNotBe(first.RootId); counters.Sync.Count.ShouldBe(2); }
        counters.Sync.ShouldAllBe(raw => raw.EffectiveReleases == 0);
        scope.Dispose();
        counters.Sync.ShouldAllBe(raw => raw.EffectiveReleases == 1);
        provider.Dispose();
        counters.Sync.ShouldAllBe(raw => raw.EffectiveReleases == 1);
    }

    [Fact]
    public void PassTheOriginalOpaqueKeyAndActualScopeToTheOriginalFactoryWithoutEagerActivation()
    {
        var services = new ServiceCollection();
        var key = new object();
        object? observedKey = null;
        IServiceProvider? observedProvider = null;
        var calls = 0;
        services.AddKeyedScoped<IOwnedProbe>(key, (sp, originalKey) =>
        {
            calls++;
            observedKey = originalKey;
            observedProvider = sp;
            return new Plain("raw");
        });
        services.DecorateKeyedStores<IOwnedProbe>((inner, _) => new BorrowingLayer(inner, "A"));
        calls.ShouldBe(0);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        calls.ShouldBe(0);
        using var scope = provider.CreateScope();
        var output = scope.ServiceProvider.GetRequiredKeyedService<IOwnedProbe>(key);
        output.Trace.ShouldBe("A(raw)");
        calls.ShouldBe(1);
        observedKey.ShouldBeSameAs(key, "a private holder key must never replace the original descriptor's key");
        observedProvider.ShouldBeSameAs(scope.ServiceProvider, "no alternate provider, proxy or root promotion");
        scope.ServiceProvider.GetKeyedServices<IOwnedProbe>(KeyedService.AnyKey).Single().ShouldBeSameAs(output);
    }

    [Fact]
    public void RefuseDisposableExternalInstancePassThroughBeforeDITransfersItsOwnership()
    {
        var counters = new Counts();
        using var external = new DisposableRaw(counters);
        var services = new ServiceCollection();
        services.AddSingleton<IOwnedProbe>(external);
        var calls = 0;
        services.DecorateKeyedStores<IOwnedProbe>((inner, _) => { calls++; return inner; });
        using var provider = services.BuildServiceProvider();
        var failure = Should.Throw<InvalidOperationException>(() => provider.GetService<IOwnedProbe>());
        failure.Message.ShouldNotBeNullOrWhiteSpace();
        calls.ShouldBe(1);
        provider.Dispose();
        external.DisposeInvocations.ShouldBe(0, "a refused pass-through must not have returned the external disposable for DI capture");
        external.EffectiveReleases.ShouldBe(0);
        external.Dispose();
        external.EffectiveReleases.ShouldBe(1);
    }

    [Fact]
    public async Task RefuseAsyncOnlyExternalInstancePassThroughBeforeAnyDiCapture()
    {
        var counters = new Counts();
        var external = new AsyncRaw(counters);
        var services = new ServiceCollection();
        services.AddSingleton<IOwnedProbe>(external);
        var calls = 0;
        services.DecorateKeyedStores<IOwnedProbe>((inner, _) => { calls++; return inner; });
        var provider = services.BuildServiceProvider();
        Exception? primary = null;
        try
        {
            Should.Throw<InvalidOperationException>(() => provider.GetService<IOwnedProbe>()).Message.ShouldNotBeNullOrWhiteSpace();
            calls.ShouldBe(1);
            provider.Dispose();
            external.DisposeInvocations.ShouldBe(0);
            external.EffectiveReleases.ShouldBe(0);
        }
        catch (Exception error) { primary = error; }
        finally { primary = await FinishAsyncCleanupAsync(provider, counters.Async, primary); }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        external.EffectiveReleases.ShouldBe(1);
    }

    private static async Task<Exception?> FinishAsyncCleanupAsync(ServiceProvider provider, IEnumerable<AsyncRaw> resources, Exception? primary)
    {
        try { await provider.DisposeAsync(); }
        catch (Exception error)
        {
            if (primary is null) { primary = error; }
            else { primary.Data["SecondaryProviderCleanup"] = error; }
        }
        foreach (var resource in resources)
        {
            try { if (resource.EffectiveReleases == 0) { await resource.DisposeAsync(); } }
            catch (Exception error)
            {
                if (primary is null) { primary = error; }
                else { primary.Data["SecondaryRawCleanup" + primary.Data.Count] = error; }
            }
        }
        return primary;
    }

    private interface IOwnedProbe
    {
        string Trace { get; }
        Guid RootId { get; }
    }

    private sealed class Counts
    {
        internal ConcurrentBag<DisposableRaw> Sync { get; } = new();
        internal ConcurrentBag<AsyncRaw> Async { get; } = new();
    }

    private class Plain(string name) : IOwnedProbe
    {
        public string Trace { get; } = name;
        public Guid RootId { get; } = Guid.NewGuid();
    }

    private sealed class BorrowingLayer(IOwnedProbe inner, string name) : IOwnedProbe
    {
        public string Trace { get; } = name + "(" + inner.Trace + ")";
        public Guid RootId => inner.RootId;
    }

    private sealed class DisposableRaw : Plain, IDisposable
    {
        private int _released;
        internal int DisposeInvocations;
        internal int EffectiveReleases;
        public DisposableRaw(Counts counts) : base("raw") => counts.Sync.Add(this);
        public void Dispose()
        {
            Interlocked.Increment(ref DisposeInvocations);
            if (Interlocked.Exchange(ref _released, 1) == 0) { Interlocked.Increment(ref EffectiveReleases); }
        }
    }

    private sealed class AsyncRaw : Plain, IAsyncDisposable
    {
        private int _released;
        internal int DisposeInvocations;
        internal int EffectiveReleases;
        public AsyncRaw(Counts counts) : base("raw") => counts.Async.Add(this);
        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref DisposeInvocations);
            if (Interlocked.Exchange(ref _released, 1) == 0) { Interlocked.Increment(ref EffectiveReleases); }
            return ValueTask.CompletedTask;
        }
    }
}
