// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Pattern", "Regression")]
public sealed class KeyedStoreDecorationRetentionShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureOnlyDisposableTransientRawUntilProviderCleanup(bool disposableRaw)
    {
        var observation = new Observation();
        var services = new ServiceCollection();
        services.AddTransient<IProbe>(_ =>
        {
            IProbe raw = disposableRaw ? new OwnedProbe(observation) : new PlainProbe();
            observation.Raw = new WeakReference(raw);
            observation.Activations++;
            return raw;
        });
        services.AddKeyedSingleton<IProbe>("other", new PlainProbe());
        services.DecorateKeyedStores<IProbe>((inner, _) => new NonOwningOutput(inner));
        using var provider = services.BuildServiceProvider();
        var reference = ActivateOutsideTheCollectionFrame(provider, observation);
        CollectWithoutFinalizerWait();
        reference.IsAlive.ShouldBe(disposableRaw,
            "only owned disposable raw requires scope retention; an unconditional disposable holder leaks ordinary transient raw");
        GC.KeepAlive(provider);
        if (disposableRaw)
        {
            observation.EffectiveReleases.ShouldBe(0);
            provider.Dispose();
            observation.DisposeInvocations.ShouldBeGreaterThan(0);
            observation.EffectiveReleases.ShouldBe(1);
        }
        else
        {
            provider.Dispose();
            observation.DisposeInvocations.ShouldBe(0);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ActivateOutsideTheCollectionFrame(IServiceProvider provider, Observation observation)
    {
        _ = provider.GetRequiredService<IProbe>();
        observation.Activations.ShouldBe(1);
        return observation.Raw.ShouldNotBeNull();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectWithoutFinalizerWait()
    {
        // Probe objects have no finalizers. Keep global finalizer work out of this bounded reachability check.
        GC.Collect();
        GC.Collect();
    }

    private interface IProbe { }
    private sealed class PlainProbe : IProbe { }
    private sealed class NonOwningOutput(IProbe inner) : IProbe
    {
        // This is a real borrowing wrapper; the raw must live exactly as long as a live output needs it.
        private readonly IProbe _inner = inner;
        public override string ToString() => _inner.GetType().Name;
    }
    private sealed class Observation
    {
        internal WeakReference? Raw;
        internal int Activations;
        internal int DisposeInvocations;
        internal int EffectiveReleases;
    }
    private sealed class OwnedProbe(Observation observation) : IProbe, IDisposable
    {
        private int _released;
        public void Dispose()
        {
            Interlocked.Increment(ref observation.DisposeInvocations);
            if (Interlocked.Exchange(ref _released, 1) == 0) { Interlocked.Increment(ref observation.EffectiveReleases); }
        }
    }
}
