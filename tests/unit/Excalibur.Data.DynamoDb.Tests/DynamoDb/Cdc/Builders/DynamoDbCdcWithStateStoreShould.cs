// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

using Excalibur.Cdc;
using Excalibur.Cdc.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="IDynamoDbCdcBuilder.WithStateStore"/> and
/// <see cref="IDynamoDbCdcBuilder.BindConfiguration"/> methods added in Sprint 662 (CDC Phase 2).
/// DynamoDB does not use connection strings; only factory-based overloads are provided.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DynamoDbCdcWithStateStoreShould : UnitTestBase
{
	// --- WithStateStore(Func<IServiceProvider, IAmazonDynamoDB> clientFactory) ---

	[Fact]
	public void WithStateStore_Factory_AcceptsValidFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, IAmazonDynamoDB> stateFactory =
			_ => A.Fake<IAmazonDynamoDB>();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseDynamoDb(dynamo =>
				dynamo.TableName("Orders")
				      .ProcessorName("order-cdc")
				      .WithStateStore(stateFactory)));

		// Assert -- DynamoDbCdcOptions are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(DynamoDbCdcOptions));
	}

	[Fact]
	public void WithStateStore_Factory_RegistersStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, IAmazonDynamoDB> stateFactory =
			_ => A.Fake<IAmazonDynamoDB>();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseDynamoDb(dynamo =>
				dynamo.TableName("Orders")
				      .ProcessorName("order-cdc")
				      .WithStateStore(stateFactory)));

		// Assert -- DynamoDbCdcStateStoreOptions are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(DynamoDbCdcStateStoreOptions));
	}

	[Fact]
	public void WithStateStore_Factory_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseDynamoDb(dynamo =>
					dynamo.WithStateStore((Func<IServiceProvider, IAmazonDynamoDB>)null!))));
	}

	// --- WithStateStore(Func<...> factory, Action<ICdcStateStoreBuilder> configure) ---

	[Fact]
	public void WithStateStore_FactoryWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, IAmazonDynamoDB> stateFactory =
			_ => A.Fake<IAmazonDynamoDB>();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseDynamoDb(dynamo =>
				dynamo.TableName("Orders")
				      .ProcessorName("order-cdc")
				      .WithStateStore(stateFactory, state =>
					      state.TableName("cdc-checkpoints"))));

		// Assert -- state store options have custom table name
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<DynamoDbCdcStateStoreOptions>>();
		stateOptions.Value.TableName.ShouldBe("cdc-checkpoints");
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseDynamoDb(dynamo =>
					dynamo.WithStateStore(
						(Func<IServiceProvider, IAmazonDynamoDB>)null!,
						_ => { }))));
	}

	[Fact]
	public void WithStateStore_FactoryWithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, IAmazonDynamoDB> stateFactory =
			_ => A.Fake<IAmazonDynamoDB>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseDynamoDb(dynamo =>
					dynamo.WithStateStore(stateFactory, null!))));
	}

	// --- Backward compatibility: omitting WithStateStore ---

	[Fact]
	public void WithoutWithStateStore_SourceOptionsStillRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- no WithStateStore call
		services.AddCdcProcessor(builder =>
			builder.UseDynamoDb(dynamo =>
				dynamo.TableName("Orders")
				      .ProcessorName("order-cdc")));

		// Assert -- DynamoDbCdcOptions are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(DynamoDbCdcOptions));
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_SetsSourceBindConfigurationPath()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- BindConfiguration is accepted without error
		services.AddCdcProcessor(builder =>
			builder.UseDynamoDb(dynamo =>
				dynamo.BindConfiguration("Cdc:DynamoDb")));

		// Assert -- IConfigureOptions<DynamoDbCdcOptions> registration exists from BindConfiguration
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(DynamoDbCdcOptions));

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
				builder.UseDynamoDb(dynamo =>
					dynamo.BindConfiguration(invalidPath!))));
	}
}
