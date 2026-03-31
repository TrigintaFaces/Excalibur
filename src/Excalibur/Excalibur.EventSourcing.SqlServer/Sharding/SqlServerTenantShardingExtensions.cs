// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer.Sharding;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server tenant shard providers.
/// </summary>
public static class SqlServerTenantShardingExtensions
{
    /// <summary>
    /// Registers the SQL Server <see cref="ITenantStoreResolver{TStore}"/> for
    /// <see cref="IEventStore"/>, enabling tenant-aware event store routing
    /// to SQL Server databases.
    /// </summary>
    /// <param name="builder">The event sourcing builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Must be called after <c>EnableTenantSharding</c>. Each shard's connection string
    /// from <see cref="ShardInfo"/> is used to create a dedicated
    /// <see cref="Excalibur.EventSourcing.SqlServer.SqlServerEventStore"/> instance.
    /// ADO.NET manages per-connection-string pooling automatically.
    /// </para>
    /// <para>
    /// Schema defaults to <c>dbo</c> unless <see cref="ShardInfo.SchemaName"/> is set
    /// for the shard.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddExcaliburEventSourcing(builder =&gt;
    /// {
    ///     builder.EnableTenantSharding(options =&gt;
    ///     {
    ///         options.EnableTenantSharding = true;
    ///     });
    ///     builder.UseSqlServerTenantEventStore();
    /// });
    /// </code>
    /// </example>
    public static IEventSourcingBuilder UseSqlServerTenantEventStore(
        this IEventSourcingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<ITenantStoreResolver<IEventStore>>(sp =>
            new SqlServerTenantEventStoreResolver(
                sp.GetRequiredService<ITenantShardMap>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetService<ISerializer>(),
                sp.GetService<IPayloadSerializer>()));

        return builder;
    }
}
