// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres.Sharding;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres tenant shard providers.
/// </summary>
public static class PostgresTenantShardingExtensions
{
    /// <summary>
    /// Registers the Postgres <see cref="ITenantStoreResolver{TStore}"/> for
    /// <see cref="IEventStore"/>, enabling tenant-aware event store routing
    /// to Postgres databases.
    /// </summary>
    /// <param name="builder">The event sourcing builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Must be called after <c>EnableTenantSharding</c>. Each shard's connection string
    /// from <see cref="ShardInfo"/> is used to create a dedicated
    /// <see cref="Excalibur.EventSourcing.Postgres.PostgresEventStore"/> instance
    /// with its own <c>NpgsqlDataSource</c> for connection pooling.
    /// </para>
    /// <para>
    /// Schema defaults to <c>public</c> unless <see cref="ShardInfo.SchemaName"/> is set
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
    ///     builder.UsePostgresTenantEventStore();
    /// });
    /// </code>
    /// </example>
    public static IEventSourcingBuilder UsePostgresTenantEventStore(
        this IEventSourcingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<ITenantStoreResolver<IEventStore>>(sp =>
            new PostgresTenantEventStoreResolver(
                sp.GetRequiredService<ITenantShardMap>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetService<ISerializer>(),
                sp.GetService<IPayloadSerializer>()));

        return builder;
    }
}
