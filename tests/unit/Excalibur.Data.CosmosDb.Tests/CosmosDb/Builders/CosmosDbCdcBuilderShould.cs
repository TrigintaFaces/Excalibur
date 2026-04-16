// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb.Builders;

/// <summary>
/// Unit tests for <see cref="CosmosDbCdcBuilder"/> — 5 connection overloads,
/// DatabaseId, ContainerId, ProcessorName, ChangeFeed, WithStateStore, BindConfiguration,
/// last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbCdcBuilderShould : UnitTestBase
{
	private const string TestConnectionString =
		"AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

	private static (CosmosDbCdcBuilder Builder, CosmosDbCdcOptions Options) CreateBuilder()
	{
		var options = new CosmosDbCdcOptions();
		var builder = new CosmosDbCdcBuilder(options);
		return (builder, options);
	}

	// --- Connection overloads (happy path) ---

	[Fact]
	public void ConnectionString_StoreValueOnOptions()
	{
		var (builder, options) = CreateBuilder();

		builder.ConnectionString(TestConnectionString);

		options.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void Endpoint_StoreEndpointAndAuthKeyOnBuilder()
	{
		var (builder, _) = CreateBuilder();

		builder.Endpoint("https://localhost:8081/", "dGVzdA==");

		builder.EndpointValue.ShouldBe("https://localhost:8081/");
		builder.AuthKeyValue.ShouldBe("dGVzdA==");
	}

	[Fact]
	public void Client_StoreClientInstanceOnBuilder()
	{
		var (builder, _) = CreateBuilder();
		var client = A.Fake<CosmosClient>();

		builder.Client(client);

		builder.ClientInstance.ShouldBe(client);
	}

	[Fact]
	public void ClientFactory_StoreFactoryOnBuilder()
	{
		var (builder, _) = CreateBuilder();
		Func<IServiceProvider, CosmosClient> factory = _ => A.Fake<CosmosClient>();

		builder.ClientFactory(factory);

		builder.ClientFactoryFunc.ShouldBe(factory);
	}

	[Fact]
	public void BindConfiguration_StorePathOnBuilder()
	{
		var (builder, _) = CreateBuilder();

		builder.BindConfiguration("CosmosDb:Cdc");

		builder.SourceBindConfigurationPath.ShouldBe("CosmosDb:Cdc");
	}

	// --- Feature methods (happy path) ---

	[Fact]
	public void DatabaseId_SetValueOnOptions()
	{
		var (builder, options) = CreateBuilder();

		builder.DatabaseId("my-database");

		options.DatabaseId.ShouldBe("my-database");
	}

	[Fact]
	public void ContainerId_SetValueOnOptions()
	{
		var (builder, options) = CreateBuilder();

		builder.ContainerId("my-container");

		options.ContainerId.ShouldBe("my-container");
	}

	[Fact]
	public void ProcessorName_SetValueOnOptions()
	{
		var (builder, options) = CreateBuilder();

		builder.ProcessorName("my-processor");

		options.ProcessorName.ShouldBe("my-processor");
	}

	[Fact]
	public void ChangeFeed_InvokeConfigureCallbackOnOptions()
	{
		var (builder, options) = CreateBuilder();

		builder.ChangeFeed(cf => cf.MaxBatchSize = 500);

		options.ChangeFeed.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void WithStateStore_StoreConfigureCallback()
	{
		var (builder, _) = CreateBuilder();
		Action<ICdcStateStoreBuilder> configure = _ => { };

		builder.WithStateStore(configure);

		builder.StateStoreConfigure.ShouldBe(configure);
	}

	// --- Last-wins semantics ---

	[Fact]
	public void Endpoint_ClearConnectionString()
	{
		var (builder, options) = CreateBuilder();

		builder.ConnectionString(TestConnectionString);
		builder.Endpoint("https://localhost:8081/", "dGVzdA==");

		options.ConnectionString.ShouldBe(null!);
		builder.ClientInstance.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		builder.EndpointValue.ShouldBe("https://localhost:8081/");
		builder.AuthKeyValue.ShouldBe("dGVzdA==");
	}

	[Fact]
	public void Client_ClearConnectionString()
	{
		var (builder, options) = CreateBuilder();
		var client = A.Fake<CosmosClient>();

		builder.ConnectionString(TestConnectionString);
		builder.Client(client);

		options.ConnectionString.ShouldBe(null!);
		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		builder.ClientInstance.ShouldBe(client);
	}

	[Fact]
	public void ClientFactory_ClearConnectionString()
	{
		var (builder, options) = CreateBuilder();
		Func<IServiceProvider, CosmosClient> factory = _ => A.Fake<CosmosClient>();

		builder.ConnectionString(TestConnectionString);
		builder.ClientFactory(factory);

		options.ConnectionString.ShouldBe(null!);
		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		builder.ClientInstance.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBe(factory);
	}

	[Fact]
	public void ConnectionString_ClearEndpoint()
	{
		var (builder, options) = CreateBuilder();

		builder.Endpoint("https://localhost:8081/", "dGVzdA==");
		builder.ConnectionString(TestConnectionString);

		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		builder.ClientInstance.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		options.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void ConnectionString_ClearClient()
	{
		var (builder, options) = CreateBuilder();
		var client = A.Fake<CosmosClient>();

		builder.Client(client);
		builder.ConnectionString(TestConnectionString);

		builder.ClientInstance.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		options.ConnectionString.ShouldBe(TestConnectionString);
	}

	// --- Fluent chaining ---

	[Fact]
	public void AllMethods_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();

		var result = builder
			.ConnectionString(TestConnectionString)
			.DatabaseId("my-db")
			.ContainerId("my-container")
			.ProcessorName("my-processor");

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void Endpoint_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.Endpoint("https://localhost:8081/", "dGVzdA==");
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void Client_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.Client(A.Fake<CosmosClient>());
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ClientFactory_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.ClientFactory(_ => A.Fake<CosmosClient>());
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void BindConfiguration_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.BindConfiguration("CosmosDb:Cdc");
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ChangeFeed_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.ChangeFeed(cf => cf.MaxBatchSize = 100);
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithStateStore_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.WithStateStore(_ => { });
		result.ShouldBeSameAs(builder);
	}

	// --- Constructor ---

	[Fact]
	public void Constructor_ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CosmosDbCdcBuilder(null!));
	}

	// --- Validation guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.ConnectionString(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Endpoint_ThrowOnInvalidEndpoint(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.Endpoint(invalidValue!, "dGVzdA=="));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Endpoint_ThrowOnInvalidAuthKey(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.Endpoint("https://localhost:8081/", invalidValue!));
	}

	[Fact]
	public void Client_ThrowOnNull()
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentNullException>(() => builder.Client(null!));
	}

	[Fact]
	public void ClientFactory_ThrowOnNull()
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentNullException>(() => builder.ClientFactory(null!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DatabaseId_ThrowOnInvalidValue(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.DatabaseId(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ContainerId_ThrowOnInvalidValue(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.ContainerId(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ProcessorName_ThrowOnInvalidValue(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.ProcessorName(invalidValue!));
	}

	[Fact]
	public void ChangeFeed_ThrowOnNull()
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentNullException>(() => builder.ChangeFeed(null!));
	}

	[Fact]
	public void WithStateStore_ThrowOnNull()
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentNullException>(() => builder.WithStateStore(null!));
	}
}
