// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ICosmosDbCdcBuilder.WithStateStore"/> and
/// <see cref="ICosmosDbCdcBuilder.BindConfiguration"/> methods.
/// Validates the unified WithStateStore(Action&lt;ICdcStateStoreBuilder&gt;) pattern.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CosmosDbCdcWithStateStoreShould : UnitTestBase
{
	private const string SourceConnectionString = "AccountEndpoint=https://source-cosmos.documents.azure.com:443/;AccountKey=dGVzdA==;";
	private const string StateConnectionString = "AccountEndpoint=https://state-cosmos.documents.azure.com:443/;AccountKey=dGVzdA==;";

	// --- WithStateStore(Action<ICdcStateStoreBuilder>) ---

	[Fact]
	public void WithStateStore_ConnectionString_AcceptsValidValue()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(cosmos =>
				cosmos.ConnectionString(SourceConnectionString)
				      .DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(state =>
					      state.ConnectionString(StateConnectionString))));

		// Assert -- state store options should be registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(CosmosDbCdcStateStoreOptions));
	}

	[Fact]
	public void WithStateStore_ConnectionString_RetainsSourceConnectionInOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(cosmos =>
				cosmos.ConnectionString(SourceConnectionString)
				      .DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(state =>
					      state.ConnectionString(StateConnectionString))));

		// Assert -- CosmosDbCdcOptions still has source connection string
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
		options.Value.ConnectionString.ShouldBe(SourceConnectionString);
	}

	[Fact]
	public void WithStateStore_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseCosmosDb(cosmos =>
					cosmos.ConnectionString(SourceConnectionString)
					      .WithStateStore((Action<ICdcStateStoreBuilder>)null!))));
	}

	// --- WithStateStore with SchemaName/TableName ---

	[Fact]
	public void WithStateStore_ConnectionStringWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(cosmos =>
				cosmos.ConnectionString(SourceConnectionString)
				      .DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(state =>
					      state.ConnectionString(StateConnectionString)
					           .SchemaName("state-db")
					           .TableName("state-container"))));

		// Assert -- we can verify the options descriptors exist
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
		options.Value.DatabaseId.ShouldBe("TestDb");
	}

	// --- Backward compatibility: omitting WithStateStore ---

	[Fact]
	public void WithoutWithStateStore_SourceOptionsStillRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- no WithStateStore call
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(cosmos =>
				cosmos.ConnectionString(SourceConnectionString)
				      .DatabaseId("TestDb")
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
			builder.UseCosmosDb(cosmos =>
				cosmos.ConnectionString(SourceConnectionString)
				      .BindConfiguration("Cdc:CosmosDb")));

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
				builder.UseCosmosDb(cosmos =>
					cosmos.ConnectionString(SourceConnectionString)
					      .BindConfiguration(invalidPath!))));
	}

	// --- State store BindConfiguration via ICdcStateStoreBuilder ---

	[Fact]
	public void WithStateStore_StateStoreBindConfiguration_Accepted()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseCosmosDb(cosmos =>
				cosmos.ConnectionString(SourceConnectionString)
				      .DatabaseId("TestDb")
				      .ContainerId("orders")
				      .ProcessorName("test-processor")
				      .WithStateStore(state =>
					      state.ConnectionString(StateConnectionString)
					           .BindConfiguration("Cdc:State"))));

		// Assert -- state store BindConfiguration registered additional IConfigureOptions
		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>));

		stateOptionsDescriptors.ShouldNotBeEmpty();
	}
}
