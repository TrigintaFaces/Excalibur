// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.Builders;

/// <summary>
/// Unit tests for <see cref="ElasticSearchDataBuilder"/> — 7 connection modes,
/// IndexPrefix (additive), last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "ElasticSearch")]
public sealed class ElasticSearchDataBuilderShould : UnitTestBase
{
    private static readonly Uri TestUri = new("http://localhost:9200");

    private static readonly Uri[] TestUris =
    [
        new("http://node1:9200"),
        new("http://node2:9200"),
    ];

    private static ElasticSearchDataBuilder CreateBuilder() => new();

    // --- Connection overloads (happy path) ---

    [Fact]
    public void NodeUri_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.NodeUri(TestUri);

        builder.NodeUriValue.ShouldBe(TestUri);
    }

    [Fact]
    public void NodeUris_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.NodeUris(TestUris);

        builder.NodeUrisValue.ShouldBe(TestUris);
    }

    [Fact]
    public void CloudId_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.CloudId("my-cloud-id");

        builder.CloudIdValue.ShouldBe("my-cloud-id");
    }

    [Fact]
    public void Client_StoreInstanceOnBuilder()
    {
        var builder = CreateBuilder();
        var client = new ElasticsearchClient();

        builder.Client(client);

        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_StoreFactoryOnBuilder()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, ElasticsearchClient> factory = _ => new ElasticsearchClient();

        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("ElasticSearch:Data");

        builder.BindConfigurationPath.ShouldBe("ElasticSearch:Data");
    }

    [Fact]
    public void IndexPrefix_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.IndexPrefix("myapp");

        builder.IndexPrefixValue.ShouldBe("myapp");
    }

    // --- Last-wins semantics (connection modes are mutually exclusive) ---

    [Fact]
    public void NodeUri_ClearAllOtherConnectionModes()
    {
        var builder = CreateBuilder();

        builder.CloudId("some-cloud");
        builder.NodeUri(TestUri);

        builder.NodeUriValue.ShouldBe(TestUri);
        builder.NodeUrisValue.ShouldBeNull();
        builder.CloudIdValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void NodeUris_ClearAllOtherConnectionModes()
    {
        var builder = CreateBuilder();

        builder.NodeUri(TestUri);
        builder.NodeUris(TestUris);

        builder.NodeUrisValue.ShouldBe(TestUris);
        builder.NodeUriValue.ShouldBeNull();
        builder.CloudIdValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void CloudId_ClearAllOtherConnectionModes()
    {
        var builder = CreateBuilder();
        var client = new ElasticsearchClient();

        builder.Client(client);
        builder.CloudId("my-cloud");

        builder.CloudIdValue.ShouldBe("my-cloud");
        builder.NodeUriValue.ShouldBeNull();
        builder.NodeUrisValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void Client_ClearAllOtherConnectionModes()
    {
        var builder = CreateBuilder();
        var client = new ElasticsearchClient();

        builder.NodeUri(TestUri);
        builder.Client(client);

        builder.ClientInstance.ShouldBe(client);
        builder.NodeUriValue.ShouldBeNull();
        builder.NodeUrisValue.ShouldBeNull();
        builder.CloudIdValue.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ClientFactory_ClearAllOtherConnectionModes()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, ElasticsearchClient> factory = _ => new ElasticsearchClient();

        builder.CloudId("some-cloud");
        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
        builder.NodeUriValue.ShouldBeNull();
        builder.NodeUrisValue.ShouldBeNull();
        builder.CloudIdValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearAllOtherConnectionModes()
    {
        var builder = CreateBuilder();
        var client = new ElasticsearchClient();

        builder.Client(client);
        builder.BindConfiguration("ElasticSearch:Data");

        builder.BindConfigurationPath.ShouldBe("ElasticSearch:Data");
        builder.NodeUriValue.ShouldBeNull();
        builder.NodeUrisValue.ShouldBeNull();
        builder.CloudIdValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
    }

    // --- IndexPrefix is additive (does NOT clear connection state) ---

    [Fact]
    public void IndexPrefix_NotClearConnectionState()
    {
        var builder = CreateBuilder();

        builder.NodeUri(TestUri);
        builder.IndexPrefix("myapp");

        builder.NodeUriValue.ShouldBe(TestUri);
        builder.IndexPrefixValue.ShouldBe("myapp");
    }

    // --- Fluent chaining ---

    [Fact]
    public void NodeUri_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.NodeUri(TestUri);
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void NodeUris_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.NodeUris(TestUris);
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void CloudId_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.CloudId("my-cloud");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.Client(new ElasticsearchClient());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.ClientFactory(_ => new ElasticsearchClient());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("ElasticSearch:Data");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void IndexPrefix_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.IndexPrefix("myapp");
        result.ShouldBeSameAs(builder);
    }

    // --- Validation guards ---

    [Fact]
    public void NodeUri_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.NodeUri(null!));
    }

    [Fact]
    public void NodeUris_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.NodeUris(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudId_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CloudId(invalidValue!));
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
    public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IndexPrefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.IndexPrefix(invalidValue!));
    }
}
