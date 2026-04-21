// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.Builders;

/// <summary>
/// Unit tests for <see cref="ElasticSearchInboxBuilder"/> — 6 connection modes,
/// IndexName, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "ElasticSearch")]
public sealed class ElasticSearchInboxBuilderShould : UnitTestBase
{
    private static readonly Uri TestUri = new("http://localhost:9200");

    private static readonly Uri[] TestUris =
    [
        new("http://node1:9200"),
        new("http://node2:9200"),
    ];

    private static (ElasticSearchInboxBuilder Builder, ElasticsearchInboxOptions Options) CreateBuilder()
    {
        var options = new ElasticsearchInboxOptions();
        var builder = new ElasticSearchInboxBuilder(options);
        return (builder, options);
    }

    // --- Connection overloads (happy path) ---

    [Fact]
    public void NodeUri_StoreValueOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.NodeUri(TestUri);

        builder.NodeUriValue.ShouldBe(TestUri);
    }

    [Fact]
    public void NodeUris_StoreValueOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.NodeUris(TestUris);

        builder.NodeUrisValue.ShouldBe(TestUris);
    }

    [Fact]
    public void CloudId_StoreValueOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.CloudId("my-cloud-id");

        builder.CloudIdValue.ShouldBe("my-cloud-id");
    }

    [Fact]
    public void Client_StoreInstanceOnBuilder()
    {
        var (builder, _) = CreateBuilder();
        var client = new ElasticsearchClient();

        builder.Client(client);

        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_StoreFactoryOnBuilder()
    {
        var (builder, _) = CreateBuilder();
        Func<IServiceProvider, ElasticsearchClient> factory = _ => new ElasticsearchClient();

        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("ElasticSearch:Inbox");

        builder.BindConfigurationPath.ShouldBe("ElasticSearch:Inbox");
    }

    // --- Feature methods (happy path) ---

    [Fact]
    public void IndexName_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.IndexName("my-inbox");

        options.IndexName.ShouldBe("my-inbox");
    }

    // --- Last-wins semantics (connection modes are mutually exclusive) ---

    [Fact]
    public void NodeUri_ClearAllOtherConnectionModes()
    {
        var (builder, _) = CreateBuilder();

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
        var (builder, _) = CreateBuilder();

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
        var (builder, _) = CreateBuilder();
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
        var (builder, _) = CreateBuilder();
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
        var (builder, _) = CreateBuilder();
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
        var (builder, _) = CreateBuilder();
        var client = new ElasticsearchClient();

        builder.Client(client);
        builder.BindConfiguration("ElasticSearch:Inbox");

        builder.BindConfigurationPath.ShouldBe("ElasticSearch:Inbox");
        builder.NodeUriValue.ShouldBeNull();
        builder.NodeUrisValue.ShouldBeNull();
        builder.CloudIdValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();

        var result = builder
            .NodeUri(TestUri)
            .IndexName("my-inbox");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void NodeUris_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.NodeUris(TestUris);
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void CloudId_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.CloudId("my-cloud");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.Client(new ElasticsearchClient());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ClientFactory(_ => new ElasticsearchClient());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.BindConfiguration("ElasticSearch:Inbox");
        result.ShouldBeSameAs(builder);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new ElasticSearchInboxBuilder(null!));
    }

    // --- Validation guards ---

    [Fact]
    public void NodeUri_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.NodeUri(null!));
    }

    [Fact]
    public void NodeUris_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.NodeUris(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloudId_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CloudId(invalidValue!));
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
    public void IndexName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.IndexName(invalidValue!));
    }
}
