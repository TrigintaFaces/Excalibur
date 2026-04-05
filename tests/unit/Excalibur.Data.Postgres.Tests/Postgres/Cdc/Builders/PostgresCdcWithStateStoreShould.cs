// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Postgres;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="IPostgresCdcBuilder.WithStateStore"/> and
/// <see cref="IPostgresCdcBuilder.BindConfiguration"/> methods.
/// Validates the Microsoft Change Feed Processor pattern: separate source/state connections.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcWithStateStoreShould : UnitTestBase
{
	private const string SourceConnectionString = "Host=source-db;Database=SourceDb;Username=test;Password=test;";
	private const string StateConnectionString = "Host=state-db;Database=StateDb;Username=test;Password=test;";

	// --- WithStateStore(Action<ICdcStateStoreBuilder> configure) with ConnectionString ---

	[Fact]
	public void WithStateStore_ConnectionString_RegistersSeparateStateStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .WithStateStore(state => state.ConnectionString(StateConnectionString))));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostgresCdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void WithStateStore_ConnectionString_RetainsSourceConnectionInOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .WithStateStore(state => state.ConnectionString(StateConnectionString))));

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.ConnectionString.ShouldBe(SourceConnectionString);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithStateStore_ConnectionString_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UsePostgres(pg =>
					pg.ConnectionString(SourceConnectionString)
					   .WithStateStore(state => state.ConnectionString(invalidValue!)))));
	}

	// --- WithStateStore with SchemaName/TableName ---

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .WithStateStore(state =>
					state.ConnectionString(StateConnectionString)
						 .SchemaName("custom_schema")
						 .TableName("custom_state"))));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("custom_schema");
		stateOptions.Value.TableName.ShouldBe("custom_state");
	}

	[Fact]
	public void WithStateStore_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UsePostgres(pg =>
					pg.ConnectionString(SourceConnectionString)
					   .WithStateStore(null!))));
	}

	// --- StateConnectionFactory ---

	[Fact]
	public void StateConnectionFactory_RegistersSeparateStateStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		Func<IServiceProvider, Func<NpgsqlConnection>> stateFactory =
			_ => () => new NpgsqlConnection(StateConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .StateConnectionFactory(stateFactory)));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostgresCdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void StateConnectionFactory_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UsePostgres(pg =>
					pg.ConnectionString(SourceConnectionString)
					   .StateConnectionFactory(null!))));
	}

	// --- StateConnectionFactory + WithStateStore combined ---

	[Fact]
	public void StateConnectionFactory_WithStateStore_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		Func<IServiceProvider, Func<NpgsqlConnection>> stateFactory =
			_ => () => new NpgsqlConnection(StateConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .StateConnectionFactory(stateFactory)
				   .WithStateStore(state =>
					state.SchemaName("audit")
						 .TableName("audit_state"))));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("audit");
		stateOptions.Value.TableName.ShouldBe("audit_state");
	}

	[Fact]
	public void StateConnectionFactory_WithStateStore_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UsePostgres(pg =>
					pg.ConnectionString(SourceConnectionString)
					   .StateConnectionFactory(null!))));
	}

	// --- Backward compatibility: omitting WithStateStore falls back to source ---

	[Fact]
	public void WithoutWithStateStore_FallsBackToSourceConnection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- no WithStateStore call
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg => pg.ConnectionString(SourceConnectionString)));

		// Assert -- state store is still registered (uses source connection)
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostgresCdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void WithoutWithStateStore_DefaultStateStoreOptionsHaveDefaults()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg => pg.ConnectionString(SourceConnectionString)));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("excalibur");
		stateOptions.Value.TableName.ShouldBe("cdc_state");
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_SetsSourceBindConfigurationPath()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .BindConfiguration("Cdc:Postgres")));

		// Assert
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(PostgresCdcOptions));

		optionsDescriptors.ShouldNotBeEmpty();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowsOnInvalidSectionPath(string? invalidPath)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UsePostgres(pg =>
					pg.ConnectionString(SourceConnectionString)
					   .BindConfiguration(invalidPath!))));
	}

	// --- State store BindConfiguration via ICdcStateStoreBuilder ---

	[Fact]
	public void WithStateStore_StateStoreBindConfiguration_Accepted()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .WithStateStore(state =>
					state.ConnectionString(StateConnectionString)
						 .BindConfiguration("Cdc:State"))));

		// Assert
		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(PostgresCdcStateStoreOptions));

		stateOptionsDescriptors.ShouldNotBeEmpty();
	}

	// --- Fluent chaining ---

	[Fact]
	public void FluentChain_AllNewMethodsCombineCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- exercise full fluent chain
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(SourceConnectionString)
				   .SchemaName("custom")
				   .StateTableName("custom_state")
				   .ReplicationSlotName("my_slot")
				   .PublicationName("my_pub")
				   .BatchSize(500)
				   .PollingInterval(TimeSpan.FromSeconds(2))
				   .WithStateStore(state =>
						state.ConnectionString(StateConnectionString)
							 .SchemaName("state_schema")
							 .TableName("state_table"))
				   .BindConfiguration("Cdc:Postgres")));

		// Assert -- verify service registrations exist
		services.ShouldContain(sd => sd.ServiceType == typeof(IPostgresCdcStateStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IPostgresCdcProcessor));

		// Assert -- verify options configuration delegates were registered for both types
		var pgOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(PostgresCdcOptions));
		pgOptionsDescriptors.ShouldNotBeEmpty();

		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(PostgresCdcStateStoreOptions));
		stateOptionsDescriptors.ShouldNotBeEmpty();
	}

	// --- Factory overload also supports WithStateStore ---

	[Fact]
	public void UsePostgresFactory_WithStateStore_RegistersSeparateState()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		Func<IServiceProvider, Func<NpgsqlConnection>> sourceFactory =
			_ => () => new NpgsqlConnection(SourceConnectionString);
		Func<IServiceProvider, Func<NpgsqlConnection>> stateFactory =
			_ => () => new NpgsqlConnection(StateConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionFactory(sourceFactory)
				   .StateConnectionFactory(stateFactory)
				   .WithStateStore(state =>
					state.SchemaName("state_schema")
						 .TableName("state_table"))));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("state_schema");
		stateOptions.Value.TableName.ShouldBe("state_table");
	}

	[Fact]
	public void UsePostgresFactory_WithBindConfiguration_Accepted()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		Func<IServiceProvider, Func<NpgsqlConnection>> sourceFactory =
			_ => () => new NpgsqlConnection(SourceConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionFactory(sourceFactory)
				   .BindConfiguration("Cdc:Postgres")));

		// Assert
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(PostgresCdcOptions));

		optionsDescriptors.ShouldNotBeEmpty();
	}
}
