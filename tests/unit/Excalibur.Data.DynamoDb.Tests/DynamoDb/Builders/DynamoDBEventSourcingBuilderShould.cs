// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;

namespace Excalibur.Data.Tests.DynamoDb.Builders;

/// <summary>
/// Unit tests for <see cref="DynamoDBEventSourcingBuilder"/> — 5 connection overloads,
/// feature methods, last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.EventSourcing)]
[Trait("Database", "DynamoDB")]
public sealed class DynamoDBEventSourcingBuilderShould : UnitTestBase
{
    private static DynamoDBEventSourcingBuilder CreateBuilder() => new();

    // --- Connection overloads (happy path) ---

    [Fact]
    public void ServiceUrl_StoreValue()
    {
        var builder = CreateBuilder();
        builder.ServiceUrl("http://localhost:8000");
        builder.ServiceUrlValue.ShouldBe("http://localhost:8000");
    }

    [Fact]
    public void Region_StoreValue()
    {
        var builder = CreateBuilder();
        builder.Region(RegionEndpoint.USEast1);
        builder.RegionValue.ShouldBe(RegionEndpoint.USEast1);
    }

    [Fact]
    public void Client_StoreInstance()
    {
        var builder = CreateBuilder();
        var client = A.Fake<IAmazonDynamoDB>();
        builder.Client(client);
        builder.ClientInstance.ShouldBeSameAs(client);
    }

    [Fact]
    public void ClientFactory_StoreFactory()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();
        builder.ClientFactory(factory);
        builder.ClientFactoryFunc.ShouldBeSameAs(factory);
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("EventSourcing:DynamoDB");
        builder.BindConfigurationPath.ShouldBe("EventSourcing:DynamoDB");
    }

    // --- Feature methods ---

    [Fact]
    public void TableName_StoreValue()
    {
        var builder = CreateBuilder();
        builder.TableName("events");
        builder.TableNameValue.ShouldBe("events");
    }

    [Fact]
    public void TablePrefix_StoreValue()
    {
        var builder = CreateBuilder();
        builder.TablePrefix("prod-");
        builder.TablePrefixValue.ShouldBe("prod-");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void Client_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();
        builder.ServiceUrl("http://localhost:8000");
        builder.Client(A.Fake<IAmazonDynamoDB>());

        builder.ServiceUrlValue.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientInstance.ShouldNotBeNull();
    }

    [Fact]
    public void ServiceUrl_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();
        builder.Client(A.Fake<IAmazonDynamoDB>());
        builder.ServiceUrl("http://localhost:8000");

        builder.ClientInstance.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void Region_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();
        builder.ClientFactory(_ => A.Fake<IAmazonDynamoDB>());
        builder.Region(RegionEndpoint.EUWest1);

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.ServiceUrlValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ClientFactory_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();
        builder.Region(RegionEndpoint.USEast1);
        builder.ClientFactory(factory);

        builder.ServiceUrlValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();
        builder.Client(A.Fake<IAmazonDynamoDB>());
        builder.BindConfiguration("ES:DynamoDB");

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.ServiceUrlValue.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
    }

    // --- Null/invalid argument guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceUrl_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().ServiceUrl(value!));
    }

    [Fact]
    public void Region_ThrowOnNull()
    {
        Should.Throw<ArgumentNullException>(() => CreateBuilder().Region(null!));
    }

    [Fact]
    public void Client_ThrowOnNull()
    {
        Should.Throw<ArgumentNullException>(() => CreateBuilder().Client(null!));
    }

    [Fact]
    public void ClientFactory_ThrowOnNull()
    {
        Should.Throw<ArgumentNullException>(() => CreateBuilder().ClientFactory(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().BindConfiguration(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TableName_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().TableName(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TablePrefix_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().TablePrefix(value!));
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder
            .ServiceUrl("http://localhost:8000")
            .TableName("events")
            .TablePrefix("dev-");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.Client(A.Fake<IAmazonDynamoDB>());
        result.ShouldBeSameAs(builder);
    }

    // --- Feature methods preserve connection state ---

    [Fact]
    public void TableName_PreserveConnectionState()
    {
        var builder = CreateBuilder();
        builder.ServiceUrl("http://localhost:8000");
        builder.TableName("events");
        builder.ServiceUrlValue.ShouldBe("http://localhost:8000");
    }
}
