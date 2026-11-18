// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Explicitly selects durable saga preparation without changing ordinary state-only registration.</summary>
public static class DurableSagaServiceCollectionExtensions
{
    /// <summary>Selects generated state metadata and the existing configured payload codec for durable transitions.</summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="stateMetadata">Metadata for every actual state and nested runtime state type.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// Configures preparation and the concrete coordinator. Configure saga routes and selected persistent
    /// stores separately. The configured IPayloadSerializer must support the actual runtime payload types
    /// without reflection; this method neither replaces the codec nor certifies arbitrary implementations.
    /// The resolver receives fresh canonical read-only options and must return metadata bound to
    /// that exact owner. Verified metadata is retained per exact state type; caller options and
    /// context instances are never mutated or replaced with a reflection resolver.
    /// Missing metadata fails before the database transaction, with no reflective fallback. Use the typed
    /// ProcessAtomicEventAsync entry for compiled timeout dispatch. Repeating this call with the SAME
    /// resolver instance is idempotent; a different explicit policy is refused before registration changes.
    /// </remarks>
    public static IServiceCollection AddDurableSagaTransitions(this IServiceCollection services, IJsonTypeInfoResolver stateMetadata)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(stateMetadata);
        if (!Select(services, new Registration(stateMetadata, false)))
        {
            return services;
        }

        RegisterCommon(services);
        services.AddSingleton(sp => new SagaGeneratedPreparationPolicy(SagaAtomicPreparationPolicy.CreateGenerated(
            stateMetadata, sp.GetRequiredService<IPayloadSerializer>())));
        services.AddSingleton(sp => sp.GetRequiredService<SagaGeneratedPreparationPolicy>().Policy);
        return services;
    }

    /// <summary>Selects the existing runtime serializer and annotated convenience preparation for durable sagas.</summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>This explicit runtime route does not register the generated typed-entry prerequisite.</remarks>
    [RequiresUnreferencedCode("Runtime durable preparation uses reflection-based state and payload serialization. Use explicit generated state metadata and a metadata-capable configured payload codec instead.")]
    [RequiresDynamicCode("Runtime durable preparation can require serializer code generation. Use the generated durable route and typed atomic entry for NativeAOT.")]
    public static IServiceCollection AddDurableSagaTransitionsUsingRuntimeSerialization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!Select(services, new Registration(null, true)))
        {
            return services;
        }

        RegisterCommon(services);
        services.AddSingleton(CreateRuntimePolicy);
        return services;
    }

    [RequiresUnreferencedCode("Creates the explicitly selected runtime serializer preparation policy.")]
    [RequiresDynamicCode("Creates the explicitly selected runtime serializer preparation policy.")]
    private static SagaAtomicPreparationPolicy CreateRuntimePolicy(IServiceProvider services)
        => SagaAtomicPreparationPolicy.CreateRuntime(services.GetRequiredService<DispatchJsonSerializer>());

    private static void RegisterCommon(IServiceCollection services)
    {
        services.AddLogging();
        services.AddOptions<SagaOptions>();
        services.AddDefaultTenantContext();
        services.AddKeyedDefaultAlias<ISagaStore>();
        services.TryAddSingleton<SagaCoordinator>();
        services.TryAddSingleton<ISagaCoordinator>(sp => sp.GetRequiredService<SagaCoordinator>());
    }

    private static bool Select(IServiceCollection services, Registration requested)
    {
        var existing = services.Where(descriptor => descriptor.ServiceType == typeof(Registration)).ToArray();
        if (existing.Length == 0)
        {
            if (services.Any(descriptor => descriptor.ServiceType == typeof(SagaAtomicPreparationPolicy)
                || descriptor.ServiceType == typeof(SagaGeneratedPreparationPolicy)))
            {
                throw new InvalidOperationException("Durable saga preparation was already configured through a different composition.");
            }

            services.AddSingleton(requested);
            return true;
        }

        if (existing.Length == 1 && existing[0].GetImplementationInstance() is Registration configured
            && configured.Runtime == requested.Runtime && ReferenceEquals(configured.Metadata, requested.Metadata))
        {
            return false;
        }

        throw new InvalidOperationException("Conflicting explicit durable saga preparation policies are not allowed.");
    }

    // Never mutate an instance shared with an already-built provider.
    private sealed record Registration(IJsonTypeInfoResolver? Metadata, bool Runtime);
}
