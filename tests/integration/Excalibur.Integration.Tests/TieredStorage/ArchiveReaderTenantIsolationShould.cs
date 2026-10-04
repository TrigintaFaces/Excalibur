// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.EventStore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class SqlServerArchiveReaderTenantIsolationShould(SqlServerEventStoreContainerFixture fixture)
{
	[Fact]
	public async Task ReadTheCandidatePartitionRegardlessOfAmbientTenant()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await ArchiveReaderTenantScenario.RunAsync(context =>
			new SqlServerEventStore(fixture.ConnectionString, NullLogger<SqlServerEventStore>.Instance, context));
	}
}

[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class PostgresArchiveReaderTenantIsolationShould(PostgresEventStoreContainerFixture fixture)
{
	[Fact]
	public async Task ReadTheCandidatePartitionRegardlessOfAmbientTenant()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
		await ArchiveReaderTenantScenario.RunAsync(context =>
			new PostgresEventStore(source, NullLogger<PostgresEventStore>.Instance, context));
	}
}

internal static class ArchiveReaderTenantScenario
{
	internal static async Task RunAsync(Func<ITenantContext, IEventStore> create)
	{
		var context = new MutableTenant();
		var store = create(context);
		var reader = (store.GetService(typeof(IEventStoreArchiveReader)) as IEventStoreArchiveReader).ShouldNotBeNull();
		var archive = (store.GetService(typeof(IEventStoreArchive)) as IEventStoreArchive).ShouldNotBeNull();
		var id = Guid.NewGuid().ToString("N");
		string[] tenants = ["archive-reader-A", "archive-reader-B", KeyedTenantPartition.Untenanted.TenantId];
		var expected = new Dictionary<(string Tenant, string Type), IReadOnlyList<StoredEvent>>();
		foreach (var tenant in tenants)
		{
			context.TenantId = tenant;
			foreach (var type in new[] { "ReaderOrder", "ReaderInvoice" })
			{
				var owner = tenant;
				(await store.AppendAsync(id, type,
					[new ReaderEvent(id, 0, owner) { Metadata = new Dictionary<string, object> { ["owner"] = owner, ["kind"] = type } },
					 new ReaderEvent(id, 1, owner) { Metadata = new Dictionary<string, object> { ["owner"] = owner, ["kind"] = type } }],
					-1, CancellationToken.None)).Success.ShouldBeTrue();
				expected.Add((KeyedTenantPartition.FromContext(context).TenantId, type), await store.LoadAsync(id, type, CancellationToken.None));
			}
		}

		context.TenantId = "unrelated";
		foreach (var pair in expected)
		{
			context.TenantId = "unrelated";
			var partition = KeyedTenantPartition.FromStoredValue(pair.Key.Tenant);
			var rows = await reader.LoadArchiveEventsAsync(partition, id, pair.Key.Type, 0, CancellationToken.None);
			var row = rows.ShouldHaveSingleItem();
			row.EventId.ShouldBe(pair.Value[0].EventId);
			row.EventData.ShouldBe(pair.Value[0].EventData);
			row.TenantId.ShouldBe(pair.Key.Tenant);
			row.GlobalPosition.ShouldBe(pair.Value[0].GlobalPosition);
			row.Metadata.ShouldBe(pair.Value[0].Metadata);
			(await archive.TombstoneArchivedEventsUpToVersionAsync(partition, id, pair.Key.Type, 0, CancellationToken.None)).ShouldBe(1);
			context.TenantId = null;
			var after = await reader.LoadArchiveEventsAsync(partition, id, pair.Key.Type, 1, CancellationToken.None);
			after.Select(e => e.Version).ShouldBe(new long[] { 0, 1 });
			after[0].EventData.ShouldBeNull();
			after[0].ArchivedAt.ShouldNotBeNull();
			after[1].EventData.ShouldBe(pair.Value[1].EventData);
		}
		context.TenantId.ShouldBeNull();
	}

	private sealed class MutableTenant : ITenantContext
	{
		public string? TenantId { get; set; }
		public bool HasTenant => TenantId is not null;
	}

	[MessageName("Test.ArchiveReader.Event")]
	private sealed record ReaderEvent(string AggregateId, long Version, string Owner) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
