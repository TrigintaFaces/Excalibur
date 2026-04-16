// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="SqlServerEventSourcingBuilder"/> — connection overloads,
/// feature methods, last-wins semantics, and fluent chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventSourcingBuilderShould : UnitTestBase
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	private static (SqlServerEventSourcingBuilder Builder, SqlServerEventSourcingOptions Options) CreateBuilder()
	{
		var options = new SqlServerEventSourcingOptions();
		var builder = new SqlServerEventSourcingBuilder(options);
		return (builder, options);
	}

	// --- Connection overloads (happy path) ---

	[Fact]
	public void ConnectionString_SetConnectionStringOnOptions()
	{
		// Arrange
		var (builder, options) = CreateBuilder();

		// Act
		builder.ConnectionString(TestConnectionString);

		// Assert
		options.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void ConnectionFactory_StoreFactory()
	{
		// Arrange
		var (builder, _) = CreateBuilder();
		Func<IServiceProvider, Func<SqlConnection>> factory = _ => () => new SqlConnection();

		// Act
		builder.ConnectionFactory(factory);

		// Assert
		builder.ConnectionFactoryFunc.ShouldBe(factory);
	}

	[Fact]
	public void ConnectionStringName_StoreName()
	{
		// Arrange
		var (builder, _) = CreateBuilder();

		// Act
		builder.ConnectionStringName("EventStore");

		// Assert
		builder.ConnectionStringNameValue.ShouldBe("EventStore");
	}

	[Fact]
	public void BindConfiguration_StorePath()
	{
		// Arrange
		var (builder, _) = CreateBuilder();

		// Act
		builder.BindConfiguration("EventSourcing:SqlServer");

		// Assert
		builder.BindConfigurationPath.ShouldBe("EventSourcing:SqlServer");
	}

	// --- Last-wins semantics ---

	[Fact]
	public void ConnectionFactory_ClearConnectionString_WhenCalledAfterConnectionString()
	{
		// Arrange
		var (builder, options) = CreateBuilder();
		Func<IServiceProvider, Func<SqlConnection>> factory = _ => () => new SqlConnection();

		// Act — set ConnectionString first, then ConnectionFactory (last-wins)
		builder.ConnectionString(TestConnectionString);
		builder.ConnectionFactory(factory);

		// Assert — ConnectionString should be cleared
		options.ConnectionString.ShouldBeNull();
		builder.ConnectionFactoryFunc.ShouldBe(factory);
		builder.ConnectionStringNameValue.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_ClearFactory_WhenCalledAfterConnectionFactory()
	{
		// Arrange
		var (builder, options) = CreateBuilder();
		Func<IServiceProvider, Func<SqlConnection>> factory = _ => () => new SqlConnection();

		// Act — set ConnectionFactory first, then ConnectionString (last-wins)
		builder.ConnectionFactory(factory);
		builder.ConnectionString(TestConnectionString);

		// Assert — Factory should be cleared
		options.ConnectionString.ShouldBe(TestConnectionString);
		builder.ConnectionFactoryFunc.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void ConnectionStringName_ClearOthers_WhenCalledAfterConnectionString()
	{
		// Arrange
		var (builder, options) = CreateBuilder();

		// Act — set ConnectionString first, then ConnectionStringName (last-wins)
		builder.ConnectionString(TestConnectionString);
		builder.ConnectionStringName("EventStore");

		// Assert — ConnectionString and other alternatives should be cleared
		options.ConnectionString.ShouldBeNull();
		builder.ConnectionFactoryFunc.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBe("EventStore");
		builder.BindConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void BindConfiguration_ClearOthers_WhenCalledAfterConnectionFactory()
	{
		// Arrange
		var (builder, options) = CreateBuilder();
		Func<IServiceProvider, Func<SqlConnection>> factory = _ => () => new SqlConnection();

		// Act — set ConnectionFactory first, then BindConfiguration (last-wins)
		builder.ConnectionFactory(factory);
		builder.BindConfiguration("EventSourcing:SqlServer");

		// Assert — Factory and other alternatives should be cleared
		options.ConnectionString.ShouldBeNull();
		builder.ConnectionFactoryFunc.ShouldBeNull();
		builder.ConnectionStringNameValue.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBe("EventSourcing:SqlServer");
	}

	// --- Feature methods ---

	[Fact]
	public void EventStoreSchema_SetSchemaOnOptions()
	{
		// Arrange
		var (builder, options) = CreateBuilder();

		// Act
		builder.EventStoreSchema("es");

		// Assert
		options.EventStoreSchema.ShouldBe("es");
	}

	[Fact]
	public void EventStoreTable_SetTableOnOptions()
	{
		// Arrange
		var (builder, options) = CreateBuilder();

		// Act
		builder.EventStoreTable("DomainEvents");

		// Assert
		options.EventStoreTable.ShouldBe("DomainEvents");
	}

	[Fact]
	public void SnapshotStoreSchema_SetSchemaOnOptions()
	{
		// Arrange
		var (builder, options) = CreateBuilder();

		// Act
		builder.SnapshotStoreSchema("snapshots");

		// Assert
		options.SnapshotStoreSchema.ShouldBe("snapshots");
	}

	[Fact]
	public void SnapshotStoreTable_SetTableOnOptions()
	{
		// Arrange
		var (builder, options) = CreateBuilder();

		// Act
		builder.SnapshotStoreTable("AggregateSnapshots");

		// Assert
		options.SnapshotStoreTable.ShouldBe("AggregateSnapshots");
	}

	// --- Fluent chaining ---

	[Fact]
	public void AllMethods_ReturnBuilderForChaining()
	{
		// Arrange
		var (builder, _) = CreateBuilder();

		// Act & Assert — every method returns the same builder instance
		var result = builder
			.ConnectionString(TestConnectionString)
			.EventStoreSchema("es")
			.EventStoreTable("Events")
			.SnapshotStoreSchema("ss")
			.SnapshotStoreTable("Snapshots");

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ConnectionFactory_ReturnBuilderForChaining()
	{
		// Arrange
		var (builder, _) = CreateBuilder();

		// Act
		var result = builder.ConnectionFactory(_ => () => new SqlConnection());

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ConnectionStringName_ReturnBuilderForChaining()
	{
		// Arrange
		var (builder, _) = CreateBuilder();

		// Act
		var result = builder.ConnectionStringName("EventStore");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void BindConfiguration_ReturnBuilderForChaining()
	{
		// Arrange
		var (builder, _) = CreateBuilder();

		// Act
		var result = builder.BindConfiguration("ES:SqlServer");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	// --- Constructor validation ---

	[Fact]
	public void Constructor_ThrowOnNullOptions()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerEventSourcingBuilder(null!));
	}

	// --- Entry point integration ---

	[Fact]
	public void UseSqlServer_RegisterEventStore_WhenConnectionStringProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));

		// Assert — EventStore and SnapshotStore should be registered
		services.ShouldContain(sd => sd.ServiceType == typeof(SqlServerEventStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(SqlServerSnapshotStore));
	}

	[Fact]
	public void UseSqlServer_RegisterValidator_WhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));

		// Assert — ValidateOnStart validator should be registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<SqlServerEventSourcingOptions>));
	}
}
