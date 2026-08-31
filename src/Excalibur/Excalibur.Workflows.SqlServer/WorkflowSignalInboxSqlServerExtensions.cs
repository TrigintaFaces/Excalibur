// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Workflows;
using Excalibur.Workflows.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions for the SQL Server durable workflow signal inbox.
/// </summary>
public static class WorkflowSignalInboxSqlServerExtensions
{
    /// <summary>
    /// Wires the durable SQL Server <see cref="IWorkflowSignalInbox"/>, overriding the in-process default so
    /// admitted signals and their deduplication keys survive a restart.
    /// </summary>
    /// <remarks>
    /// This registers the durable inbox <em>and</em> the <see cref="IWorkflowSignalDurability"/> capability
    /// marker in the same call — the marker is inseparable from the wiring it attests, so a consumer's
    /// <c>RequireDurableSignalInbox()</c> gate can only pass when the durable inbox was actually wired.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the SQL Server signal-inbox options (connection string, table).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSqlServerWorkflowSignalInbox(
        this IServiceCollection services,
        Action<SqlServerWorkflowSignalInboxOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        _ = services.AddOptions<SqlServerWorkflowSignalInboxOptions>()
            .Configure(configure)
            .ValidateOnStart();

        // The inbox resolves its tenant partition from ITenantContext, so the container must be able to
        // supply one. Idempotent single-tenant default: a multi-tenant host registers its own first and
        // TryAdd leaves it alone.
        services.AddDefaultTenantContext();

        // AOT-safe manual validation (no data-annotation reflection): connection string present + schema/table
        // are safe SQL identifiers before they are interpolated into the inbox DDL/DML.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<SqlServerWorkflowSignalInboxOptions>, SqlServerWorkflowSignalInboxOptionsValidator>());

        // Override the in-memory TryAdd default (a later Add wins single-service resolution) with the durable
        // impl, and register the durability marker in the SAME call so the marker is never present without
        // the durable inbox actually being wired (: marker inseparable from the wiring).
        // The tenant-aware seam registers the CONCRETE store and emits the tenant-scoping capability for
        // the contract in the same act; the contract itself is then pointed at that same instance with a
        // plain Add, which is what overrides the in-process default a prior AddWorkflows TryAdded. Both
        // markers -- tenancy and durability -- are therefore inseparable from this wiring.
        _ = services.AddTenantAwareStore<IWorkflowSignalInbox, SqlServerWorkflowSignalInbox>();
        services.AddSingleton<IWorkflowSignalInbox>(
            static sp => sp.GetRequiredService<SqlServerWorkflowSignalInbox>());
        services.AddSingleton<IWorkflowSignalDurability, WorkflowSignalDurabilityMarker>();

        // Registered in the SAME call as the durable inbox, for the same reason the marker is: the
        // inbox's idempotency is not a property of this code, it is a property of a UNIQUE constraint
        // in the consumer's database. Wiring the inbox without the check would let a table lacking
        // that constraint accept duplicate signals silently, which is the one failure mode the inbox
        // exists to prevent and the one that reports nothing when it happens.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, SqlServerWorkflowSignalInboxSchemaGuard>());

        return services;
    }
}
