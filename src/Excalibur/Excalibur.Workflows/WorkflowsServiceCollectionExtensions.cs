// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

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
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<WorkflowRegistry>();
        services.TryAddSingleton<ActivityRegistry>();
        services.TryAddSingleton<WorkflowSignalNotifier>();
        services.TryAddSingleton<IWorkflowSignalInbox, InMemoryWorkflowSignalInbox>();
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
}
