// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Oracle projection destinations supporting TransactionalInline.</summary>
public static class OracleProjectionStoreExtensions
{
    /// <summary>Registers a tenant-scoped projection store against an explicitly provisioned table.</summary>
    /// <typeparam name="TProjection">Read model type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="connectionString">The event store database connection string.</param>
    /// <param name="tableName">Base projection table name.</param>
    /// <param name="jsonOptions">Configured read-model serialization.</param>
    /// <param name="schemaName">Schema owning the destination table.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddOracleProjectionStore<TProjection>(this IServiceCollection services,
        string connectionString, string tableName, JsonSerializerOptions? jsonOptions = null, string schemaName = "EXCALIBUR") where TProjection : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        services.AddDefaultTenantContext();
        services.AddTenantScopedProjectionStore<IProjectionStore<TProjection>, OracleProjectionStore<TProjection>, IProjectionStore<object>>(provider =>
            new OracleProjectionStore<TProjection>(connectionString, provider.GetRequiredService<ITenantContext>(), tableName, jsonOptions, schemaName));
        return services;
    }
}
