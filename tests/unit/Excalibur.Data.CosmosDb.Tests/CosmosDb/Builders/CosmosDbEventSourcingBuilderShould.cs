// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb.Builders;

/// <summary>
/// Unit tests for <see cref="CosmosDbEventSourcingBuilder"/> — 5 connection overloads,
/// DatabaseName, ContainerName, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbEventSourcingBuilderShould : UnitTestBase
{
	private const string TestConnectionString =
		"AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

	private static (CosmosDbEventSourcingBuilder Builder, CosmosDbEventStoreOptions Options) CreateBuilder()
	{
		var options = new CosmosDbEventStoreOptions();
		var builder = new CosmosDbEventSourcingBuilder(options);
		return (builder, options);
	}

	// --- Connection overloads (happy path) ---

	[Fact]
	public void ConnectionString_StoreValueOnBuilder()
	{
		var (builder, _) = CreateBuilder();

		builder.ConnectionString(TestConnectionString);

		builder.ConnectionStringValue.ShouldBe(TestConnectionString);
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

		builder.BindConfiguration("CosmosDb:EventSourcing");

		builder.BindConfigurationPath.ShouldBe("CosmosDb:EventSourcing");
	}

	// --- Feature methods (happy path) ---

	[Fact]
	public void DatabaseName_StoreValueOnBuilder()
	{
		var (builder, _) = CreateBuilder();

		builder.DatabaseName("my-database");

		builder.DatabaseNameValue.ShouldBe("my-database");
	}

	[Fact]
	public void ContainerName_SetValueOnOptions()
	{
		var (builder, options) = CreateBuilder();

		builder.ContainerName("my-events");

		options.EventsContainerName.ShouldBe("my-events");
	}

	// --- Last-wins semantics ---

	[Fact]
	public void Endpoint_ClearConnectionString()
	{
		var (builder, _) = CreateBuilder();

		builder.ConnectionString(TestConnectionString);
		builder.Endpoint("https://localhost:8081/", "dGVzdA==");

		builder.ConnectionStringValue.ShouldBeNull();
		builder.ClientInstance.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
		builder.EndpointValue.ShouldBe("https://localhost:8081/");
		builder.AuthKeyValue.ShouldBe("dGVzdA==");
	}

	[Fact]
	public void Client_ClearConnectionString()
	{
		var (builder, _) = CreateBuilder();
		var client = A.Fake<CosmosClient>();

		builder.ConnectionString(TestConnectionString);
		builder.Client(client);

		builder.ConnectionStringValue.ShouldBeNull();
		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
		builder.ClientInstance.ShouldBe(client);
	}

	[Fact]
	public void ClientFactory_ClearConnectionString()
	{
		var (builder, _) = CreateBuilder();
		Func<IServiceProvider, CosmosClient> factory = _ => A.Fake<CosmosClient>();

		builder.ConnectionString(TestConnectionString);
		builder.ClientFactory(factory);

		builder.ConnectionStringValue.ShouldBeNull();
		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		builder.ClientInstance.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBe(factory);
	}

	[Fact]
	public void ConnectionString_ClearEndpoint()
	{
		var (builder, _) = CreateBuilder();

		builder.Endpoint("https://localhost:8081/", "dGVzdA==");
		builder.ConnectionString(TestConnectionString);

		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		builder.ClientInstance.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBeNull();
		builder.ConnectionStringValue.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void BindConfiguration_ClearAll()
	{
		var (builder, _) = CreateBuilder();
		var client = A.Fake<CosmosClient>();

		builder.Client(client);
		builder.BindConfiguration("CosmosDb:EventSourcing");

		builder.ConnectionStringValue.ShouldBeNull();
		builder.EndpointValue.ShouldBeNull();
		builder.AuthKeyValue.ShouldBeNull();
		builder.ClientInstance.ShouldBeNull();
		builder.ClientFactoryFunc.ShouldBeNull();
		builder.BindConfigurationPath.ShouldBe("CosmosDb:EventSourcing");
	}

	// --- Fluent chaining ---

	[Fact]
	public void AllConnectionMethods_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();

		var result = builder
			.ConnectionString(TestConnectionString)
			.DatabaseName("my-db")
			.ContainerName("my-events");

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
		var result = builder.BindConfiguration("CosmosDb:EventSourcing");
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DatabaseName_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.DatabaseName("my-db");
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ContainerName_ReturnBuilderForChaining()
	{
		var (builder, _) = CreateBuilder();
		var result = builder.ContainerName("my-events");
		result.ShouldBeSameAs(builder);
	}

	// --- Constructor ---

	[Fact]
	public void Constructor_ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CosmosDbEventSourcingBuilder(null!));
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
	public void DatabaseName_ThrowOnInvalidValue(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.DatabaseName(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ContainerName_ThrowOnInvalidValue(string? invalidValue)
	{
		var (builder, _) = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.ContainerName(invalidValue!));
	}
}
