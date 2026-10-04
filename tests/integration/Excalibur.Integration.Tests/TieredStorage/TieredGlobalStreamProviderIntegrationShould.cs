// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Testcontainers.Azurite;

using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerTieredGlobalStreamProviderIntegrationShould(SqlServerEventStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(false, "roundtrip")]
    [InlineData(true, "roundtrip")]
    [InlineData(false, "erase")]
    [InlineData(true, "erase")]
    [InlineData(false, "failure")]
    [InlineData(true, "failure")]
    public async Task RestoreTheRegisteredGlobalFeed(bool filtered, string scenario)
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await fixture.EnsureInitializedAsync();
        await TieredGlobalStreamScenario.RunAsync(
            builder => builder.UseSqlServer(sql => sql.ConnectionString(fixture.ConnectionString)
                .EventStoreSchema(fixture.SchemaName).EventStoreTable(fixture.TableName)),
            tenant => new SqlServerEventStore(fixture.ConnectionString, NullLogger<SqlServerEventStore>.Instance, tenant),
            filtered, scenario);
    }
}

[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
[Trait("Database", "Postgres")]
public sealed class PostgresTieredGlobalStreamProviderIntegrationShould(PostgresEventStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(false, "roundtrip")]
    [InlineData(true, "roundtrip")]
    [InlineData(false, "erase")]
    [InlineData(true, "erase")]
    [InlineData(false, "failure")]
    [InlineData(true, "failure")]
    public async Task RestoreTheRegisteredGlobalFeed(bool filtered, string scenario)
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await fixture.EnsureInitializedAsync();
        await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
        await TieredGlobalStreamScenario.RunAsync(
            builder => builder.UsePostgres(pg => pg.ConnectionString(fixture.ConnectionString).EventStoreTable(fixture.TableName)),
            tenant => new PostgresEventStore(source, NullLogger<PostgresEventStore>.Instance, tenant),
            filtered, scenario);
    }
}

internal static class TieredGlobalStreamScenario
{
    private static readonly string[] ArchiveTenants = ["archive-A", "archive-B"];

