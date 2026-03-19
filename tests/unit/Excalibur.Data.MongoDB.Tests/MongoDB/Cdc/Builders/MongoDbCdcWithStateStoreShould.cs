// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.MongoDB;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="IMongoDbCdcBuilder.WithStateStore"/> and
/// <see cref="IMongoDbCdcBuilder.BindConfiguration"/> methods added in Sprint 662 (CDC Phase 2).
/// Validates the Microsoft Change Feed Processor pattern: separate source/state connections.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbCdcWithStateStoreShould : UnitTestBase
{
	private const string SourceConnectionString = "mongodb://source-host:27017";
	private const string StateConnectionString = "mongodb://state-host:27017";

	// --- WithStateStore(string connectionString) ---

	[Fact]
	public void WithStateStore_ConnectionString_AcceptsValidValue()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.DatabaseName("TestDb")
				     .ProcessorId("test-processor")
				     .WithStateStore(StateConnectionString)));

		// Assert -- state store options should be registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(MongoDbCdcStateStoreOptions));
	}

	[Fact]
	public void WithStateStore_ConnectionString_RetainsSourceConnectionInOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.DatabaseName("TestDb")
				     .ProcessorId("test-processor")
				     .WithStateStore(StateConnectionString)));

		// Assert -- MongoDbCdcOptions still has source connection string
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>();
		options.Value.Connection.ConnectionString.ShouldBe(SourceConnectionString);
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
				builder.UseMongoDB(SourceConnectionString, mongo =>
					mongo.WithStateStore(invalidValue!))));
	}

	// --- WithStateStore(string connectionString, Action<ICdcStateStoreBuilder> configure) ---

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.DatabaseName("TestDb")
				     .ProcessorId("test-processor")
				     .WithStateStore(StateConnectionString, state =>
					     state.SchemaName("custom-db")
					          .TableName("custom-collection"))));

		// Assert -- state store options reflect custom database/collection
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>();
		stateOptions.Value.DatabaseName.ShouldBe("custom-db");
		stateOptions.Value.CollectionName.ShouldBe("custom-collection");
	}

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseMongoDB(SourceConnectionString, mongo =>
					mongo.WithStateStore(StateConnectionString, (Action<ICdcStateStoreBuilder>)null!))));
	}

	// --- WithStateStore(Func<IServiceProvider, IMongoClient> clientFactory) ---

	[Fact]
	public void WithStateStore_Factory_AcceptsValidFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		Func<IServiceProvider, IMongoClient> stateFactory =
			_ => A.Fake<IMongoClient>();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.DatabaseName("TestDb")
				     .ProcessorId("test-processor")
				     .WithStateStore(stateFactory)));

		// Assert -- state store options are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(MongoDbCdcStateStoreOptions));
	}

	[Fact]
	public void WithStateStore_Factory_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseMongoDB(SourceConnectionString, mongo =>
					mongo.WithStateStore((Func<IServiceProvider, IMongoClient>)null!))));
	}

	// --- WithStateStore(Func<...> factory, Action<ICdcStateStoreBuilder> configure) ---

	[Fact]
	public void WithStateStore_FactoryWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		Func<IServiceProvider, IMongoClient> stateFactory =
			_ => A.Fake<IMongoClient>();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.DatabaseName("TestDb")
				     .ProcessorId("test-processor")
				     .WithStateStore(stateFactory, state =>
					     state.SchemaName("audit-db")
					          .TableName("AuditState"))));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>();
		stateOptions.Value.DatabaseName.ShouldBe("audit-db");
		stateOptions.Value.CollectionName.ShouldBe("AuditState");
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseMongoDB(SourceConnectionString, mongo =>
					mongo.WithStateStore(
						(Func<IServiceProvider, IMongoClient>)null!,
						_ => { }))));
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, IMongoClient> stateFactory =
			_ => A.Fake<IMongoClient>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseMongoDB(SourceConnectionString, mongo =>
					mongo.WithStateStore(stateFactory, null!))));
	}

	// --- Backward compatibility: omitting WithStateStore ---

	[Fact]
	public void WithoutWithStateStore_SourceOptionsStillRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- no WithStateStore call
		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.DatabaseName("TestDb")
				     .ProcessorId("test-processor")));

		// Assert -- MongoDbCdcOptions are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(MongoDbCdcOptions));
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_SetsSourceBindConfigurationPath()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- BindConfiguration is accepted without error
		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.BindConfiguration("Cdc:MongoDB")));

		// Assert -- IConfigureOptions<MongoDbCdcOptions> registration exists from BindConfiguration
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(MongoDbCdcOptions));

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
				builder.UseMongoDB(SourceConnectionString, mongo =>
					mongo.BindConfiguration(invalidPath!))));
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
			builder.UseMongoDB(SourceConnectionString, mongo =>
				mongo.DatabaseName("TestDb")
				     .ProcessorId("test-processor")
				     .WithStateStore(StateConnectionString, state =>
					     state.BindConfiguration("Cdc:State"))));

		// Assert -- state store options BindConfiguration is wired
		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(MongoDbCdcStateStoreOptions));

		stateOptionsDescriptors.ShouldNotBeEmpty();
	}
}
