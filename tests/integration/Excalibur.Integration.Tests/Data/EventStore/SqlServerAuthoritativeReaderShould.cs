// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Helpers;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>Real SQL Server checks for fresh, owned, tenant-confined event observations.</summary>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Integration")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerAuthoritativeReaderShould(SqlServerEventStoreContainerFixture fixture)
{
	private const string AggregateType = "AuthoritativeOrder";

	[Fact]
	public async Task ObserveErasureOutsideAmbientSnapshotAndRejectReadOnlyPresentAndAbsentEvents()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		// A private database in the disposable fixture permits snapshot/read-only transitions without
		// altering the shared master event tables used by other tests.
		var database = "authoritative_" + Guid.NewGuid().ToString("N");
		await using var admin = fixture.CreateConnection();
		await admin.OpenAsync().ConfigureAwait(false);
#pragma warning disable CA2100 // Identifier consists exclusively of the fixed prefix plus generated GUID hex.
		await using (var create = new SqlCommand($"CREATE DATABASE [{database}]", admin))
		{
			_ = await create.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		await using (var enable = new SqlCommand($"ALTER DATABASE [{database}] SET ALLOW_SNAPSHOT_ISOLATION ON", admin))
		{
			_ = await enable.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
#pragma warning restore CA2100
		var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
		{
			InitialCatalog = database,
			Pooling = false,
		}.ConnectionString;
		await using (var setup = new SqlConnection(connectionString))
		{
			await setup.OpenAsync().ConfigureAwait(false);
			foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(
				"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/001_CreateEventStoreSchema.sql"))
			{
#pragma warning disable CA2100 // Fixed shipped schema script, not caller input.
				await using var command = new SqlCommand(script, setup);
#pragma warning restore CA2100
				_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
		}
		var context = new ChangingTenant { TenantId = "snapshot-tenant" };
		var tenant = KeyedTenantPartition.FromContext(context);
		var store = SqlServerEventStore.CreateWithOwnedPrimaryConnectionFactory(() =>
		{
			Transaction.Current.ShouldBeNull("suppression must include the factory invocation");
			return new SqlConnection(connectionString);
		},
			NullLogger<SqlServerEventStore>.Instance, context);
		var original = new OrderPlaced(Guid.NewGuid().ToString("N"), 0);
		(await store.AppendAsync(original.AggregateId, AggregateType, [original], -1, CancellationToken.None)
			.ConfigureAwait(false)).Success.ShouldBeTrue();
		var reader = store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();

		using (var ambient = new TransactionScope(TransactionScopeOption.RequiresNew,
			new TransactionOptions { IsolationLevel = IsolationLevel.Snapshot }, TransactionScopeAsyncFlowOption.Enabled))
		{
			var ambientTransaction = Transaction.Current;
			await using var stale = new SqlConnection(connectionString);
			await stale.OpenAsync().ConfigureAwait(false);
			await using var query = new SqlCommand("SELECT EventType FROM dbo.EventStoreEvents WHERE EventId = @id", stale);
			_ = query.Parameters.AddWithValue("id", original.EventId);
			var oldType = (string)(await query.ExecuteScalarAsync().ConfigureAwait(false))!;
			oldType.ShouldNotBeNullOrWhiteSpace();
			oldType.ShouldNotBe(ErasedEventMarker.EventType);
			using (var outside = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
			{
				(await store.EraseEventsAsync(original.AggregateId, AggregateType, Guid.NewGuid(), CancellationToken.None)
					.ConfigureAwait(false)).ShouldBe(1);
			}
			(await query.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe(oldType,
				"the caller must demonstrably retain the pre-erasure snapshot");
			var fresh = await reader.ReadCurrentAsync(tenant, original.AggregateId, AggregateType,
				original.EventId, 0, CancellationToken.None).ConfigureAwait(false);
			fresh.ShouldNotBeNull();
			fresh.EventType.ShouldBe(ErasedEventMarker.EventType);
			fresh.EventId.ShouldBe(original.EventId);
			fresh.Tenant.TenantId.ShouldBe(tenant.TenantId);
			Transaction.Current.ShouldBeSameAs(ambientTransaction);
			stale.State.ShouldBe(System.Data.ConnectionState.Open);
		}

#pragma warning disable CA2100 // Same private generated database identifier as CREATE DATABASE above.
		await using (var readOnly = new SqlCommand($"ALTER DATABASE [{database}] SET READ_ONLY WITH ROLLBACK IMMEDIATE", admin))
		{
			_ = await readOnly.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
#pragma warning restore CA2100
		foreach (var eventId in new[] { original.EventId, Guid.NewGuid().ToString("N") })
		{
			var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
				await reader.ReadCurrentAsync(tenant, original.AggregateId, AggregateType, eventId, 0, CancellationToken.None));
			failure.Message.ShouldContain("READ_WRITE");
		}
	}

	[Fact]
	public async Task RejectBorrowedOpenConnectionWithoutClosingIt()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await using var borrowed = fixture.CreateConnection();
		await borrowed.OpenAsync().ConfigureAwait(false);
		var context = new ChangingTenant { TenantId = "borrowed" };
		var store = SqlServerEventStore.CreateWithOwnedPrimaryConnectionFactory(() => borrowed,
			NullLogger<SqlServerEventStore>.Instance, context);
		var reader = store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await reader.ReadCurrentAsync(KeyedTenantPartition.FromContext(context), "aggregate", AggregateType, "event", 0, CancellationToken.None));
		failure.Message.ShouldContain("fresh closed owned");
		borrowed.State.ShouldBe(System.Data.ConnectionState.Open);
		await using var probe = new SqlCommand("SELECT 1", borrowed);
		(await probe.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe(1);
	}

	[Fact]
	public async Task RecheckScopeAndPropagateCancellationAndDatabaseErrorsInsteadOfAbsence()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var context = new ChangingTenant { TenantId = "scope-a" };
		var tenant = KeyedTenantPartition.FromContext(context);
		var opens = 0;
		SqlConnection CreateOwned()
		{
			opens++;
			return fixture.CreateConnection();
		}
		var store = SqlServerEventStore.CreateWithOwnedPrimaryConnectionFactory(CreateOwned,
			NullLogger<SqlServerEventStore>.Instance, context);
		var reader = store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		var missing = Guid.NewGuid().ToString("N");
		(await reader.ReadCurrentAsync(tenant, missing, AggregateType, missing, 0, CancellationToken.None)
			.ConfigureAwait(false)).ShouldBeNull();
		opens.ShouldBe(1);
		context.TenantId = "scope-b";
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await reader.ReadCurrentAsync(tenant, missing, AggregateType, missing, 0, CancellationToken.None));
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await reader.ReadCurrentAsync(KeyedTenantPartition.FromContext(context), missing, AggregateType, missing, 0, cancelled.Token));
		opens.ShouldBe(1, "authorization and pre-cancellation must fail before allocating a connection");
		var invalidTable = SqlServerAuthoritativeEventReader.CreateConfined(CreateOwned, "dbo", "MissingAuthoritativeEvents", context);
		await Should.ThrowAsync<SqlException>(async () =>
			await invalidTable.ReadCurrentAsync(KeyedTenantPartition.FromContext(context), missing, AggregateType, missing, 0, CancellationToken.None));
	}

	private sealed class ChangingTenant : ITenantContext
	{
		public string? TenantId { get; set; }
		public bool HasTenant => TenantId is not null;
	}

	[MessageName("Test.SqlServerAuthoritativeReader.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
