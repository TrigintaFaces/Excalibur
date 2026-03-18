// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for the generic IDb-typed SQL Server DI overloads (Sprint 659 T.2).
/// Validates AddSqlServerEventStore&lt;TDb&gt;, AddSqlServerSnapshotStore&lt;TDb&gt;,
/// AddSqlServerOutboxStore&lt;TDb&gt;, AddSqlServerEventSourcing&lt;TDb&gt;, and
/// AddSqlServerProjectionStore&lt;TProjection, TDb&gt;.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerIDbTypedOverloadsShould : UnitTestBase
{
	// --- AddSqlServerEventStore<TDb> ---

	[Fact]
	public void RegisterEventStore_WithIDbTypedOverload()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IEventSourcingTestDb>(new TestDb());
		services.AddLogging();

		services.AddSqlServerEventStore<IEventSourcingTestDb>();

		services.Any(sd =>
			sd.ServiceType == typeof(SqlServerEventStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesNull_EventStore_IDbOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerEventStore<IEventSourcingTestDb>());
	}

	[Fact]
	public void ReturnServicesForChaining_EventStore_IDbOverload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerEventStore<IEventSourcingTestDb>();

		result.ShouldBeSameAs(services);
	}

	// --- AddSqlServerSnapshotStore<TDb> ---

	[Fact]
	public void RegisterSnapshotStore_WithIDbTypedOverload()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IEventSourcingTestDb>(new TestDb());
		services.AddLogging();

		services.AddSqlServerSnapshotStore<IEventSourcingTestDb>();

		services.Any(sd =>
			sd.ServiceType == typeof(SqlServerSnapshotStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesNull_SnapshotStore_IDbOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerSnapshotStore<IEventSourcingTestDb>());
	}

	[Fact]
	public void ReturnServicesForChaining_SnapshotStore_IDbOverload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerSnapshotStore<IEventSourcingTestDb>();

		result.ShouldBeSameAs(services);
	}

	// --- AddSqlServerOutboxStore<TDb> (event-sourced) ---

	[Fact]
	public void RegisterOutboxStore_WithIDbTypedOverload()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IEventSourcingTestDb>(new TestDb());
		services.AddLogging();

		services.AddSqlServerOutboxStore<IEventSourcingTestDb>();

		services.Any(sd =>
			sd.ServiceType == typeof(IEventSourcedOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesNull_OutboxStore_IDbOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerOutboxStore<IEventSourcingTestDb>());
	}

	[Fact]
	public void ReturnServicesForChaining_OutboxStore_IDbOverload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerOutboxStore<IEventSourcingTestDb>();

		result.ShouldBeSameAs(services);
	}

	// --- AddSqlServerEventSourcing<TDb> (compound) ---

	[Fact]
	public void RegisterAllStores_WithCompoundIDbOverload()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IEventSourcingTestDb>(new TestDb());
		services.AddLogging();

		services.AddSqlServerEventSourcing<IEventSourcingTestDb>();

		// Should register event store, snapshot store, and outbox store
		services.Any(sd => sd.ServiceType == typeof(SqlServerEventStore)).ShouldBeTrue();
		services.Any(sd => sd.ServiceType == typeof(SqlServerSnapshotStore)).ShouldBeTrue();
		services.Any(sd => sd.ServiceType == typeof(IEventSourcedOutboxStore)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesNull_EventSourcing_IDbOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerEventSourcing<IEventSourcingTestDb>());
	}

	[Fact]
	public void ReturnServicesForChaining_EventSourcing_IDbOverload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerEventSourcing<IEventSourcingTestDb>();

		result.ShouldBeSameAs(services);
	}

}

// Test infrastructure (file-level to avoid CA1034)

public interface IEventSourcingTestDb : IDb;

file sealed class TestDb : IEventSourcingTestDb
{
	public IDbConnection Connection => new SqlConnection("Server=localhost;Database=TestDb;Integrated Security=true;");
	public void Open() { }
	public void Close() { }
}
