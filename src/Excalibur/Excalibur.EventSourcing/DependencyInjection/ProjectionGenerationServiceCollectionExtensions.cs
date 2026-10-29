// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers readers that follow durable projection generation pointers.</summary>
public static class ProjectionGenerationServiceCollectionExtensions
{
    /// <summary>Registers a scoped generation reader and its atomic cutover gate.</summary>
    /// <typeparam name="TProjection">The supported read schema.</typeparam>
    /// <param name="services">Application services.</param>
    /// <param name="subscriptionName">The stable subscription that owns the projections.</param>
    /// <param name="readerName">A stable reader name, independent of the projection version.</param>
    /// <param name="compatibleProjectionKeys">Explicitly compatible schemas; defaults to this projection's declared version.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddProjectionGenerationReader<TProjection>(this IServiceCollection services,
        string subscriptionName, string readerName, IReadOnlyCollection<string>? compatibleProjectionKeys = null)
        where TProjection : class
    {
        ArgumentNullException.ThrowIfNull(services);
        RelationalProjectionGenerationStore.Validate(subscriptionName, readerName);
        Register<TProjection>(services, provider => new(provider.GetRequiredService<ISubscriptionCheckpointStore>(),
            provider.GetRequiredService<IGlobalStreamQuery>(), subscriptionName, readerName), compatibleProjectionKeys);
        return services;
    }

    /// <summary>Registers a reader and management control using the inline processor's source and subscription.</summary>
    /// <typeparam name="TProjection">The supported read schema.</typeparam>
    /// <param name="services">Application services.</param>
    /// <param name="readerName">The stable logical reader name.</param>
    /// <param name="compatibleProjectionKeys">Explicitly compatible schemas; defaults to the declared version.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddInlineProjectionGenerationReader<TProjection>(this IServiceCollection services,
        string readerName, IReadOnlyCollection<string>? compatibleProjectionKeys = null) where TProjection : class
    {
        ArgumentNullException.ThrowIfNull(services);
        RelationalProjectionGenerationStore.Validate("inline", readerName);
        Register<TProjection>(services, provider => new(InlineProjectionCoordinator.ResolveCheckpoints(provider),
            provider.GetRequiredService<IGlobalStreamQuery>(),
            InlineProjectionCoordinator.SubscriptionName(provider.GetRequiredService<IOptions<GlobalStreamProjectionOptions>>().Value),
            readerName), compatibleProjectionKeys);
        return services;
    }

    private static void Register<TProjection>(IServiceCollection services,
        Func<IServiceProvider, ProjectionGenerationBinding<TProjection>> bindingFactory, IReadOnlyCollection<string>? compatibleProjectionKeys)
        where TProjection : class
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ProjectionGenerationReader<TProjection>)
            || descriptor.ServiceType == typeof(ProjectionGenerationControl<TProjection>)))
        {
            throw new InvalidOperationException($"A generation reader for '{typeof(TProjection).Name}' is already registered. Each projection type must have one unambiguous reader and management binding.");
        }
        ProjectionCapacity.Register(services);
        var keys = compatibleProjectionKeys?.ToArray();
        services.AddScoped(bindingFactory);
        services.AddScoped(provider => new ProjectionGenerationControl<TProjection>(provider.GetRequiredService<ProjectionGenerationBinding<TProjection>>(), keys) { Capacity = provider.GetRequiredService<ProjectionCapacity>() });
        services.AddScoped(provider =>
        {
            var binding = provider.GetRequiredService<ProjectionGenerationBinding<TProjection>>();
            return new ProjectionGenerationReader<TProjection>(binding.Checkpoints,
                provider.GetRequiredService<IProjectionStore<TProjection>>(), binding.SubscriptionName, binding.ReaderName, keys)
            {
                Capacity = provider.GetRequiredService<ProjectionCapacity>(),
                SourceResource = () => (binding.Checkpoints as IProjectionDestinationScope)?.Scope,
            };
        });
    }
}
