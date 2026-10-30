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
            Configure(sp);
            return new TransactionalProjectionCoordinator(sp.GetRequiredService<IProjectionRegistry>(), sp.GetRequiredService<IServiceScopeFactory>());
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TransactionalProjectionStartup>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, TransactionalProjectionStartup>());
    }

    public void Validate()
    {
        Configure(services);
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

    private static void Configure(IServiceProvider services)
    {
        foreach (var configuration in services.GetServices<EventNotificationServiceCollectionExtensions.IConfigureProjection>())
        {
            configuration.Configure();
        }
    }
}
