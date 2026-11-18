// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Sharding;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerShardResolverSynchronousDisposalShould
{
    [Fact]
    public void DisposeTheRealSqlResolverThroughSynchronousServiceProviderDisposal()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantShardMap>(new PhysicalMap());
        services.AddExcalibur(excalibur => excalibur.AddEventSourcing(events =>
        {
            events.UseSqlServer(sql => sql.ConnectionString("Server=localhost;Database=UnusedReadinessDisposal;Integrated Security=true;TrustServerCertificate=true"));
            events.UseSqlServerTenantEventStore();
        }));
        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<ITenantStoreResolver<IEventStore>>();
        resolver.Resolve("tenant-different-from-shard").ShouldNotBeNull();
        Should.NotThrow(() => provider.Dispose());
        Should.NotThrow(() => provider.Dispose());
        Should.Throw<ObjectDisposedException>(() => resolver.Resolve("tenant-different-from-shard"));
    }

    private sealed class PhysicalMap : ITenantShardMap
    {
        private readonly ShardInfo _shard = new("physical-sql", "Server=localhost;Database=UnusedReadinessDisposal;Integrated Security=true;TrustServerCertificate=true") { SchemaName = "tenant_schema" };
        public ShardInfo GetShardInfo(string tenantId) => _shard;
        public ShardInfo GetShardById(string shardId) => shardId == _shard.ShardId ? _shard : throw new KeyNotFoundException(shardId);
        public IReadOnlyCollection<string> GetRegisteredShardIds() => [_shard.ShardId];
    }
}
