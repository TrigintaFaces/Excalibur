// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Decorates non-reserved store registrations without changing their position, key or lifetime.
/// Resolved forwarding aliases inherit layer lineage and therefore do not apply the same layer twice.
/// </summary>
/// <remarks>
/// Original activation and decoration remain deferred and use the same service provider. A layer is one
/// application of <see cref="DecorateKeyedStores{TService}"/>, not a decorator type: applying the same factory
/// twice deliberately creates two distinct layers. Lineage tracks returned object identity, never a cache
/// from raw stores to wrappers, so independent registrations and scoped/transient activation stay distinct.
/// </remarks>
public static class KeyedStoreServiceCollectionExtensions
{
    // Values contain only weak opaque-token references, never outputs, raw inputs, delegates, scopes or providers.
    private static readonly ConditionalWeakTable<object, DecorationLineage> OutputLineages = new();

    /// <summary>Wraps every non-reserved registration of the store contract, including independent unkeyed stores.</summary>
    /// <typeparam name="TService">The store contract being decorated.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="decoratorFactory">Builds a non-null decorator from the resolved original and the same provider.</param>
    /// <param name="reservedKeys">
    /// Internal implementation keys excluded from this layer. Eligible aliases to a reserved raw store are
    /// still decorated; reserving a key does not remove wrappers returned by its own factory.
    /// </param>
    /// <returns>True when at least one eligible descriptor was replaced; false when none exists.</returns>
    /// <remarks>
    /// <para>
    /// Type/factory-created disposable originals remain owned by a private same-lifetime holder, captured by DI
    /// before decoration. Instance originals remain borrowed. Decorators must honor that inner-ownership policy:
    /// do not dispose a borrowed original or independently dispose an original whose holder already owns it.
    /// Disposal must be idempotent because ordinary DI alias factories can capture the same returned object
    /// more than once. Async disposal prefers IAsyncDisposable; owned async-only values require asynchronous
    /// scope/provider disposal. No synchronous blocking on asynchronous disposal is performed.
    /// </para>
    /// <para>
    /// A factory must preserve the supplied inner's wrapping semantics; this helper cannot prove an arbitrary
    /// factory secure when it discards its inner or returns an unrelated shared object. A null result is refused.
    /// Returning a borrowed disposable instance unchanged is also refused, before publishing lineage or returning
    /// it to DI, because converting that external instance into a factory result would transfer disposal ownership.
    /// Non-disposable pass-throughs and normal factory aliases remain supported.
    /// </para>
    /// <para>
    /// Explicit and wildcard AnyKey factories receive their actual requested service key. Private ownership
    /// routing preserves each descriptor's original singleton, scoped or transient behavior independently per key.
    /// </para>
    /// <para>
    /// Keyed implementation types use constructor planning for valid resolvable graphs without activating
    /// candidate dependencies. Unselected dependency graphs are not recursively container-validated: a malformed
    /// unselected graph may produce a different diagnostic or allow a valid shorter constructor to be selected.
    /// A failure from a selected dependency or constructor propagates; it never triggers another constructor
    /// or an undecorated fallback. Unkeyed types keep their existing ActivatorUtilities activation behavior.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The collection or decorator factory is null.</exception>
    /// <exception cref="InvalidOperationException">An activation/decorator returns null, or an external disposable pass-through would transfer ownership.</exception>
    public static bool DecorateKeyedStores<TService>(
        this IServiceCollection services,
        Func<TService, IServiceProvider, TService> decoratorFactory,
        IReadOnlyCollection<object>? reservedKeys = null)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decoratorFactory);

        var token = new object();
        var replacements = new List<(int Index, ServiceDescriptor Public, ServiceLifetime Lifetime)>();
        for (var index = 0; index < services.Count; index++)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType != typeof(TService) ||
                (descriptor.IsKeyedService && descriptor.ServiceKey is { } key && reservedKeys?.Contains(key) == true))
            {
                continue;
            }
            var activation = new ActivationSpec<TService>(BuildOriginalFactory<TService>(descriptor),
                externalInstance: descriptor.GetImplementationInstance() is TService);
            var lifetime = descriptor.Lifetime;
            TService Resolve(IServiceProvider sp, object? requestedKey)
            {
                // The composite key keeps descriptor identity and the actual public requested key.
                // DI itself owns/caches these private holders under the original lifetime; no raw cache.
                var holder = ResolveHolder(sp, activation, requestedKey, lifetime);
                return ApplyLayer(holder.Value, holder.ExternalInstance, sp, decoratorFactory, token);
            }
            var replacement = descriptor.IsKeyedService
                ? ServiceDescriptor.DescribeKeyed(typeof(TService), descriptor.ServiceKey, (sp, key) => Resolve(sp, key), lifetime)
                : ServiceDescriptor.Describe(typeof(TService), sp => Resolve(sp, requestedKey: null), lifetime);
            replacements.Add((index, replacement, lifetime));
        }

        // Preflight all activations before changing any public descriptor; no original/customer code runs.
        foreach (var replacement in replacements) { services[replacement.Index] = replacement.Public; }
        if (replacements.Any(static item => item.Lifetime == ServiceLifetime.Singleton))
        {
            EnsureHolderRouter<TService, SingletonLifetime>(services, ServiceLifetime.Singleton);
        }
        if (replacements.Any(static item => item.Lifetime == ServiceLifetime.Scoped))
        {
            EnsureHolderRouter<TService, ScopedLifetime>(services, ServiceLifetime.Scoped);
        }
        if (replacements.Any(static item => item.Lifetime == ServiceLifetime.Transient))
        {
            EnsureHolderRouter<TService, TransientLifetime>(services, ServiceLifetime.Transient);
        }
        return replacements.Count != 0;
    }

    private static void EnsureHolderRouter<TService, TLifetime>(IServiceCollection services, ServiceLifetime lifetime)
        where TService : class
    {
        // Stateless routing is identical across helper applications. Private service types differ by
        // lifetime; a wildcard singleton holder must never become a scoped/transient holder by accident.
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(OriginalHolder<TService, TLifetime>) &&
            descriptor.IsKeyedService && ReferenceEquals(descriptor.ServiceKey, KeyedService.AnyKey))) { return; }
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(OriginalHolder<TService, TLifetime>), KeyedService.AnyKey,
            static (sp, key) => CreateHolder<TService, TLifetime>(sp, key), lifetime));
    }

    private static HolderValue<TService> ResolveHolder<TService>(IServiceProvider provider, ActivationSpec<TService> activation,
        object? requestedKey, ServiceLifetime lifetime) where TService : class
    {
        var key = new ActivationKey<TService>(activation, requestedKey);
        return lifetime switch
        {
            ServiceLifetime.Singleton => provider.GetRequiredKeyedService<OriginalHolder<TService, SingletonLifetime>>(key),
            ServiceLifetime.Scoped => provider.GetRequiredKeyedService<OriginalHolder<TService, ScopedLifetime>>(key),
            ServiceLifetime.Transient => provider.GetRequiredKeyedService<OriginalHolder<TService, TransientLifetime>>(key),
            _ => throw new InvalidOperationException("The store descriptor has an unsupported service lifetime."),
        };
    }

    private static TService ApplyLayer<TService>(TService inner, bool externalInstance, IServiceProvider provider,
        Func<TService, IServiceProvider, TService> decoratorFactory, object token) where TService : class
    {
        var inherited = ReadLineage(inner);
        if (inherited.Contains(token))
        {
            RefuseExternalOwnershipTransfer(externalInstance, inner, inner);
            return inner;
        }

        // No original activation or customer callback executes under a lineage lock.
        var output = decoratorFactory(inner, provider)
            ?? throw new InvalidOperationException("The store decorator returned null; a non-null store is required.");
        RefuseExternalOwnershipTransfer(externalInstance, inner, output);
        inherited.UnionWith(ReadLineage(inner));
        PublishLineage(output, inherited, token);
        return output;
    }

    private static void RefuseExternalOwnershipTransfer<TService>(bool externalInstance, TService inner, TService output)
        where TService : class
    {
        if (externalInstance && ReferenceEquals(output, inner) &&
            (output is IDisposable || output is IAsyncDisposable))
        {
            throw new InvalidOperationException("A store decorator cannot return a borrowed disposable instance unchanged; return a non-owning wrapper instead.");
        }
    }

    private static HashSet<object> ReadLineage(object output)
    {
        if (!OutputLineages.TryGetValue(output, out var lineage)) { return new HashSet<object>(ReferenceEqualityComparer.Instance); }
        lock (lineage.Gate) { return ReadLiveTokens(lineage); }
    }

    private static void PublishLineage(object output, HashSet<object> inherited, object token)
    {
        var lineage = OutputLineages.GetValue(output, static _ => new DecorationLineage());
        lock (lineage.Gate)
        {
            // Copy/merge then publish: no lost concurrent output tags or partially published token set.
            var merged = ReadLiveTokens(lineage);
            merged.UnionWith(inherited);
            merged.Add(token);
            var references = new List<WeakReference<object>>(merged.Count);
            foreach (var liveToken in merged) { references.Add(new WeakReference<object>(liveToken)); }
            lineage.Tokens = references;
        }
    }

    // Caller holds the lineage gate. Strong snapshot locals protect live tokens during this operation;
    // only weak references remain globally, so an external no-op output cannot retain dead applications.
    private static HashSet<object> ReadLiveTokens(DecorationLineage lineage)
    {
        var live = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var retained = new List<WeakReference<object>>();
        foreach (var reference in lineage.Tokens)
        {
            if (reference.TryGetTarget(out var token)) { live.Add(token); retained.Add(reference); }
        }
        if (retained.Count != lineage.Tokens.Count) { lineage.Tokens = retained; }
        return live;
    }

    private static OriginalHolder<TService, TLifetime> CreateHolder<TService, TLifetime>(IServiceProvider provider, object? key)
        where TService : class
    {
        if (key is not ActivationKey<TService> activationKey)
        {
            throw new InvalidOperationException("A private store ownership holder requires its opaque activation key.");
        }
        var activation = activationKey.Activation;
        var value = activation.Factory(provider, activationKey.RequestedKey)
            ?? throw new InvalidOperationException("The original store activation returned null; a non-null store is required.");
        return !activation.ExternalInstance && (value is IDisposable || value is IAsyncDisposable)
            ? new OwnedOriginalHolder<TService, TLifetime>(value)
            : new OriginalHolder<TService, TLifetime>(value, activation.ExternalInstance);
    }

    private static Func<IServiceProvider, object?, TService> BuildOriginalFactory<TService>(ServiceDescriptor descriptor) where TService : class
    {
        var implementationType = descriptor.GetImplementationType();
        if (implementationType is not null)
        {
            return descriptor.IsKeyedService
                ? (sp, key) => (TService)KeyedStoreTypeActivator.Create(sp, typeof(TService), implementationType, key)
                : (sp, _) => (TService)ActivatorUtilities.CreateInstance(sp, implementationType);
        }
        if (descriptor.GetImplementationFactoryWithRequestedKey() is { } factory) { return (sp, key) => (TService)factory(sp, key); }
        if (descriptor.GetImplementationInstance() is TService instance) { return (_, _) => instance; }
        throw new InvalidOperationException("The store descriptor has no supported type, factory or instance activation.");
    }

    private sealed class ActivationSpec<TService>(Func<IServiceProvider, object?, TService> factory, bool externalInstance)
        where TService : class
    {
        internal Func<IServiceProvider, object?, TService> Factory { get; } = factory;
        internal bool ExternalInstance { get; } = externalInstance;
    }

    private sealed class ActivationKey<TService>(ActivationSpec<TService> activation, object? requestedKey) : IEquatable<ActivationKey<TService>>
        where TService : class
    {
        internal ActivationSpec<TService> Activation { get; } = activation;
        internal object? RequestedKey { get; } = requestedKey;
        public bool Equals(ActivationKey<TService>? other) => other is not null &&
            ReferenceEquals(Activation, other.Activation) && Equals(RequestedKey, other.RequestedKey);
        public override bool Equals(object? obj) => obj is ActivationKey<TService> other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Activation), RequestedKey);
    }

    private readonly struct SingletonLifetime { }
    private readonly struct ScopedLifetime { }
    private readonly struct TransientLifetime { }

    private sealed class DecorationLineage
    {
        internal object Gate { get; } = new();
        internal List<WeakReference<object>> Tokens { get; set; } = new();
    }

    // Deliberately non-disposable: otherwise DI would retain every non-disposable transient original
    // until provider disposal, introducing memory growth where the original transient was not captured.
    private class HolderValue<TService>(TService value, bool externalInstance) where TService : class
    {
        internal TService Value { get; } = value;
        internal bool ExternalInstance { get; } = externalInstance;
    }

    private class OriginalHolder<TService, TLifetime>(TService value, bool externalInstance) : HolderValue<TService>(value, externalInstance)
        where TService : class { }

    private sealed class OwnedOriginalHolder<TService, TLifetime>(TService value) : OriginalHolder<TService, TLifetime>(value, externalInstance: false), IDisposable, IAsyncDisposable
        where TService : class
    {
        private int _disposed;
        public void Dispose()
        {
            if (Volatile.Read(ref _disposed) != 0) { return; }
            if (Value is not IDisposable disposable)
            {
                throw new InvalidOperationException("The owned store implements only IAsyncDisposable; dispose its scope or provider asynchronously.");
            }
            if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
            try { disposable.Dispose(); }
            finally { GC.SuppressFinalize(this); }
        }
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
            try
            {
                if (Value is IAsyncDisposable asynchronous) { await asynchronous.DisposeAsync().ConfigureAwait(false); }
                else if (Value is IDisposable synchronous) { synchronous.Dispose(); }
            }
            finally { GC.SuppressFinalize(this); }
        }
    }
}
