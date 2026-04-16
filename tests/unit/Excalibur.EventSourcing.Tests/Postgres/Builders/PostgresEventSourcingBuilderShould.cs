// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Npgsql;

namespace Excalibur.EventSourcing.Tests.Postgres.Builders;

/// <summary>
/// Unit tests for <see cref="PostgresEventSourcingBuilder"/> — 5 connection overloads,
/// feature methods, last-wins semantics, DataSource, and fluent chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventSourcingBuilderShould : UnitTestBase
{
	private const string TestConnectionString =
		"Host=localhost;Database=TestDb;Username=test;Password=test";

	private static (PostgresEventSourcingBuilder Builder, PostgresEventSourcingOptions Options) CreateBuilder()
	{
		var options = new PostgresEventSourcingOptions();
		var builder = new PostgresEventSourcingBuilder(options);
		return (builder, options);
	}

	// --- Connection overloads (happy path) ---

	[Fact]
	public void ConnectionString_SetConnectionStringOnOptions()
	{
		var (builder, options) = CreateBuilder();

		builder.ConnectionString(TestConnectionString);

		options.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void DataSourceFactory_StoreFactory()
	{
		var (builder, _) = CreateBuilder();
		Func<IServiceProvider, NpgsqlDataSource> factory = _ =>
			NpgsqlDataSource.Create(TestConnectionString);

		builder.DataSourceFactory(factory);

		builder.DataSourceFactoryFunc.ShouldBe(factory);
	}

	[Fact]
	public void DataSource_StoreInstance()
	{
		var (builder, _) = CreateBuilder();
		using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

		builder.DataSource(dataSource);

		builder.DataSourceInstance.ShouldBe(dataSource);
	}

	[Fact]
	public void ConnectionStringName_StoreName()
	{
		var (builder, _) = CreateBuilder();

		builder.ConnectionStringName("EventStore");

		builder.ConnectionStringNameValue.ShouldBe("EventStore");
	}

	[Fact]
	public void BindConfiguration_StorePath()
	{
		var (builder, _) = CreateBuilder();

		builder.BindConfiguration("EventSourcing:Postgres");

		builder.BindConfigurationPath.ShouldBe("EventSourcing:Postgres");
	}

	// --- Last-wins semantics (5 overloads) ---

	[Fact]
	public void DataSourceFactory_ClearConnectionString()
	{
		var (builder, options) = CreateBuilder();

		builder.ConnectionString(TestConnectionString);
		builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString));

		options.ConnectionString.ShouldBeNull();
		builder.DataSourceFactoryFunc.ShouldNotBeNull();
		builder.DataSourceInstance.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_ClearDataSource()
	{
		var (builder, options) = CreateBuilder();
		using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

		builder.DataSource(dataSource);
		builder.ConnectionString(TestConnectionString);

		options.ConnectionString.ShouldBe(TestConnectionString);
		builder.DataSourceInstance.ShouldBeNull();
		builder.DataSourceFactoryFunc.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void DataSource_ClearAll()
	{
		var (builder, options) = CreateBuilder();
		using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

		builder.ConnectionString(TestConnectionString);
		builder.DataSource(dataSource);

		options.ConnectionString.ShouldBeNull();
		builder.DataSourceInstance.ShouldBe(dataSource);
		builder.DataSourceFactoryFunc.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void ConnectionStringName_ClearAll()
	{
		var (builder, options) = CreateBuilder();

		builder.ConnectionString(TestConnectionString);
		builder.ConnectionStringName("EventStore");

		options.ConnectionString.ShouldBeNull();
		builder.DataSourceFactoryFunc.ShouldBeNull();
		builder.DataSourceInstance.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBe("EventStore");
		builder.BindConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void BindConfiguration_ClearAll()
	{
		var (builder, options) = CreateBuilder();
		using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

		builder.DataSource(dataSource);
		builder.BindConfiguration("ES:Postgres");

		options.ConnectionString.ShouldBeNull();
		builder.DataSourceFactoryFunc.ShouldBeNull();
		builder.DataSourceInstance.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBe("ES:Postgres");
	}

	// --- Feature methods ---

	[Fact]
	public void EventStoreSchema_SetSchemaOnOptions()
	{
		var (builder, options) = CreateBuilder();
		builder.EventStoreSchema("es");
		options.EventStoreSchema.ShouldBe("es");
	}

	[Fact]
	public void EventStoreTable_SetTableOnOptions()
	{
		var (builder, options) = CreateBuilder();
		builder.EventStoreTable("domain_events");
		options.EventStoreTable.ShouldBe("domain_events");
	}

	[Fact]
	public void SnapshotStoreSchema_SetSchemaOnOptions()
	{
		var (builder, options) = CreateBuilder();
		builder.SnapshotStoreSchema("snapshots");
		options.SnapshotStoreSchema.ShouldBe("snapshots");
	}

	[Fact]
	public void SnapshotStoreTable_SetTableOnOptions()
	{
		var (builder, options) = CreateBuilder();
		builder.SnapshotStoreTable("aggregate_snapshots");
		options.SnapshotStoreTable.ShouldBe("aggregate_snapshots");
	}

	// --- Fluent chaining ---

	[Fact]
	public void AllMethods_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();

		var result = builder
			.ConnectionString(TestConnectionString)
			.EventStoreSchema("es")
			.EventStoreTable("events")
			.SnapshotStoreSchema("ss")
			.SnapshotStoreTable("snapshots");

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DataSourceFactory_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString));
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DataSource_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		using var dataSource = NpgsqlDataSource.Create(TestConnectionString);
		var result = builder.DataSource(dataSource);
		result.ShouldBeSameAs(builder);
	}

	// --- Constructor ---

	[Fact]
	public void Constructor_ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PostgresEventSourcingBuilder(null!));
	}
}
