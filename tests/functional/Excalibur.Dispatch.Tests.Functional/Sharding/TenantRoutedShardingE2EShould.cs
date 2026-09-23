// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Dispatch.Tests.Functional.Sharding;

/// <summary>
///     End-to-end arm for the <b>tenant-routed sharding</b> guarantee: a tenant's events are written to and
///     read from that tenant's own shard, another tenant's shard is unreachable under the same identifiers,
///     and an operation with no tenant established fails closed rather than picking one.
/// </summary>
/// <remarks>
///     <para>
///     <strong>Why an E2E arm, when the routing store has unit tests.</strong> Routing is the one property
///     that cannot be observed from inside a single store. A unit test over
///     <c>TenantRoutingEventStore</c> can prove it asks its resolver for the ambient tenant's store; it
///     cannot prove that the resolver a real container hands it reaches a different physical database, nor
///     that the shipped SQL Server store the resolver builds actually writes there. The assertions below
///     therefore read the <em>two shard databases directly</em>, not the store's own account of itself.
///     </para>
///     <para>
///     <strong>Composed the way a consumer composes it:</strong>
///     <c>AddExcaliburEventSourcing(b =&gt; { b.EnableTenantSharding(…); b.UseSqlServerTenantEventStore(); })</c>
///     plus the two registrations the framework documents as the consumer's own — an
///     <see cref="ITenantShardMap"/> and an ambient <c>ITenantContext</c>. The store under test is resolved
///     as plain <see cref="IEventStore"/>, which is what application code holds; nothing here names
///     <c>TenantRoutingEventStore</c>, because a consumer never does.
///     </para>
///     <para>
///     <strong>Non-vacuity.</strong> The safety arms (another tenant sees nothing; no tenant fails closed)
///     are each paired with a liveness assertion in the same arm — a router that returned nothing to
///     everybody, or refused everybody, would satisfy safety alone and is exactly what the liveness half
///     catches. The arms were additionally proven RED by severing the routing in place — see the bead's
///     evidence.
///     </para>
/// </remarks>
[Collection(nameof(TenantShardedEventStoreCollection))]
[Trait("Category", "Functional")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Trait("Feature", "TenantRoutedSharding")]
public sealed class TenantRoutedShardingE2EShould
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string AggregateType = "Order";

    private readonly TenantShardedEventStoreFixture _fixture;

    /// <summary>Initializes the arm with the shared two-shard SQL Server fixture.</summary>
    /// <param name="fixture">The two-shard fixture.</param>
    public TenantRoutedShardingE2EShould(TenantShardedEventStoreFixture fixture) => _fixture = fixture;

    /// <summary>
    ///     LIVENESS + SAFETY, in one arm because they are the two halves of one claim. Tenant A's events are
    ///     written to shard A's database and read back under tenant A (liveness); the same aggregate
    ///     identifier under tenant B resolves to shard B and finds nothing, and shard B's database holds no
    ///     rows for it (safety).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The identifiers are deliberately IDENTICAL across the two tenants, and the assertion that carries
    ///     the weight is the <em>physical</em> one: tenant A's rows are in shard A's database and tenant B's
    ///     are in shard B's, for the same aggregate id. A router that ignored the ambient tenant would put
    ///     both streams in one database, which is what this arm goes red on.
    ///     </para>
    ///     <para>
    ///     <strong>The cross-tenant READ assertion is deliberately not carrying that weight, and this is
    ///     worth stating because it is not obvious.</strong> The shipped SQL Server event store also binds a
    ///     tenant term on every read, so "tenant B sees nothing" stays true even on a store that routed both
    ///     tenants to one shard — row-level confinement answers it, and the routing defect is invisible to
    ///     it. That was measured, not assumed: severing the routing left this assertion green. It is kept
    ///     because it is the property a consumer actually cares about and the two defences are meant to hold
    ///     together; the discriminating assertions are the row counts per database.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task RouteEachTenantsEventsToItsOwnShardAndNotTheOthers()
    {
        await ReadyAsync().ConfigureAwait(false);
        await using var provider = BuildConsumerHost();

        var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

        // Both tenants write the SAME aggregate id, each under its own ambient tenant scope.
        using (TenantContextHolder.BeginScope(TenantA))
        {
            var store = provider.GetRequiredService<IEventStore>();
            _ = await store.AppendAsync(
                aggregateId,
                AggregateType,
                [new OrderPlaced(aggregateId, 0), new OrderPlaced(aggregateId, 1)],
                -1,
                CancellationToken.None).ConfigureAwait(false);
        }

        using (TenantContextHolder.BeginScope(TenantB))
        {
            var store = provider.GetRequiredService<IEventStore>();
            _ = await store.AppendAsync(
                aggregateId,
                AggregateType,
                [new OrderPlaced(aggregateId, 0)],
                -1,
                CancellationToken.None).ConfigureAwait(false);
        }

        // SAFETY (the discriminating assertion), read from the engines rather than from the router: the two
        // streams are in two different databases. Asking the store where it put them would let the routing
        // decision under test answer the question about itself.
        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardADatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(
                2, "shard A's database must hold exactly tenant A's two events for this aggregate — if it " +
                   "holds three, both tenants were routed to one shard");
        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardBDatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(
                1, "shard B's database must hold exactly tenant B's one event for the same aggregate id — if " +
                   "it holds none, tenant B's write was routed to another tenant's shard");

        // LIVENESS: each tenant reads its own stream back through the same router, and gets its own.
        using (TenantContextHolder.BeginScope(TenantA))
        {
            var store = provider.GetRequiredService<IEventStore>();
            var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
                .ConfigureAwait(false);

            loaded.Count.ShouldBe(
                2, "tenant A must read back its own two events — a router that reached nothing would satisfy " +
                   "every isolation assertion here while being completely broken");
        }

        using (TenantContextHolder.BeginScope(TenantB))
        {
            var store = provider.GetRequiredService<IEventStore>();
            var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
                .ConfigureAwait(false);

            loaded.Count.ShouldBe(
                1, "tenant B must read back its OWN one event under the shared identifier, not tenant A's two");
        }
    }

    /// <summary>
    ///     SAFETY — a tenant the deployment never mapped to a shard is refused by the router with
    ///     <see cref="TenantShardNotFoundException"/>, and nothing is written to either shard.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This is the arm that isolates the <em>router's</em> decision from everything downstream of it. An
    ///     unmapped tenant is a question only the shard map can answer, and it can only be asked by a router
    ///     that consults the ambient tenant per operation. A router that resolved a fixed shard would accept
    ///     this write and place an unmapped tenant's data in some other tenant's database — the failure mode
    ///     that is hardest to notice afterwards, because both the write and every subsequent read succeed.
    ///     </para>
    ///     <para>
    ///     It is also the arm that distinguishes the router's fail-closed from the store's. See
    ///     <see cref="FailClosedWhenNoTenantIsEstablished"/>, which asserts a real property of the
    ///     composition but cannot tell the two apart.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task RefuseATenantTheDeploymentNeverMappedToAShard()
    {
        await ReadyAsync().ConfigureAwait(false);
        await using var provider = BuildConsumerHost();

        var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

        using (TenantContextHolder.BeginScope("tenant-nobody-mapped"))
        {
            var store = provider.GetRequiredService<IEventStore>();

            _ = await Should.ThrowAsync<TenantShardNotFoundException>(
                async () => await store.AppendAsync(
                    aggregateId,
                    AggregateType,
                    [new OrderPlaced(aggregateId, 0)],
                    -1,
                    CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
        }

        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardADatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(0, "an unmapped tenant's write must not land in another tenant's shard");
        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardBDatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(0, "an unmapped tenant's write must not land in another tenant's shard");
    }

    /// <summary>
    ///     LIVENESS for the second shard, and the proof that the first arm's zero means "routed elsewhere"
    ///     rather than "this shard never works": tenant B writes the same aggregate identifier and its rows
    ///     land in shard B, with shard A untouched.
    /// </summary>
    /// <remarks>
    ///     Without this arm, a router hard-wired to shard A would pass the first arm completely — tenant A's
    ///     rows in A, nothing in B, tenant B reading nothing. Every assertion there is satisfied by the
    ///     defect. This arm is what distinguishes the two.
    /// </remarks>
    [Fact]
    public async Task WriteToTheSecondShardWhenTheSecondTenantIsAmbient()
    {
        await ReadyAsync().ConfigureAwait(false);
        await using var provider = BuildConsumerHost();

        var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

        using (TenantContextHolder.BeginScope(TenantB))
        {
            var store = provider.GetRequiredService<IEventStore>();
            _ = await store.AppendAsync(
                aggregateId,
                AggregateType,
                [new OrderPlaced(aggregateId, 0)],
                -1,
                CancellationToken.None).ConfigureAwait(false);

            var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
                .ConfigureAwait(false);
            loaded.Count.ShouldBe(1, "tenant B must be able to read back what it wrote");
        }

        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardBDatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(1, "tenant B's event must be written to tenant B's own shard, not to the first shard");
        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardADatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(0, "shard A must be untouched by a write made under tenant B");
    }

    /// <summary>
    ///     SAFETY — fail closed. With no tenant established, the router has no shard to resolve and must
    ///     refuse with <see cref="TenantRequiredException"/> rather than choosing one.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Choosing a default here would be the worst available outcome: a background job that forgot to
    ///     establish its tenant would write one tenant's data into whichever shard the default named, and
    ///     nothing downstream would report an error. The refusal is asserted on a write, where the
    ///     consequence is durable.
    /// </para>
    ///     <para>
    ///     <strong>What this arm does NOT discriminate, stated rather than implied:</strong> the router and
    ///     the shipped SQL Server store both refuse a tenant-less operation with this same exception type, so
    ///     a green here does not prove the <em>router</em> refused it. That was measured: severing the
    ///     routing left this arm green. It is kept because "the composition fails closed" is a real property
    ///     a consumer depends on; <see cref="RefuseATenantTheDeploymentNeverMappedToAShard"/> is the arm that
    ///     isolates the router's own decision.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task FailClosedWhenNoTenantIsEstablished()
    {
        await ReadyAsync().ConfigureAwait(false);
        await using var provider = BuildConsumerHost();

        var aggregateId = "agg-" + Guid.NewGuid().ToString("N");
        var store = provider.GetRequiredService<IEventStore>();

        // No TenantContextHolder scope is open, so no tenant is resolved.
        _ = await Should.ThrowAsync<TenantRequiredException>(
            async () => await store.AppendAsync(
                aggregateId,
                AggregateType,
                [new OrderPlaced(aggregateId, 0)],
                -1,
                CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

        // And it refused BEFORE writing: neither shard gained a row.
        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardADatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(0, "a refused, tenant-less write must not reach a shard");
        (await _fixture.RowCountInAsync(TenantShardedEventStoreFixture.ShardBDatabase, aggregateId)
            .ConfigureAwait(false))
            .ShouldBe(0, "a refused, tenant-less write must not reach a shard");
    }

    private async Task ReadyAsync()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "tenant isolation across shards is a data-protection guarantee — this real-SQL-Server arm must "
            + "never be skipped");
        await _fixture.EnsureShardsInitializedAsync().ConfigureAwait(false);
        await _fixture.CleanupAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     The consumer's own composition: enable tenant sharding, use the SQL Server per-shard resolver,
    ///     supply a shard map and an ambient tenant context. Nothing here names the routing store.
    /// </summary>
    private ServiceProvider BuildConsumerHost()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(static b => b.SetMinimumLevel(LogLevel.Warning));

        // The shard map is the consumer's, as the framework documents: it is the deployment's statement of
        // which tenant lives where, and no framework default could know it.
        _ = services.AddSingleton<ITenantShardMap>(new TwoShardMap(
            (TenantA, TenantShardedEventStoreFixture.ShardAId, _fixture.ShardAConnectionString),
            (TenantB, TenantShardedEventStoreFixture.ShardBId, _fixture.ShardBConnectionString)));

        _ = services.AddExcalibur(x => x.AddEventSourcing(static b =>
        {
            // No DefaultShardId: an unmapped tenant must fail rather than land somewhere arbitrary.
            _ = b.EnableTenantSharding(static _ => { });
            _ = b.UseSqlServerTenantEventStore();
        }));

        // The ambient context that leaves an unresolved tenant UNRESOLVED, so a tenant-required operation
        // fails closed instead of writing to a reserved partition.
        _ = services.AddTenantContext();

        return services.BuildServiceProvider();
    }

    /// <summary>A consumer-supplied shard map over exactly two shards, with no default.</summary>
    /// <remarks>
    ///     An unmapped tenant throws rather than resolving anywhere, which is the fail-fast shape the shard
    ///     map contract documents when no default shard is configured.
    /// </remarks>
    private sealed class TwoShardMap : ITenantShardMap
    {
        private readonly Dictionary<string, ShardInfo> _byTenant;

        public TwoShardMap(params (string TenantId, string ShardId, string ConnectionString)[] shards) =>
            _byTenant = shards.ToDictionary(
                s => s.TenantId,
                s => new ShardInfo(s.ShardId, s.ConnectionString, SchemaName: "dbo"),
                StringComparer.Ordinal);

        public ShardInfo GetShardInfo(string tenantId) =>
            _byTenant.TryGetValue(tenantId, out var shard)
                ? shard
                : throw new TenantShardNotFoundException($"Tenant '{tenantId}' is not mapped to a shard.");

        public IReadOnlyCollection<string> GetRegisteredShardIds() =>
            _byTenant.Values.Select(s => s.ShardId).ToList();
    }

    /// <summary>The event this arm appends. Its content is irrelevant; its destination is the subject.</summary>
    [MessageName("Test.TenantRoutedShardingE2E.OrderPlaced")]
    private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();

        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

        public IDictionary<string, object>? Metadata { get; init; }
    }
}
