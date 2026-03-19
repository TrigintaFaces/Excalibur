// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerCdcBuilder.WithStateStore"/> and
/// <see cref="ISqlServerCdcBuilder.BindConfiguration"/> methods added in Sprint 661 (CDC Phase 1).
/// Validates the Microsoft Change Feed Processor pattern: separate source/state connections.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcWithStateStoreShould : UnitTestBase
{
	private const string SourceConnectionString = "Server=source-db;Database=SourceDb;Encrypt=false;TrustServerCertificate=true";
	private const string StateConnectionString = "Server=state-db;Database=StateDb;Encrypt=false;TrustServerCertificate=true";

	// --- WithStateStore(string connectionString) ---

	[Fact]
	public void WithStateStore_ConnectionString_RegistersSeparateStateStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.DatabaseName("TestDb")
				   .WithStateStore(StateConnectionString)));

		// Assert -- ICdcStateStore should be registered (uses state connection)
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void WithStateStore_ConnectionString_RetainsSourceConnectionInOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.DatabaseName("TestDb")
				   .WithStateStore(StateConnectionString)));

		// Assert -- SqlServerCdcOptions still has source connection string
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
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
				builder.UseSqlServer(SourceConnectionString, sql =>
					sql.WithStateStore(invalidValue!))));
	}

	// --- WithStateStore(string connectionString, Action<ICdcStateStoreBuilder> configure) ---

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.DatabaseName("TestDb")
				   .WithStateStore(StateConnectionString, state =>
						state.SchemaName("custom_schema")
							 .TableName("CustomState"))));

		// Assert -- state store options reflect custom schema/table
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("custom_schema");
		stateOptions.Value.TableName.ShouldBe("CustomState");
	}

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(SourceConnectionString, sql =>
					sql.WithStateStore(StateConnectionString, null!))));
	}

	// --- WithStateStore(Func<IServiceProvider, Func<SqlConnection>> stateConnectionFactory) ---

	[Fact]
	public void WithStateStore_Factory_RegistersSeparateStateStore()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, Func<SqlConnection>> stateFactory =
			_ => () => new SqlConnection(StateConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.DatabaseName("TestDb")
				   .WithStateStore(stateFactory)));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void WithStateStore_Factory_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(SourceConnectionString, sql =>
					sql.WithStateStore((Func<IServiceProvider, Func<SqlConnection>>)null!))));
	}

	// --- WithStateStore(Func<...> factory, Action<ICdcStateStoreBuilder> configure) ---

	[Fact]
	public void WithStateStore_FactoryWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, Func<SqlConnection>> stateFactory =
			_ => () => new SqlConnection(StateConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.DatabaseName("TestDb")
				   .WithStateStore(stateFactory, state =>
						state.SchemaName("audit")
							 .TableName("AuditState"))));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("audit");
		stateOptions.Value.TableName.ShouldBe("AuditState");
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(SourceConnectionString, sql =>
					sql.WithStateStore(
						(Func<IServiceProvider, Func<SqlConnection>>)null!,
						_ => { }))));
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, Func<SqlConnection>> stateFactory =
			_ => () => new SqlConnection(StateConnectionString);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(SourceConnectionString, sql =>
					sql.WithStateStore(stateFactory, null!))));
	}

	// --- Backward compatibility: omitting WithStateStore falls back to source ---

	[Fact]
	public void WithoutWithStateStore_FallsBackToSourceConnection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- no WithStateStore call
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.DatabaseName("TestDb")));

		// Assert -- ICdcStateStore is still registered (uses source connection)
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void WithoutWithStateStore_DefaultStateStoreOptionsHaveDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString));

		// Assert -- state store options have defaults from source config
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("Cdc");
		stateOptions.Value.TableName.ShouldBe("CdcProcessingState");
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_SetsSourceBindConfigurationPath()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- BindConfiguration is accepted without error
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.BindConfiguration("Cdc:SqlServer")));

		// Assert -- IOptions registration exists (BindConfiguration wires it)
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(SqlServerCdcOptions));

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
				builder.UseSqlServer(SourceConnectionString, sql =>
					sql.BindConfiguration(invalidPath!))));
	}

	// --- State store BindConfiguration via ICdcStateStoreBuilder ---

	[Fact]
	public void WithStateStore_StateStoreBindConfiguration_Accepted()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.DatabaseName("TestDb")
				   .WithStateStore(StateConnectionString, state =>
						state.BindConfiguration("Cdc:State"))));

		// Assert -- state store options BindConfiguration is wired
		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(SqlServerCdcStateStoreOptions));

		stateOptionsDescriptors.ShouldNotBeEmpty();
	}

	// --- Fluent chaining ---

	[Fact]
	public void FluentChain_AllNewMethodsCombineCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- exercise the full fluent chain without resolving services that need infra deps
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(SourceConnectionString, sql =>
				sql.SchemaName("cdc")
				   .StateTableName("CdcState")
				   .BatchSize(200)
				   .PollingInterval(TimeSpan.FromSeconds(10))
				   .CommandTimeout(TimeSpan.FromSeconds(60))
				   .DatabaseName("AuditDb")
				   .WithStateStore(StateConnectionString, state =>
						state.SchemaName("audit_state")
							 .TableName("AuditCheckpoints"))
				   .BindConfiguration("Cdc:Source")));

		// Assert -- verify service registrations exist
		services.ShouldContain(sd => sd.ServiceType == typeof(ICdcStateStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICdcRepository));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICdcProcessor));

		// Assert -- verify options configuration delegates were registered for both types
		var sqlOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(SqlServerCdcOptions));
		sqlOptionsDescriptors.ShouldNotBeEmpty();

		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(SqlServerCdcStateStoreOptions));
		stateOptionsDescriptors.ShouldNotBeEmpty();
	}

	// --- Factory overload also supports WithStateStore ---

	[Fact]
	public void UseSqlServerFactory_WithStateStore_RegistersSeparateState()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, Func<SqlConnection>> sourceFactory =
			_ => () => new SqlConnection(SourceConnectionString);
		Func<IServiceProvider, Func<SqlConnection>> stateFactory =
			_ => () => new SqlConnection(StateConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sourceFactory, sql =>
				sql.SchemaName("cdc")
				   .StateTableName("CdcState")
				   .DatabaseName("TestDb")
				   .WithStateStore(stateFactory, state =>
						state.SchemaName("state_schema")
							 .TableName("StateTable"))));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("state_schema");
		stateOptions.Value.TableName.ShouldBe("StateTable");
	}

	[Fact]
	public void UseSqlServerFactory_WithBindConfiguration_Accepted()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, Func<SqlConnection>> sourceFactory =
			_ => () => new SqlConnection(SourceConnectionString);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sourceFactory, sql =>
				sql.SchemaName("cdc")
				   .StateTableName("CdcState")
				   .BindConfiguration("Cdc:SqlServer")));

		// Assert
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(SqlServerCdcOptions));

		optionsDescriptors.ShouldNotBeEmpty();
	}
}
