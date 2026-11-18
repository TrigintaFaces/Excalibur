// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Workflows;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions for the embedded durable-execution engine.
/// </summary>
public static class WorkflowsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the durable workflow execution engine. Requires an <c>IEventStore</c> (the workflow journal) and
    /// an <c>IEventSerializer</c> to be registered by the consumer's event-sourcing configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// This legacy entry preserves the ordinary JIT reflection default and honors late
    /// WorkflowOptions.PayloadTypeInfoResolver configuration. For an annotation-free metadata route,
    /// use the overload accepting a non-null resolver. An explicit resolver is authoritative over
    /// unspecified parameterless calls in either registration order.
    /// </remarks>
    [RequiresUnreferencedCode("The legacy workflow registration can use reflection for payloads. Use AddWorkflows with a source-generated IJsonTypeInfoResolver.")]
    [RequiresDynamicCode("The legacy workflow registration can require runtime code generation for payloads. Use AddWorkflows with a source-generated IJsonTypeInfoResolver.")]
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AddCoreServices(services);
        if (FindPayloadSelection(services) is null)
        {
            services.TryAddSingleton<WorkflowPreparedPayloadOptions>(CreateLegacyPayloadOptions);
        }

        return services;
    }

    /// <summary>Adds the workflow engine with explicit consumer payload metadata and no implicit reflection fallback.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="payloadTypeInfoResolver">The resolver covering activity, workflow, and signal payload types.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">The services or resolver are null.</exception>
    /// <exception cref="InvalidOperationException">A different explicit resolver was already selected.</exception>
    public static IServiceCollection AddWorkflows(this IServiceCollection services, IJsonTypeInfoResolver payloadTypeInfoResolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(payloadTypeInfoResolver);
        var existing = FindPayloadSelection(services);
        if (existing is not null && !ReferenceEquals(existing.Resolver, payloadTypeInfoResolver))
        {
            throw new InvalidOperationException("Workflow payload metadata was already registered with a different resolver. Compose one resolver before calling AddWorkflows.");
        }

        AddCoreServices(services);
        if (existing is null)
        {
            // Replace an unspecified legacy default, never mutate a selector shared with an already-built provider.
            services.AddSingleton(new WorkflowPayloadSelection(payloadTypeInfoResolver));
            services.RemoveAll<WorkflowPreparedPayloadOptions>();
            services.AddSingleton<WorkflowPreparedPayloadOptions>(provider => CreateExplicitPayloadOptions(provider, payloadTypeInfoResolver));
        }

        return services;
    }

    /// <summary>
    /// Requires that a <em>durable</em> <see cref="IWorkflowSignalInbox"/> be wired, failing host start when
    /// only the in-process default is registered.
    /// </summary>
    /// <remarks>
    /// In-memory inbox paired with a durable journal is a legitimate (limited) configuration, so this
    /// requirement is opt-in: call it when the deployment needs admitted signals and their deduplication
    /// keys to survive a restart. It registers a startup guard that throws unless a durable provider (for
    /// example <c>AddSqlServerWorkflowSignalInbox(...)</c>) has wired the <see cref="IWorkflowSignalDurability"/>
    /// capability marker. The guard inspects registration only, never resolving the inbox.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection RequireDurableSignalInbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, WorkflowSignalInboxDurabilityValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, WorkflowSignalInboxDurabilityValidator>());

        return services;
    }

    /// <summary>
    /// Registers version 1 of a durable workflow body under the given name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The workflow name used to start and journal instances.</param>
    /// <param name="body">The workflow body, whose only legal non-determinism flows through its context.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddWorkflow(
        this IServiceCollection services,
        string name,
        WorkflowBody body) =>
        services.AddWorkflow(name, version: 1, body);

    /// <summary>
    /// Registers a specific definition version of a durable workflow body under the given name. Multiple
    /// versions of one name may be registered together: new instances bind the latest version, while
    /// in-flight instances continue to replay against the version they started on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The workflow name used to start and journal instances.</param>
    /// <param name="version">The definition version. Versions start at 1.</param>
    /// <param name="body">The workflow body, whose only legal non-determinism flows through its context.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddWorkflow(
        this IServiceCollection services,
        string name,
        int version,
        WorkflowBody body)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentNullException.ThrowIfNull(body);

        services.AddSingleton(new WorkflowDescriptor(name, version, body));
        return services;
    }

    /// <summary>
    /// Registers an at-least-once activity under the given name. The activity type is resolved from the
    /// service provider on each invocation.
    /// </summary>
    /// <typeparam name="TActivity">The activity implementation type.</typeparam>
    /// <typeparam name="TInput">The activity input type.</typeparam>
    /// <typeparam name="TOutput">The activity output type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The activity name referenced from a workflow body.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddActivity<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActivity,
        TInput,
        TOutput>(
        this IServiceCollection services,
        string name)
        where TActivity : class, IActivity<TInput, TOutput>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.TryAddTransient<TActivity>();

        ActivityInvoker invoker = async (serviceProvider, input, cancellationToken) =>
        {
            var activity = serviceProvider.GetRequiredService<TActivity>();
            return await activity.ExecuteAsync((TInput)input, cancellationToken).ConfigureAwait(false);
        };

        services.AddSingleton(new ActivityDescriptor(name, invoker));
        return services;
    }

    private static WorkflowPayloadSelection? FindPayloadSelection(IServiceCollection services)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == typeof(WorkflowPayloadSelection)).ToArray();
        if (descriptors.Length == 0)
        {
            return null;
        }

        if (descriptors.Length != 1 || descriptors[0].IsKeyedService
            || descriptors[0].GetImplementationInstance() is not WorkflowPayloadSelection selection)
        {
            throw new InvalidOperationException("Workflow payload selection must be a single framework-owned explicit registration.");
        }

        return selection;
    }

    private static WorkflowPreparedPayloadOptions CreateExplicitPayloadOptions(IServiceProvider provider, IJsonTypeInfoResolver resolver)
    {
        var configured = provider.GetRequiredService<IOptions<WorkflowOptions>>().Value.PayloadTypeInfoResolver;
        if (configured is not null && !ReferenceEquals(configured, resolver))
        {
            throw new InvalidOperationException("WorkflowOptions.PayloadTypeInfoResolver conflicts with the explicit AddWorkflows resolver. Use one resolver for both configuration paths.");
        }

        return new WorkflowPreparedPayloadOptions(WorkflowPayloadSerializer.CreateOptions(resolver));
    }

    [RequiresUnreferencedCode("The legacy workflow registration may select reflection payload serialization. Use the explicit metadata overload.")]
    [RequiresDynamicCode("The legacy workflow registration may require runtime code generation. Use the explicit metadata overload.")]
    private static WorkflowPreparedPayloadOptions CreateLegacyPayloadOptions(IServiceProvider provider)
    {
        var resolver = provider.GetRequiredService<IOptions<WorkflowOptions>>().Value.PayloadTypeInfoResolver;
        return new WorkflowPreparedPayloadOptions(resolver is null
            ? WorkflowPayloadSerializer.CreateReflectionOptions()
            : WorkflowPayloadSerializer.CreateOptions(resolver));
    }

    private static IServiceCollection AddCoreServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<WorkflowRegistry>();
        services.TryAddSingleton<ActivityRegistry>();
        services.TryAddSingleton<WorkflowSignalNotifier>();

        // TryAdd, so a host that established its own tenancy keeps it and a single-tenant host still gets a
        // context. The in-process inbox's constructor requires ITenantContext — the tenant term is part of
        // its mailbox key — so without this the default registration cannot construct it.
        _ = services.AddDefaultTenantContext();

        // Through the tenant-aware seam, not a bare TryAdd. The seam resolves the ambient context
        // (fail-closed) before constructing the inbox and emits the tenant-scoping capability for the
        // contract in the same act, so the attestation cannot be present without the wiring it describes.
        // The mailbox contract is tenant-owned, and a multi-tenant host refuses any tenant-owned store that
        // attests nothing -- registering this default plainly made the framework's own default the thing
        // that failed the gate.
        _ = services.AddTenantAwareStore<IWorkflowSignalInbox, InMemoryWorkflowSignalInbox>();
        services.TryAddSingleton<IWorkflowSignalInbox>(
            static sp => sp.GetRequiredService<InMemoryWorkflowSignalInbox>());
        services.TryAddScoped<IWorkflowExecutor, WorkflowExecutor>();

        services.AddOptions<WorkflowOptions>().ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<WorkflowOptions>, WorkflowOptionsValidator>());

        // Registry well-formedness (no duplicate (name, version), versions >= 1) is validated at startup via
        // ValidateOnStart so a mis-registration fails fast at composition, not on first workflow start.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<WorkflowOptions>, WorkflowRegistryValidator>());

        return services;
    }
}
