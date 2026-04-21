// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;

namespace Excalibur.Data.Tests.DynamoDb.Builders;

/// <summary>
/// Unit tests for <see cref="DynamoDBDataBuilder"/> — 5 connection overloads,
/// feature methods, last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait("Database", "DynamoDB")]
public sealed class DynamoDBDataBuilderShould : UnitTestBase
{
    private static DynamoDBDataBuilder CreateBuilder() => new();

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

        builder.BindConfiguration("Data:DynamoDB");

        builder.BindConfigurationPath.ShouldBe("Data:DynamoDB");
    }

    // --- Feature methods ---

    [Fact]
    public void TableName_StoreValue()
    {
        var builder = CreateBuilder();

        builder.TableName("my-table");

        builder.TableNameValue.ShouldBe("my-table");
    }

    [Fact]
    public void TablePrefix_StoreValue()
    {
        var builder = CreateBuilder();

        builder.TablePrefix("dev-");

        builder.TablePrefixValue.ShouldBe("dev-");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void Client_ClearServiceUrlAndRegionAndFactoryAndBind()
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
    public void ServiceUrl_ClearClientAndRegionAndFactoryAndBind()
    {
        var builder = CreateBuilder();

        builder.Client(A.Fake<IAmazonDynamoDB>());
        builder.ServiceUrl("http://localhost:8000");

        builder.ClientInstance.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ServiceUrlValue.ShouldBe("http://localhost:8000");
    }

    [Fact]
    public void Region_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();

        builder.Client(A.Fake<IAmazonDynamoDB>());
        builder.Region(RegionEndpoint.EUWest1);

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.ServiceUrlValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.RegionValue.ShouldBe(RegionEndpoint.EUWest1);
    }

    [Fact]
    public void ClientFactory_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();

        builder.ServiceUrl("http://localhost:8000");
        builder.ClientFactory(factory);

        builder.ServiceUrlValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeSameAs(factory);
    }

    [Fact]
    public void BindConfiguration_ClearAllOtherConnectionOverloads()
    {
        var builder = CreateBuilder();

        builder.Client(A.Fake<IAmazonDynamoDB>());
        builder.BindConfiguration("Data:DynamoDB");

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.ServiceUrlValue.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Data:DynamoDB");
    }

    // --- Null/invalid argument guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceUrl_ThrowOnNullOrWhitespace(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ServiceUrl(value!));
    }

    [Fact]
    public void Region_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.Region(null!));
    }

    [Fact]
    public void Client_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.Client(null!));
    }

    [Fact]
    public void ClientFactory_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ClientFactory(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnNullOrWhitespace(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TableName_ThrowOnNullOrWhitespace(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.TableName(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TablePrefix_ThrowOnNullOrWhitespace(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.TablePrefix(value!));
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .ServiceUrl("http://localhost:8000")
            .TableName("my-table")
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

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.ClientFactory(_ => A.Fake<IAmazonDynamoDB>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Region_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.Region(RegionEndpoint.USWest2);
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("Data:DynamoDB");
        result.ShouldBeSameAs(builder);
    }

    // --- Feature methods do not clear connection state ---

    [Fact]
    public void TableName_PreserveConnectionState()
    {
        var builder = CreateBuilder();
        builder.ServiceUrl("http://localhost:8000");

        builder.TableName("my-table");

        builder.ServiceUrlValue.ShouldBe("http://localhost:8000");
    }

    [Fact]
    public void TablePrefix_PreserveConnectionState()
    {
        var builder = CreateBuilder();
        builder.ServiceUrl("http://localhost:8000");

        builder.TablePrefix("dev-");

        builder.ServiceUrlValue.ShouldBe("http://localhost:8000");
    }
}
