// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Pattern", "Regression")]
public sealed class KeyedStoreDecorationLineageShould
{
    private static readonly string[] ExpectedEnumerableTraces = ["A(first)", "A(last)"];

    [Fact]
    public void Baseline_ConstantInstanceRemainsExternallyOwned()
    {
        var counters = new Counters();
        using var external = new ResourceProbe(counters);
        var services = new ServiceCollection();
        services.AddSingleton<IProbe>(external);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IProbe>().ShouldBeSameAs(external);
        provider.Dispose();
        external.DisposeInvocations.ShouldBe(0);
        external.EffectiveReleases.ShouldBe(0);
        external.Dispose();
        external.EffectiveReleases.ShouldBe(1);
    }

    [Fact]
    public void Baseline_AliasFactoriesMayCaptureTheSameDisposableMoreThanOnce()
    {
        var counters = new Counters();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        services.AddSingleton<ResourceProbe>();
        services.AddKeyedSingleton<IProbe>("alias", (sp, _) => sp.GetRequiredService<ResourceProbe>());
        services.AddSingleton<IProbe>(sp => sp.GetRequiredService<ResourceProbe>());
        using var provider = services.BuildServiceProvider();
        var raw = provider.GetRequiredService<ResourceProbe>();
        provider.GetRequiredService<IProbe>().ShouldBeSameAs(raw);
        provider.GetRequiredKeyedService<IProbe>("alias").ShouldBeSameAs(raw);
        provider.Dispose();
        raw.DisposeInvocations.ShouldBeGreaterThan(1, "baseline DI can capture the same output through multiple factory descriptors");
        raw.EffectiveReleases.ShouldBe(1, "idempotent resource release, not exact Dispose call count, is the lifecycle invariant");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Baseline_MixedUnkeyedTypeOrFactoryRemainsDiOwnedWithANonOwningVisibleWrapper(bool implementationType)
    {
        var counters = new Counters();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        // There is deliberately NO independent concrete ResourceProbe registration.
        if (implementationType) { services.AddSingleton<IProbe, ResourceProbe>(); }
        else { services.AddSingleton<IProbe>(_ => new ResourceProbe(counters)); }
        services.AddKeyedSingleton<IProbe>("other", new RawProbe("other", counters));
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "non-owning", counters));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IProbe>().RootId.ShouldBe(counters.Resources.Single().RootId);
        var raw = counters.Resources.Single();
        raw.EffectiveReleases.ShouldBe(0);
        // Existing Rule1 leaves this unkeyed raw captured by DI. An all-target repair must not lose it.
        provider.Dispose();
        raw.DisposeInvocations.ShouldBeGreaterThan(0);
        raw.EffectiveReleases.ShouldBe(1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void PreserveExactlyOneOrderedLayerWhenOlderAliasFactoriesSeeNewerOutputs(int layers)
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Singleton);
        var names = new[] { "A", "B", "C" };
        for (var index = 0; index < layers; index++)
        {
            var name = names[index];
            services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, name, counters)).ShouldBeTrue();
        }
        counters.RawActivations.ShouldBe(0);
        counters.LayerActivations.ShouldBe(0);
        using var provider = services.BuildServiceProvider();
        counters.RawActivations.ShouldBe(0);
        // Resolve the oldest unkeyed forwarder first, through every currently decorated alias.
        var unkeyed = provider.GetRequiredService<IProbe>();
        var expected = layers == 2 ? "B(A(raw))" : "C(B(A(raw)))";
        unkeyed.Trace.ShouldBe(expected);
        provider.GetRequiredKeyedService<IProbe>("default").ShouldBeSameAs(unkeyed);
        provider.GetRequiredKeyedService<IProbe>("provider").ShouldBeSameAs(unkeyed);
        counters.RawActivations.ShouldBe(1);
        counters.LayerActivations.ShouldBe(layers);
    }

    [Fact]
    public void TreatRepeatedUseOfTheSameFactoryAsDistinctApplications()
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Singleton);
        Func<IProbe, IServiceProvider, IProbe> same = (inner, _) => new Layer(inner, "same", counters);
        services.DecorateKeyedStores(same).ShouldBeTrue();
        services.DecorateKeyedStores(same).ShouldBeTrue();
        using var provider = services.BuildServiceProvider();
        var value = provider.GetRequiredService<IProbe>();
        value.Trace.ShouldBe("same(same(raw))");
        provider.GetRequiredKeyedService<IProbe>("default").ShouldBeSameAs(value);
        counters.LayerActivations.ShouldBe(2);
    }

    [Theory]
    [InlineData("instance")]
    [InlineData("type")]
    [InlineData("factory")]
    public void DecorateIndependentUnkeyedTerminalsBesideProviderAndDefaultKeys(string shape)
    {
        var counters = new Counters();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        if (string.Equals(shape, "instance", StringComparison.Ordinal)) { services.AddSingleton<IProbe>(new RawProbe("independent", counters)); }
        else if (string.Equals(shape, "type", StringComparison.Ordinal)) { services.AddSingleton<IProbe, TypedProbe>(); }
        else { services.AddSingleton<IProbe>(_ => new RawProbe("independent", counters)); }
        services.AddKeyedSingleton<IProbe>("provider", (_, _) => new RawProbe("provider", counters));
        services.AddKeyedSingleton<IProbe>("default", (sp, _) => sp.GetRequiredKeyedService<IProbe>("provider"));
        var activationsBefore = counters.RawActivations;
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", counters)).ShouldBeTrue();
        counters.RawActivations.ShouldBe(activationsBefore);
        counters.LayerActivations.ShouldBe(0);
        using var provider = services.BuildServiceProvider();
        var independent = provider.GetRequiredService<IProbe>();
        independent.Trace.ShouldBe("A(independent)");
        var keyed = provider.GetRequiredKeyedService<IProbe>("provider");
        keyed.Trace.ShouldBe("A(provider)");
        keyed.ShouldBeSameAs(provider.GetRequiredKeyedService<IProbe>("default"));
        independent.ShouldNotBeSameAs(keyed);
        independent.RootId.ShouldNotBe(keyed.RootId);
        counters.LayerActivations.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservePlainUnkeyedAndDefaultOnlyRegistrationShapes(bool keyedDefault)
    {
        var counters = new Counters();
        var services = new ServiceCollection();
        var raw = new RawProbe("only", counters);
        if (keyedDefault) { services.AddKeyedSingleton<IProbe>("default", raw); }
        else { services.AddSingleton<IProbe>(raw); }
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", counters)).ShouldBeTrue();
        using var provider = services.BuildServiceProvider();
        var value = keyedDefault ? provider.GetRequiredKeyedService<IProbe>("default") : provider.GetRequiredService<IProbe>();
        value.Trace.ShouldBe("A(only)");
        value.RootId.ShouldBe(raw.RootId);
        counters.LayerActivations.ShouldBe(1);
    }

    [Fact]
    public void PreserveDescriptorPositionEnumerableOrderAndLastRegistrationWinner()
    {
        var counters = new Counters();
        var services = new ServiceCollection();
        services.AddSingleton<IProbe>(new RawProbe("first", counters));
        services.AddKeyedSingleton<IProbe>("provider", new RawProbe("provider", counters));
        services.AddSingleton<IProbe>(new RawProbe("last", counters));
        services.AddKeyedSingleton<IProbe>("default", (sp, _) => sp.GetRequiredKeyedService<IProbe>("provider"));
        var descriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IProbe)).Select(Describe).ToArray();
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", counters)).ShouldBeTrue();
        services.Where(descriptor => descriptor.ServiceType == typeof(IProbe)).Select(Describe).ToArray().ShouldBe(descriptors);
        using var provider = services.BuildServiceProvider();
        provider.GetServices<IProbe>().Select(value => value.Trace).ToArray().ShouldBe(ExpectedEnumerableTraces);
        provider.GetRequiredService<IProbe>().Trace.ShouldBe("A(last)");
        provider.GetRequiredKeyedService<IProbe>("provider").Trace.ShouldBe("A(provider)");
    }

    [Fact]
    public void KeepScopedAliasesIdenticalWithinScopeAndFreshAcrossScopes()
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Scoped);
        services.DecorateKeyedStores<IProbe>((inner, sp) => { counters.LayerProviders.Add(sp); return new Layer(inner, "A", counters); });
        services.DecorateKeyedStores<IProbe>((inner, sp) => { counters.LayerProviders.Add(sp); return new Layer(inner, "B", counters); });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstValue = first.ServiceProvider.GetRequiredService<IProbe>();
        first.ServiceProvider.GetRequiredKeyedService<IProbe>("default").ShouldBeSameAs(firstValue);
        first.ServiceProvider.GetRequiredKeyedService<IProbe>("provider").ShouldBeSameAs(firstValue);
        var secondValue = second.ServiceProvider.GetRequiredService<IProbe>();
        secondValue.Trace.ShouldBe("B(A(raw))");
        secondValue.ShouldNotBeSameAs(firstValue);
        secondValue.RootId.ShouldNotBe(firstValue.RootId);
        counters.RawActivations.ShouldBe(2);
        counters.LayerActivations.ShouldBe(4);
        counters.LayerProviders.Count(sp => ReferenceEquals(sp, first.ServiceProvider)).ShouldBe(2);
        counters.LayerProviders.Count(sp => ReferenceEquals(sp, second.ServiceProvider)).ShouldBe(2);
    }

    [Fact]
    public void NeverMemoizeTransientRawInputsOrCollapseIndependentDescriptorOutputs()
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Transient);
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", counters));
        using var provider = services.BuildServiceProvider();
        var values = new[] { provider.GetRequiredService<IProbe>(), provider.GetRequiredKeyedService<IProbe>("default"), provider.GetRequiredKeyedService<IProbe>("provider") };
        values.Select(value => value.RootId).Distinct().Count().ShouldBe(3);
        values.ShouldAllBe(value => string.Equals(value.Trace, "A(raw)", StringComparison.Ordinal));
        counters.RawActivations.ShouldBe(3);
        counters.LayerActivations.ShouldBe(3);
    }

    [Fact]
    public void DoNotCollapseSeparateDescriptorsThatShareOneRawInstance()
    {
        var counters = new Counters();
        var raw = new RawProbe("shared", counters);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProbe>("one", raw);
        services.AddKeyedSingleton<IProbe>("two", raw);
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", counters));
        using var provider = services.BuildServiceProvider();
        var one = provider.GetRequiredKeyedService<IProbe>("one");
        var two = provider.GetRequiredKeyedService<IProbe>("two");
        one.ShouldNotBeSameAs(two);
        one.RootId.ShouldBe(two.RootId);
        one.Trace.ShouldBe("A(shared)");
        two.Trace.ShouldBe("A(shared)");
        counters.LayerActivations.ShouldBe(2);
        provider.GetKeyedServices<IProbe>(KeyedService.AnyKey).ToArray().ShouldBe(new[] { one, two },
            "a disposal holder must not export a raw IProbe through AnyKey enumeration");
    }

    [Fact]
    public async Task ResolveConcurrentAliasesWithoutDuplicatingLayersOrDeadlockingActivation()
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Singleton);
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "A", counters));
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "B", counters));
        var provider = services.BuildServiceProvider();
        var workers = new List<Task<IProbe>>(16);
        Task<IProbe[]>? aggregate = null;
        Exception? primary = null;
        var cleanupErrors = new List<Exception>();
        try
        {
            for (var index = 0; index < 16; index++)
            {
                var route = index % 3;
                workers.Add(Task.Run(() => route switch
                {
                    0 => provider.GetRequiredService<IProbe>(),
                    1 => provider.GetRequiredKeyedService<IProbe>("default"),
                    _ => provider.GetRequiredKeyedService<IProbe>("provider")
                }));
            }
            aggregate = Task.WhenAll(workers);
            var values = await aggregate.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            values.ShouldAllBe(value => ReferenceEquals(value, values[0]));
            values[0].Trace.ShouldBe("B(A(raw))");
            counters.RawActivations.ShouldBe(1);
            counters.LayerActivations.ShouldBe(2);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            // Join every actually admitted DI worker, even if scheduling/observation/assertion failed.
            aggregate ??= Task.WhenAll(workers);
            try { await aggregate.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); }
            catch (Exception error)
            {
                if (!ReferenceEquals(error, primary)) { cleanupErrors.Add(error); }
            }
            if (aggregate.IsCompleted)
            {
                try { provider.Dispose(); }
                catch (Exception error) { cleanupErrors.Add(error); }
            }
            else
            {
                // Synchronous GetService cannot be force-canceled. Keep its provider alive until all finish.
                _ = DisposeProviderAfterWorkersAsync(provider, aggregate);
            }
        }
        foreach (var error in cleanupErrors)
        {
            if (primary is null) { primary = error; continue; }
            primary.Data["SecondaryConcurrentResolutionCleanup" + primary.Data.Count] = error;
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static async Task DisposeProviderAfterWorkersAsync(ServiceProvider provider, Task workers)
    {
        try { await workers.ConfigureAwait(false); }
        catch (Exception error) { ReportLateFailure("Deferred DI worker failure", error); }
        try { provider.Dispose(); }
        catch (Exception error) { ReportLateFailure("Deferred provider cleanup failure", error); }
    }

    private static void ReportLateFailure(string message, Exception error)
    {
        // The initiating timeout already failed the test; reporting must not create an unobserved fault.
        try { Trace.WriteLine(message + ": " + error); }
        catch (Exception) { /* A throwing global diagnostic listener cannot turn failed cleanup into a pass. */ }
    }

    [Fact]
    public void NeverPublishSuccessfulLineageForAThrowingFactoryAndAllowValidRetry()
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Singleton);
        var failure = new IOException("original decorator activation failure");
        var calls = 0;
        services.DecorateKeyedStores<IProbe>((inner, _) => ++calls == 1 ? throw failure : new Layer(inner, "A", counters));
        using var provider = services.BuildServiceProvider();
        Should.Throw<IOException>(() => provider.GetRequiredService<IProbe>()).ShouldBeSameAs(failure);
        var value = provider.GetRequiredService<IProbe>();
        value.Trace.ShouldBe("A(raw)");
        calls.ShouldBe(2);
        counters.RawActivations.ShouldBe(1);
        value.ShouldBeSameAs(provider.GetRequiredKeyedService<IProbe>("default"));
    }

    [Fact]
    public void ApplyAPassThroughFactoryOnceToTheReturnedAliasChain()
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Singleton);
        var calls = 0;
        services.DecorateKeyedStores<IProbe>((inner, _) => { calls++; return inner; });
        using var provider = services.BuildServiceProvider();
        var value = provider.GetRequiredService<IProbe>();
        value.Trace.ShouldBe("raw");
        value.ShouldBeSameAs(provider.GetRequiredKeyedService<IProbe>("default"));
        value.ShouldBeSameAs(provider.GetRequiredKeyedService<IProbe>("provider"));
        calls.ShouldBe(1);
    }

    [Fact]
    public void RejectNullDecoratorOutputWithoutCachingItAndAllowValidRetry()
    {
        var counters = new Counters();
        var services = Canonical(counters, ServiceLifetime.Singleton);
        var calls = 0;
        services.DecorateKeyedStores<IProbe>((inner, _) => ++calls == 1 ? null! : new Layer(inner, "A", counters));
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetKeyedService<IProbe>("provider")).Message.ShouldContain("null");
        provider.GetRequiredService<IProbe>().Trace.ShouldBe("A(raw)");
        calls.ShouldBe(2);
    }

    [Fact]
    public void LeaveReservedRawUndecoratedButDecorateItsEligibleOuterAlias()
    {
        var counters = new Counters();
        var raw = new RawProbe("raw-hot", counters);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProbe>("internal-raw", raw);
        services.AddKeyedSingleton<IProbe>("default", (sp, _) => sp.GetRequiredKeyedService<IProbe>("internal-raw"));
        services.AddSingleton<IProbe>(sp => sp.GetRequiredKeyedService<IProbe>("default"));
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "tenant", counters), new object[] { "internal-raw" });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IProbe>("internal-raw").ShouldBeSameAs(raw);
        var outer = provider.GetRequiredService<IProbe>();
        outer.Trace.ShouldBe("tenant(raw-hot)");
        outer.ShouldBeSameAs(provider.GetRequiredKeyedService<IProbe>("default"));
        counters.LayerActivations.ShouldBe(1);
    }

    [Fact]
    public void PreserveExternallyOwnedInnerWithAnActualNonOwningOutputPattern()
    {
        var counters = new Counters();
        using var external = new ResourceProbe(counters);
        var services = new ServiceCollection();
        services.AddSingleton<IProbe>(external);
        services.DecorateKeyedStores<IProbe>((inner, _) => new Layer(inner, "non-owning", counters));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IProbe>().RootId.ShouldBe(external.RootId);
        provider.Dispose();
        external.DisposeInvocations.ShouldBe(0);
        external.EffectiveReleases.ShouldBe(0);
        external.Dispose();
        external.EffectiveReleases.ShouldBe(1);
    }

    [Fact]
    public void ReleaseWrapperResourcesOnceAndIndependentlyOwnedRawAtScopeEnd()
    {
        var counters = new Counters();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        services.AddScoped<ResourceProbe>();
        services.AddKeyedScoped<IProbe>("provider", (sp, _) => sp.GetRequiredService<ResourceProbe>());
        services.AddKeyedScoped<IProbe>("default", (sp, _) => sp.GetRequiredKeyedService<IProbe>("provider"));
        services.AddScoped<IProbe>(sp => sp.GetRequiredKeyedService<IProbe>("default"));
        services.DecorateKeyedStores<IProbe>((inner, _) => new ResourceLayer(inner, counters, ownsInner: false));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var value = scope.ServiceProvider.GetRequiredService<IProbe>();
        scope.ServiceProvider.GetRequiredKeyedService<IProbe>("default").ShouldBeSameAs(value);
        scope.ServiceProvider.GetRequiredKeyedService<IProbe>("provider").ShouldBeSameAs(value);
        var raw = counters.Resources.Single();
        var wrapper = counters.ResourceLayers.Single();
        raw.EffectiveReleases.ShouldBe(0);
        wrapper.EffectiveReleases.ShouldBe(0);
        scope.Dispose();
        wrapper.DisposeInvocations.ShouldBeGreaterThan(0);
        wrapper.EffectiveReleases.ShouldBe(1);
        raw.EffectiveReleases.ShouldBe(1);
        provider.Dispose();
        raw.EffectiveReleases.ShouldBe(1);
        wrapper.EffectiveReleases.ShouldBe(1);
    }

    [Fact]
    public void ReleaseAnActivatorCreatedRawOnlyThroughAnExplicitOwningDecoratorPattern()
    {
        var counters = new Counters();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        services.AddSingleton<IProbe, ResourceProbe>();
        services.DecorateKeyedStores<IProbe>((inner, _) => new ResourceLayer(inner, counters, ownsInner: true));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IProbe>().Trace.ShouldBe("resource(resource-raw)");
        var raw = counters.Resources.Single();
        provider.Dispose();
        raw.EffectiveReleases.ShouldBe(1);
        counters.ResourceLayers.Single().EffectiveReleases.ShouldBe(1);
    }

    private static ServiceCollection Canonical(Counters counters, ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        ((IServiceCollection)services).Add(ServiceDescriptor.Describe(typeof(RawProbe), _ => new RawProbe("raw", counters), lifetime));
        ((IServiceCollection)services).Add(ServiceDescriptor.DescribeKeyed(typeof(IProbe), "provider", (sp, _) => sp.GetRequiredService<RawProbe>(), lifetime));
        ((IServiceCollection)services).Add(ServiceDescriptor.DescribeKeyed(typeof(IProbe), "default", (sp, _) => sp.GetRequiredKeyedService<IProbe>("provider"), lifetime));
        ((IServiceCollection)services).Add(ServiceDescriptor.Describe(typeof(IProbe), sp => sp.GetRequiredKeyedService<IProbe>("default"), lifetime));
        return services;
    }

    private static string Describe(ServiceDescriptor descriptor) => descriptor.ServiceType.Name + ":" +
        (descriptor.IsKeyedService ? descriptor.ServiceKey?.ToString() : "<unkeyed>") + ":" + descriptor.Lifetime;

    private interface IProbe
    {
        string Trace { get; }
        Guid RootId { get; }
    }

    private sealed class Counters
    {
        internal int RawActivations;
        internal int LayerActivations;
        internal ConcurrentBag<IServiceProvider> LayerProviders { get; } = new();
        internal ConcurrentBag<ResourceProbe> Resources { get; } = new();
        internal ConcurrentBag<ResourceLayer> ResourceLayers { get; } = new();
    }

    private class RawProbe : IProbe
    {
        internal RawProbe(string name, Counters counters)
        {
            Trace = name;
            Interlocked.Increment(ref counters.RawActivations);
        }
        public string Trace { get; }
        public Guid RootId { get; } = Guid.NewGuid();
    }

    private sealed class TypedProbe : RawProbe
    {
        public TypedProbe(Counters counters) : base("independent", counters) { }
    }

    private sealed class Layer : IProbe
    {
        internal Layer(IProbe inner, string name, Counters counters)
        {
            Trace = name + "(" + inner.Trace + ")";
            RootId = inner.RootId;
            Interlocked.Increment(ref counters.LayerActivations);
        }
        public string Trace { get; }
        public Guid RootId { get; }
    }

    private sealed class ResourceProbe : RawProbe, IDisposable
    {
        private int _released;
        internal int DisposeInvocations;
        internal int EffectiveReleases;
        public ResourceProbe(Counters counters) : base("resource-raw", counters) => counters.Resources.Add(this);
        public void Dispose()
        {
            Interlocked.Increment(ref DisposeInvocations);
            if (Interlocked.Exchange(ref _released, 1) == 0) { Interlocked.Increment(ref EffectiveReleases); }
        }
    }

    private sealed class ResourceLayer : IProbe, IDisposable
    {
        private readonly IProbe _inner;
        private readonly bool _ownsInner;
        private int _released;
        internal int DisposeInvocations;
        internal int EffectiveReleases;
        internal ResourceLayer(IProbe inner, Counters counters, bool ownsInner)
        {
            _inner = inner; _ownsInner = ownsInner;
            Trace = "resource(" + inner.Trace + ")";
            counters.ResourceLayers.Add(this);
        }
        public string Trace { get; }
        public Guid RootId => _inner.RootId;
        public void Dispose()
        {
            Interlocked.Increment(ref DisposeInvocations);
            if (Interlocked.Exchange(ref _released, 1) != 0) { return; }
            Interlocked.Increment(ref EffectiveReleases);
            if (_ownsInner && _inner is IDisposable owned) { owned.Dispose(); }
        }
    }
}
