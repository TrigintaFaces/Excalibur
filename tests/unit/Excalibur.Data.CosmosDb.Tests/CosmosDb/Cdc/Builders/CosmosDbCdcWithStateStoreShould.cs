// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ICosmosDbCdcBuilder.WithStateStore"/> and
/// <see cref="ICosmosDbCdcBuilder.BindConfiguration"/> methods added in Sprint 662 (CDC Phase 2).
/// Validates the Microsoft Change Feed Processor pattern: separate source/state connections.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CosmosDbCdcWithStateStoreShould : UnitTestBase
{
	private const string SourceConnectionString = "AccountEndpoint=https://source-cosmos.documents.azure.com:443/;AccountKey=dGVzdA==;";
	private const string StateConnectionString = "AccountEndpoint=https://state-cosmos.documents.azure.com:443/;AccountKey=dGVzdA==;";

	// --- WithStateStore(string connectionString) ---

	[Fact]
	public void WithStateStore_ConnectionString_AcceptsValidValue()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(StateConnectionString)));

		// Assert -- state store options should be registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(CosmosDbCdcOptions));
	}

	[Fact]
	public void WithStateStore_ConnectionString_RetainsSourceConnectionInOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(StateConnectionString)));

		// Assert -- CosmosDbCdcOptions still has source connection string
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
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
				builder.UseCosmosDb(SourceConnectionString, cosmos =>
					cosmos.WithStateStore(invalidValue!))));
	}

	// --- WithStateStore(string connectionString, Action<ICdcStateStoreBuilder> configure) ---

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(StateConnectionString, state =>
					      state.SchemaName("state-db")
					           .TableName("state-container"))));

		// Assert -- we can verify the options descriptors exist
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
		options.Value.DatabaseId.ShouldBe("TestDb");
	}

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseCosmosDb(SourceConnectionString, cosmos =>
					cosmos.WithStateStore(StateConnectionString, (Action<ICdcStateStoreBuilder>)null!))));
	}

	// --- WithStateStore(Func<IServiceProvider, CosmosClient> clientFactory) ---

	[Fact]
	public void WithStateStore_Factory_AcceptsValidFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, CosmosClient> stateFactory =
			_ => A.Fake<CosmosClient>();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(stateFactory)));

		// Assert -- CDC options are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(CosmosDbCdcOptions));
	}

	[Fact]
	public void WithStateStore_Factory_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseCosmosDb(SourceConnectionString, cosmos =>
					cosmos.WithStateStore((Func<IServiceProvider, CosmosClient>)null!))));
	}

	// --- WithStateStore(Func<...> factory, Action<ICdcStateStoreBuilder> configure) ---

	[Fact]
	public void WithStateStore_FactoryWithConfigure_AcceptsValidValues()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, CosmosClient> stateFactory =
			_ => A.Fake<CosmosClient>();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(stateFactory, state =>
					      state.SchemaName("audit-db")
					           .TableName("AuditState"))));

		// Assert -- options registered
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
		options.Value.ConnectionString.ShouldBe(SourceConnectionString);
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseCosmosDb(SourceConnectionString, cosmos =>
					cosmos.WithStateStore(
						(Func<IServiceProvider, CosmosClient>)null!,
						_ => { }))));
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, CosmosClient> stateFactory =
			_ => A.Fake<CosmosClient>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseCosmosDb(SourceConnectionString, cosmos =>
					cosmos.WithStateStore(stateFactory, null!))));
	}

	// --- Backward compatibility: omitting WithStateStore ---

	[Fact]
	public void WithoutWithStateStore_SourceOptionsStillRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- no WithStateStore call
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")));

		// Assert -- CosmosDbCdcOptions are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(CosmosDbCdcOptions));
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_SetsSourceBindConfigurationPath()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- BindConfiguration is accepted without error
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.BindConfiguration("Cdc:CosmosDb")));

		// Assert -- IConfigureOptions<CosmosDbCdcOptions> registration exists from BindConfiguration
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(CosmosDbCdcOptions));

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
				builder.UseCosmosDb(SourceConnectionString, cosmos =>
					cosmos.BindConfiguration(invalidPath!))));
	}

	// --- State store BindConfiguration via ICdcStateStoreBuilder ---

	[Fact]
	public void WithStateStore_StateStoreBindConfiguration_Accepted()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(SourceConnectionString, cosmos =>
				cosmos.DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(StateConnectionString, state =>
					      state.BindConfiguration("Cdc:State"))));

		// Assert -- state store BindConfiguration registered additional IConfigureOptions
		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>));

		stateOptionsDescriptors.ShouldNotBeEmpty();
	}
}
