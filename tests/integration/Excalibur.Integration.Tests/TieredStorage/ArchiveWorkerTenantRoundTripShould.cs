// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.TieredStorage;
using Excalibur.Integration.Tests.Data.EventStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Testcontainers.Azurite;
using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class SqlServerArchiveWorkerTenantRoundTripShould(SqlServerEventStoreContainerFixture fixture)
{
	[Fact]
	public async Task ArchiveEachCandidatesOwnPayloadAndKeepTheHotTail()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await ArchiveWorkerTenantScenario.RunAsync(builder => builder.UseSqlServer(sql =>
			sql.ConnectionString(fixture.ConnectionString).EventStoreSchema(fixture.SchemaName).EventStoreTable(fixture.TableName)));
	}
}

[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class PostgresArchiveWorkerTenantRoundTripShould(PostgresEventStoreContainerFixture fixture)
{
	[Fact]
	public async Task ArchiveEachCandidatesOwnPayloadAndKeepTheHotTail()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await ArchiveWorkerTenantScenario.RunAsync(builder => builder.UsePostgres(pg =>
			pg.ConnectionString(fixture.ConnectionString).EventStoreTable(fixture.TableName)));
	}
}

internal static class ArchiveWorkerTenantScenario
{
	internal static async Task RunAsync(Action<IEventSourcingBuilder> configure)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		await using var blob = new AzuriteBuilder().WithImage("mcr.microsoft.com/azure-storage/azurite:3.36.0")
			.WithCommand("--skipApiVersionCheck").Build();
		await blob.StartAsync(timeout.Token);
		var context = new MutableTenant { TenantId = "worker-A" };
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<ITenantContext>(context);
		var builder = new ExcaliburEventSourcingBuilder(services);
		configure(builder);
		builder.UseAzureBlobColdEventStore(options => options.ConnectionString(blob.GetConnectionString())
			.ContainerName("worker-" + Guid.NewGuid().ToString("N")).CreateContainerIfNotExists()
			.Layout(ColdArchiveLayout.TypedV2));
		var policy = new ArchivePolicy { MaxAge = TimeSpan.FromDays(1), RetainRecentCount = 1 };
		builder.UseTieredStorage(options =>
		{
			options.MaxAge = policy.MaxAge;
			options.RetainRecentCount = policy.RetainRecentCount;
		});
		await using var provider = services.BuildServiceProvider();
		var cold = provider.GetRequiredService<IColdEventStore>();
		await ((IColdEventStoreMigration)cold).ActivateTypedLayoutAsync(timeout.Token);
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var reader = provider.GetRequiredService<IEventStoreArchiveReader>();
		var archive = provider.GetRequiredService<IEventStoreArchive>();
		var id = Guid.NewGuid().ToString("N");
		var expected = new Dictionary<(string Tenant, string Type), IReadOnlyList<StoredEvent>>();
		foreach (var tenant in new[] { "worker-A", "worker-B", KeyedTenantPartition.Untenanted.TenantId })
		foreach (var type in new[] { "WorkerOrder", "WorkerInvoice" })
		{
			context.TenantId = tenant;
			var events = Enumerable.Range(0, 3).Select(v => new WorkerEvent(id, v, tenant, type)
			{
				OccurredAt = v < 2 ? DateTimeOffset.UtcNow.AddDays(-10) : DateTimeOffset.UtcNow,
			}).ToArray();
			(await store.AppendAsync(id, type, events, -1, timeout.Token)).Success.ShouldBeTrue();
			expected.Add((tenant, type), await store.LoadAsync(id, type, timeout.Token));
		}

		context.TenantId = "unrelated-worker-host";
		var worker = provider.GetServices<IHostedService>().OfType<EventArchiveService>().ShouldHaveSingleItem();
		var cycle = typeof(EventArchiveService).GetMethod("RunArchiveCycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)
			.ShouldNotBeNull();
		await ((Task)cycle.Invoke(worker, [timeout.Token])!).WaitAsync(timeout.Token);
		context.TenantId.ShouldBe("unrelated-worker-host");

		foreach (var pair in expected)
		{
			var partition = KeyedTenantPartition.FromStoredValue(pair.Key.Tenant);
			var raw = await reader.LoadArchiveEventsAsync(partition, id, pair.Key.Type, 2, timeout.Token);
			raw.Count.ShouldBe(3);
			raw[0].EventData.ShouldBeNull();
			raw[1].EventData.ShouldBeNull();
			raw[0].ArchivedAt.ShouldNotBeNull();
			raw[1].ArchivedAt.ShouldNotBeNull();
			raw[2].EventData.ShouldBe(pair.Value[2].EventData);
			var storedCold = await cold.ReadAsync(partition, id, pair.Key.Type, timeout.Token);
			storedCold.Select(e => e.Version).ShouldBe(new long[] { 0, 1 });
			storedCold.Select(e => e.EventId).ShouldBe(pair.Value.Take(2).Select(e => e.EventId));
			storedCold[0].EventData.ShouldBe(pair.Value[0].EventData);
			context.TenantId = pair.Key.Tenant;
			var restored = await store.LoadAsync(id, pair.Key.Type, timeout.Token);
			restored.Count.ShouldBe(3);
			for (var i = 0; i < restored.Count; i++)
			{
				restored[i].EventData.ShouldBe(pair.Value[i].EventData);
				restored[i].GlobalPosition.ShouldBe(pair.Value[i].GlobalPosition);
				restored[i].TenantId.ShouldBe(pair.Key.Tenant);
			}
		}
		(await archive.GetArchiveCandidatesAsync(policy, 100, timeout.Token)).ShouldNotContain(c => c.AggregateId == id);
	}

	private sealed class MutableTenant : ITenantContext
	{
		public string? TenantId { get; set; }
		public bool HasTenant => TenantId is not null;
	}

	[MessageName("Test.ArchiveWorker.Event")]
	private sealed record WorkerEvent(string AggregateId, long Version, string Owner, string Kind) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
