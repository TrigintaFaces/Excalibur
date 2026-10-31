// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation;

/// <summary>Selects borrowed request services or owns a scope for one validation operation.</summary>
internal sealed class ValidatorServiceScope
{
    private readonly IServiceProvider _root;
    private readonly IServiceProvider _rootAlias;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IDispatchAmbientScopeAccessor? _ambient;

    internal ValidatorServiceScope(IServiceProvider root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        _rootAlias = root.GetService<IServiceProvider>() ?? root;
        _scopeFactory = root.GetService<IServiceScopeFactory>();
        _ambient = root.GetService<IDispatchAmbientScopeAccessor>();
    }

    internal Lease Acquire(IServiceProvider? requestServices)
    {
        if (IsBorrowable(requestServices))
        {
            return new Lease(requestServices!);
        }

        var ambient = _ambient?.CurrentServiceProvider;
        if (IsBorrowable(ambient))
        {
            return new Lease(ambient!);
        }

        if (_scopeFactory is null)
        {
            throw new InvalidOperationException(
                "FluentValidation requires request-scoped services or an IServiceScopeFactory to create a validation scope.");
        }

        return new Lease(_scopeFactory.CreateAsyncScope());
    }

    private bool IsBorrowable(IServiceProvider? provider) =>
        provider is not null && !ReferenceEquals(provider, _root) && !ReferenceEquals(provider, _rootAlias)
        && !(provider is ServiceProvider && ReferenceEquals(provider.GetService<IServiceProvider>(), _rootAlias));

    internal readonly struct Lease : IAsyncDisposable
    {
        private readonly AsyncServiceScope? _ownedScope;

        internal Lease(IServiceProvider provider) => Provider = provider;

        internal Lease(AsyncServiceScope scope)
        {
            Provider = scope.ServiceProvider;
            _ownedScope = scope;
        }

        internal IServiceProvider Provider { get; }

        public ValueTask DisposeAsync() => _ownedScope?.DisposeAsync() ?? default;
    }
}
