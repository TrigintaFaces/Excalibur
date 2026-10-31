// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

internal sealed class TransactionalProjectionStartup(IServiceProvider services) : IHostedService, IStartupPrerequisiteValidator
{
    internal static void Register(IServiceCollection services)
    {
        services.TryAddSingleton<ITransactionalProjectionCoordinator>(sp =>
        {
            EventNotificationServiceCollectionExtensions.ConfigureProjections(sp);
            return new TransactionalProjectionCoordinator(sp.GetRequiredService<IProjectionRegistry>(), sp.GetRequiredService<IServiceScopeFactory>());
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TransactionalProjectionStartup>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, TransactionalProjectionStartup>());

        // Declare that the two transactional-projection facets honor the ambient tenant. Both are
        // genuinely tenant-scoped: the shared relational destination puts TenantId in the predicate of
        // every statement it issues, and the transactional write additionally REFUSES a transaction whose
        // tenant differs from the destination's.
        //
        // EMITTED HERE, AND THE PLACEMENT IS THE POINT. A marker that could be registered independently of
        // the wiring it attests is the advertised-but-unwired lie in a costume: a host would pass the
        // row-discriminator capability requirement while holding stores that never received a tenant. This
        // method is the transactional-inline opt-in itself, so the marker cannot be present without the
        // wiring -- and Validate() below is a startup prerequisite that refuses to start when the
        // configured event store or any registered destination does not actually implement the facets, so
        // the attestation cannot outlive the capability either.
        //
        // Through the canonical internal seam, as the erasure facet is: one internal marker
        // implementation, no provider clones to drift.
        TenantScopedStoreServiceCollectionExtensions.AddTenantScopingCapability<ITransactionalProjectionEventStore>(services);
        TenantScopedStoreServiceCollectionExtensions.AddTenantScopingCapability<ITransactionalProjectionStore<object>>(services);
    }

    public void Validate()
    {
        EventNotificationServiceCollectionExtensions.ConfigureProjections(services);
        var store = services.GetRequiredKeyedService<IEventStore>("default");
        if (store.GetService(typeof(ITransactionalProjectionEventStore)) is not ITransactionalProjectionEventStore)
        {
            throw new InvalidOperationException("TransactionalInline requires a supporting relational event store; the configured provider or decorator is unsupported.");
        }

        using var scope = services.CreateScope();
        foreach (var registration in services.GetRequiredService<IProjectionRegistry>().GetByMode(ProjectionMode.TransactionalInline))
        {
            registration.ValidateTransactionalStore!(scope.ServiceProvider);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Validate();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}
