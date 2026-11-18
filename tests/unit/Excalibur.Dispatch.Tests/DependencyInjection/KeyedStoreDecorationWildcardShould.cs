// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Pattern", "Regression")]
public sealed class KeyedStoreDecorationWildcardShould
{
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void Baseline_AnyKeyFactoryReceivesActualRequestedKeysAndPreservesPerKeyLifetime(ServiceLifetime lifetime) =>
        CheckRequestedKeysAndLifetime(lifetime, decorate: false);

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void PreserveAnyKeyFactoryRequestedKeysAndPerKeyLifetimeAcrossTwoLayers(ServiceLifetime lifetime) =>
        CheckRequestedKeysAndLifetime(lifetime, decorate: true);

    [Fact]
    public void PreserveWildcardLayerOrderAndAliasIdentityWithoutExportingAnyRawBookkeeping()
    {
        var observations = new Observations();
        var services = Wildcard(observations, ServiceLifetime.Singleton);
        services.AddKeyedSingleton<IProbe>("alias-a", (sp, _) => sp.GetRequiredKeyedService<IProbe>("a"));
        services.AddKeyedSingleton<IProbe>("alias-b", (sp, _) => sp.GetRequiredKeyedService<IProbe>("b"));
        services.AddKeyedSingleton<IProbe>("default", (sp, _) => sp.GetRequiredKeyedService<IProbe>("a"));
        services.AddSingleton<IProbe>(sp => sp.GetRequiredKeyedService<IProbe>("default"));
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", observations));
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "B", observations));
        using var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredKeyedService<IProbe>("alias-a");
        var b = provider.GetRequiredKeyedService<IProbe>("alias-b");
        a.RequestedKey.ShouldBe("a");
        b.RequestedKey.ShouldBe("b");
        a.Trace.ShouldBe("B(A(a))");
        b.Trace.ShouldBe("B(A(b))");
        provider.GetRequiredKeyedService<IProbe>("a").ShouldBeSameAs(a);
        provider.GetRequiredKeyedService<IProbe>("b").ShouldBeSameAs(b);
        provider.GetRequiredKeyedService<IProbe>("default").ShouldBeSameAs(a);
        provider.GetRequiredService<IProbe>().ShouldBeSameAs(a);
        provider.GetKeyedServices<IProbe>(KeyedService.AnyKey).ToArray().ShouldBe(new[] { a, b, a },
            "enumeration includes explicit aliases in descriptor order, excludes wildcard registration itself and must not expose raw holders");
        observations.Activations.ShouldBe(2);
        observations.Layers.ShouldBe(4);
    }

    [Fact]
    public void KeepAnExplicitKeyAheadOfTheWildcardAndExcludeWildcardFromEnumeration()
    {
        var observations = new Observations();
        var services = Wildcard(observations, ServiceLifetime.Singleton);
        var explicitValue = new Raw("specific");
        services.AddKeyedSingleton<IProbe>("a", explicitValue);
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", observations));
        using var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredKeyedService<IProbe>("a");
        a.RequestedKey.ShouldBe("specific");
        a.RootId.ShouldBe(explicitValue.RootId);
        a.Trace.ShouldBe("A(specific)");
        var b = provider.GetRequiredKeyedService<IProbe>("b");
        b.RequestedKey.ShouldBe("b");
        b.Trace.ShouldBe("A(b)");
        observations.Activations.ShouldBe(1);
        provider.GetKeyedServices<IProbe>(KeyedService.AnyKey).Single().ShouldBeSameAs(a);
    }

    [Fact]
    public void KeepRepeatedWildcardTransientOutputsFreshAndNonDisposableRawCollectible()
    {
        var observations = new Observations();
        var services = Wildcard(observations, ServiceLifetime.Transient);
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", observations));
        using var provider = services.BuildServiceProvider();
        var ids = ResolveRepeatedOutsideCollectionFrame(provider);
        ids.Distinct().Count().ShouldBe(64);
        observations.Activations.ShouldBe(64);
        observations.Layers.ShouldBe(64);
        GC.Collect();
        GC.Collect();
        observations.RawReferences.ShouldAllBe(reference => !reference.IsAlive);
        GC.KeepAlive(provider);
        // Internal metadata-cache boundedness still requires source review of stable composite-key equality.
    }

    private static void CheckRequestedKeysAndLifetime(ServiceLifetime lifetime, bool decorate)
    {
        var observations = new Observations();
        var services = Wildcard(observations, lifetime);
        if (decorate)
        {
            services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", observations));
            services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "B", observations));
        }
        observations.Activations.ShouldBe(0);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var a = first.ServiceProvider.GetRequiredKeyedService<IProbe>("a");
        var equalA = first.ServiceProvider.GetRequiredKeyedService<IProbe>(new string('a', 1));
        var b = first.ServiceProvider.GetRequiredKeyedService<IProbe>("b");
        var otherScopeA = second.ServiceProvider.GetRequiredKeyedService<IProbe>("a");
        a.RequestedKey.ShouldBe("a");
        b.RequestedKey.ShouldBe("b");
        otherScopeA.RequestedKey.ShouldBe("a");
        a.RootId.ShouldNotBe(b.RootId);
        if (lifetime == ServiceLifetime.Transient) { equalA.ShouldNotBeSameAs(a); }
        else { equalA.ShouldBeSameAs(a, "equal requested keys must share the existing per-key cache"); }
        if (lifetime == ServiceLifetime.Singleton) { otherScopeA.ShouldBeSameAs(a); }
        else { otherScopeA.ShouldNotBeSameAs(a); otherScopeA.RootId.ShouldNotBe(a.RootId); }
        var expectedActivations = lifetime == ServiceLifetime.Singleton ? 2 : lifetime == ServiceLifetime.Scoped ? 3 : 4;
        observations.Activations.ShouldBe(expectedActivations);
        observations.Layers.ShouldBe(decorate ? expectedActivations * 2 : 0);
        new[] { a, equalA, b, otherScopeA }.ShouldAllBe(value =>
            string.Equals(value.Trace, decorate ? "B(A(" + value.RequestedKey + "))" : (value.RequestedKey == null ? null : value.RequestedKey.ToString()), StringComparison.Ordinal));
    }

    private static ServiceCollection Wildcard(Observations observations, ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        ((IServiceCollection)services).Add(ServiceDescriptor.DescribeKeyed(typeof(IProbe), KeyedService.AnyKey, (_, actualKey) =>
        {
            Interlocked.Increment(ref observations.Activations);
            var raw = new Raw(actualKey);
            observations.RawReferences.Enqueue(new WeakReference(raw));
            return raw;
        }, lifetime));
        return services;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Guid[] ResolveRepeatedOutsideCollectionFrame(IServiceProvider provider)
    {
        var ids = new Guid[64];
        for (var index = 0; index < ids.Length; index++)
        {
            var value = provider.GetRequiredKeyedService<IProbe>(new string('a', 1));
            value.RequestedKey.ShouldBe("a");
            value.Trace.ShouldBe("A(a)");
            ids[index] = value.RootId;
        }
        return ids;
    }

    private interface IProbe
    {
        object? RequestedKey { get; }
        Guid RootId { get; }
        string Trace { get; }
    }
    private sealed class Observations
    {
        internal int Activations;
        internal int Layers;
        internal ConcurrentQueue<WeakReference> RawReferences { get; } = new();
    }
    private sealed class Raw(object? actualKey) : IProbe
    {
        public object? RequestedKey { get; } = actualKey;
        public Guid RootId { get; } = Guid.NewGuid();
        public string Trace => RequestedKey?.ToString() ?? "null";
    }
    private sealed class Layer : IProbe
    {
        private readonly IProbe _inner;
        internal Layer(IProbe inner, string name, Observations observations)
        {
            _inner = inner;
            Trace = name + "(" + inner.Trace + ")";
            Interlocked.Increment(ref observations.Layers);
        }
        public object? RequestedKey => _inner.RequestedKey;
        public Guid RootId => _inner.RootId;
        public string Trace { get; }
    }
}