    internal static async Task RunAsync(Action<IEventSourcingBuilder> configure,
        Func<ITenantContext, IEventStore> createRawStore, bool filtered, string scenario)
    {
        await using var blob = new AzuriteBuilder().WithImage("mcr.microsoft.com/azure-storage/azurite:3.36.0")
            .WithCommand("--skipApiVersionCheck").Build();
        await blob.StartAsync();
        var coldServices = new ServiceCollection();
        coldServices.AddLogging();
        new ExcaliburEventSourcingBuilder(coldServices).UseAzureBlobColdEventStore(options =>
            options.ConnectionString(blob.GetConnectionString()).ContainerName("global-" + Guid.NewGuid().ToString("N"))
                .CreateContainerIfNotExists());
        await using var coldProvider = coldServices.BuildServiceProvider();
        var actualCold = coldProvider.GetRequiredService<IColdEventStore>();
        var observingCold = new ObservingColdStore(actualCold);
        var context = new TenantContext { TenantId = "archive-A" };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(context);
        services.AddSingleton<IColdEventStore>(observingCold);
        var builder = new ExcaliburEventSourcingBuilder(services);
        configure(builder);
        builder.UseTieredStorage(policy => policy.MaxAge = TimeSpan.FromDays(1));
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredKeyedService<IEventStore>("default");
        var global = provider.GetRequiredService<IGlobalStreamQuery>();
        var archive = provider.GetRequiredService<IEventStoreArchive>();
        var start = new GlobalStreamPosition(await global.GetHeadPositionAsync(CancellationToken.None), DateTimeOffset.MinValue);
        var id = Guid.NewGuid().ToString("N");
        const string type = "GlobalArchiveOrder";
        var originals = new List<StoredEvent>();
        foreach (var tenantId in ArchiveTenants)
        {
            context.TenantId = tenantId;
            var tenant = KeyedTenantPartition.Scoped(tenantId);
            var message = new ArchiveEvent(id, 0)
            {
                Metadata = new Dictionary<string, object> { ["owner"] = tenantId },
            };
            (await store.AppendAsync(id, type, [message], -1, CancellationToken.None)).Success.ShouldBeTrue();
            var persisted = (await store.LoadAsync(id, type, CancellationToken.None)).ShouldHaveSingleItem();
            persisted.EventData.ShouldNotBeNull();
            persisted.TenantId.ShouldBe(tenantId);
            originals.Add(persisted);
            (await actualCold.WriteAsync(tenant, id, type, [persisted], CancellationToken.None)).ShouldBe(0);
            (await archive.TombstoneArchivedEventsUpToVersionAsync(tenant, id, type, 0, CancellationToken.None)).ShouldBe(1);
            var raw = createRawStore(new TenantContext { TenantId = tenantId });
            var marker = (await raw.LoadAsync(id, type, CancellationToken.None)).ShouldHaveSingleItem();
            marker.EventData.ShouldBeNull();
            marker.ArchivedAt.ShouldNotBeNull();
            marker.GlobalPosition.ShouldBe(persisted.GlobalPosition);
        }
        originals[0].GlobalPosition.ShouldBeGreaterThan(start.Position);
        originals[1].GlobalPosition.ShouldBe(originals[0].GlobalPosition + 1);
        originals[0].EventType.ShouldBe(originals[1].EventType);

        var erasureCommitted = false;
        observingCold.AfterRead = async (tenant, aggregateId, aggregateType, fetched) =>
        {
            aggregateId.ShouldBe(id);
            aggregateType.ShouldBe(type);
            if (scenario == "erase" && tenant.TenantId == "archive-A")
            {
                erasureCommitted.ShouldBeFalse();
                var oldEvent = fetched.ShouldHaveSingleItem();
                oldEvent.EventId.ShouldBe(originals[0].EventId);
                oldEvent.EventData.ShouldNotBeNull();
                oldEvent.EventData.ShouldBe(originals[0].EventData);
                var raw = createRawStore(new TenantContext { TenantId = tenant.TenantId });
                var erasure = raw.GetService(typeof(IEventStoreErasure)).ShouldBeAssignableTo<IEventStoreErasure>();
                (await erasure.EraseEventsAsync(id, type, Guid.NewGuid(), CancellationToken.None)).ShouldBe(1);
                var erased = (await raw.LoadAsync(id, type, CancellationToken.None)).ShouldHaveSingleItem();
                erased.EventType.ShouldBe(ErasedEventMarker.EventType);
                erased.EventData.ShouldBeNull();
                erasureCommitted = true;
            }
            if (scenario == "failure" && tenant.TenantId == "archive-B")
            {
                observingCold.Reads.ShouldBe(ArchiveTenants);
                throw new InvalidOperationException("injected B cold failure");
            }
        };

        context.TenantId = "unrelated-global-caller";
        IReadOnlyList<StoredEvent>? delivered = null;
        async Task ReadAsync() => delivered = filtered
            ? await global.ReadByEventTypeAsync(originals[0].EventType, start, 2, CancellationToken.None)
            : await global.ReadAllAsync(start, 2, CancellationToken.None);
        if (scenario == "failure")
        {
            (await Should.ThrowAsync<InvalidOperationException>(ReadAsync)).Message.ShouldBe("injected B cold failure");
            delivered.ShouldBeNull();
        }
        else
        {
            await ReadAsync();
            delivered.ShouldNotBeNull();
            delivered.Select(e => e.EventId).ShouldBe(originals.Select(e => e.EventId));
            delivered.Select(e => e.TenantId).ShouldBe(ArchiveTenants);
            delivered.Select(e => e.GlobalPosition).ShouldBe(originals.Select(e => e.GlobalPosition));
            for (var i = 0; i < originals.Count; i++)
            {
                if (scenario == "erase" && i == 0)
                {
                    erasureCommitted.ShouldBeTrue();
                    delivered[i].EventType.ShouldBe(ErasedEventMarker.EventType);
                    delivered[i].EventData.ShouldBeNull();
                    delivered[i].Metadata.ShouldBeNull();
                }
                else
                {
                    delivered[i].EventType.ShouldBe(originals[i].EventType);
                    delivered[i].EventData.ShouldBe(originals[i].EventData);
                    delivered[i].Metadata.ShouldBe(originals[i].Metadata);
                }
            }
        }
        observingCold.Reads.ShouldBe(ArchiveTenants);
        context.TenantId.ShouldBe("unrelated-global-caller");
    }

    private sealed class TenantContext : ITenantContext
    {
        public string? TenantId { get; set; }
        public bool HasTenant => TenantId is not null;
    }

    [MessageName("Test.TieredGlobalArchive.Event")]
    private sealed record ArchiveEvent(string AggregateId, long Version) : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public IDictionary<string, object>? Metadata { get; init; }
    }

    private sealed class ObservingColdStore(IColdEventStore inner) : IColdEventStore
    {
        public Func<KeyedTenantPartition, string, string, IReadOnlyList<StoredEvent>, Task>? AfterRead { get; set; }
        public List<string> Reads { get; } = [];

        public async Task<IReadOnlyList<StoredEvent>> ReadAsync(KeyedTenantPartition tenant, string id, string type, CancellationToken token)
        {
            var events = await inner.ReadAsync(tenant, id, type, token);
            Reads.Add(tenant.TenantId);
            if (AfterRead is { } afterRead)
            {
                await afterRead(tenant, id, type, events);
            }
            return events;
        }

        public Task<long> WriteAsync(KeyedTenantPartition tenant, string id, string type,
            IReadOnlyList<StoredEvent> events, CancellationToken token) => inner.WriteAsync(tenant, id, type, events, token);
        public Task<IReadOnlyList<StoredEvent>> ReadAsync(KeyedTenantPartition tenant, string id, string type,
            long fromVersion, CancellationToken token) => inner.ReadAsync(tenant, id, type, fromVersion, token);
        public Task<bool> HasArchivedEventsAsync(KeyedTenantPartition tenant, string id, string type, CancellationToken token) =>
            inner.HasArchivedEventsAsync(tenant, id, type, token);
    }
}
