// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch;

using OpenSearch.Client;

namespace Excalibur.Data.Tests.OpenSearch.Builders;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "OpenSearch")]
public sealed class OpenSearchDataBuilderShould : UnitTestBase
{
    private readonly OpenSearchDataBuilder _sut = new();

    // ── Happy-path: each method sets its own property ──

    [Fact]
    public void SetNodeUri()
    {
        var uri = new Uri("https://localhost:9200");

        _sut.NodeUri(uri);

        _sut.NodeUriValue.ShouldBe(uri);
    }

    [Fact]
    public void SetNodeUris()
    {
        var uris = new[] { new Uri("https://node1:9200"), new Uri("https://node2:9200") };

        _sut.NodeUris(uris);

        _sut.NodeUrisValue.ShouldBe(uris);
    }

    [Fact]
    public void SetClientInstance()
    {
        var client = new OpenSearchClient();

        _sut.Client(client);

        _sut.ClientInstance.ShouldBeSameAs(client);
    }

    [Fact]
    public void SetClientFactory()
    {
        Func<IServiceProvider, OpenSearchClient> factory = _ => new OpenSearchClient();

        _sut.ClientFactory(factory);

        _sut.ClientFactoryFunc.ShouldBeSameAs(factory);
    }

    [Fact]
    public void SetBindConfigurationPath()
    {
        _sut.BindConfiguration("OpenSearch:Settings");

        _sut.BindConfigurationPath.ShouldBe("OpenSearch:Settings");
    }

    [Fact]
    public void SetIndexPrefix()
    {
        _sut.IndexPrefix("myapp");

        _sut.IndexPrefixValue.ShouldBe("myapp");
    }

    // ── Last-wins: connection methods are mutually exclusive ──

    [Fact]
    public void ClearOtherConnectionPropertiesWhenNodeUriIsSet()
    {
        // Arrange - set every other connection property first
        _sut.NodeUris(new[] { new Uri("https://node:9200") });
        _sut.Client(new OpenSearchClient());
        _sut.ClientFactory(_ => new OpenSearchClient());
        _sut.BindConfiguration("Section");

        // Act
        _sut.NodeUri(new Uri("https://final:9200"));

        // Assert - only NodeUriValue survives
        _sut.NodeUriValue.ShouldNotBeNull();
        _sut.NodeUrisValue.ShouldBeNull();
        _sut.ClientInstance.ShouldBeNull();
        _sut.ClientFactoryFunc.ShouldBeNull();
        _sut.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ClearOtherConnectionPropertiesWhenNodeUrisIsSet()
    {
        _sut.NodeUri(new Uri("https://node:9200"));
        _sut.Client(new OpenSearchClient());
        _sut.ClientFactory(_ => new OpenSearchClient());
        _sut.BindConfiguration("Section");

        _sut.NodeUris(new[] { new Uri("https://final:9200") });

        _sut.NodeUrisValue.ShouldNotBeNull();
        _sut.NodeUriValue.ShouldBeNull();
        _sut.ClientInstance.ShouldBeNull();
        _sut.ClientFactoryFunc.ShouldBeNull();
        _sut.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ClearOtherConnectionPropertiesWhenClientIsSet()
    {
        _sut.NodeUri(new Uri("https://node:9200"));
        _sut.NodeUris(new[] { new Uri("https://node:9200") });
        _sut.ClientFactory(_ => new OpenSearchClient());
        _sut.BindConfiguration("Section");

        _sut.Client(new OpenSearchClient());

        _sut.ClientInstance.ShouldNotBeNull();
        _sut.NodeUriValue.ShouldBeNull();
        _sut.NodeUrisValue.ShouldBeNull();
        _sut.ClientFactoryFunc.ShouldBeNull();
        _sut.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ClearOtherConnectionPropertiesWhenClientFactoryIsSet()
    {
        _sut.NodeUri(new Uri("https://node:9200"));
        _sut.NodeUris(new[] { new Uri("https://node:9200") });
        _sut.Client(new OpenSearchClient());
        _sut.BindConfiguration("Section");

        _sut.ClientFactory(_ => new OpenSearchClient());

        _sut.ClientFactoryFunc.ShouldNotBeNull();
        _sut.NodeUriValue.ShouldBeNull();
        _sut.NodeUrisValue.ShouldBeNull();
        _sut.ClientInstance.ShouldBeNull();
        _sut.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ClearOtherConnectionPropertiesWhenBindConfigurationIsSet()
    {
        _sut.NodeUri(new Uri("https://node:9200"));
        _sut.NodeUris(new[] { new Uri("https://node:9200") });
        _sut.Client(new OpenSearchClient());
        _sut.ClientFactory(_ => new OpenSearchClient());

        _sut.BindConfiguration("OpenSearch");

        _sut.BindConfigurationPath.ShouldNotBeNull();
        _sut.NodeUriValue.ShouldBeNull();
        _sut.NodeUrisValue.ShouldBeNull();
        _sut.ClientInstance.ShouldBeNull();
        _sut.ClientFactoryFunc.ShouldBeNull();
    }

    // ── IndexPrefix is additive (not mutually exclusive) ──

    [Fact]
    public void PreserveIndexPrefixWhenConnectionMethodChanges()
    {
        _sut.IndexPrefix("myapp");
        _sut.NodeUri(new Uri("https://localhost:9200"));

        _sut.IndexPrefixValue.ShouldBe("myapp");
        _sut.NodeUriValue.ShouldNotBeNull();
    }

    [Fact]
    public void OverwriteIndexPrefixWhenCalledAgain()
    {
        _sut.IndexPrefix("first");
        _sut.IndexPrefix("second");

        _sut.IndexPrefixValue.ShouldBe("second");
    }

    // ── Fluent chaining: every method returns the builder ──

    [Fact]
    public void ReturnSameBuilderFromNodeUri()
    {
        var result = _sut.NodeUri(new Uri("https://localhost:9200"));

        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public void ReturnSameBuilderFromNodeUris()
    {
        var result = _sut.NodeUris(new[] { new Uri("https://localhost:9200") });

        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public void ReturnSameBuilderFromClient()
    {
        var result = _sut.Client(new OpenSearchClient());

        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public void ReturnSameBuilderFromClientFactory()
    {
        var result = _sut.ClientFactory(_ => new OpenSearchClient());

        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public void ReturnSameBuilderFromBindConfiguration()
    {
        var result = _sut.BindConfiguration("Section");

        result.ShouldBeSameAs(_sut);
    }

    [Fact]
    public void ReturnSameBuilderFromIndexPrefix()
    {
        var result = _sut.IndexPrefix("prefix");

        result.ShouldBeSameAs(_sut);
    }

    // ── Validation guards: null and empty inputs throw ──

    [Fact]
    public void ThrowWhenNodeUriIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.NodeUri(null!));
    }

    [Fact]
    public void ThrowWhenNodeUrisIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.NodeUris(null!));
    }

    [Fact]
    public void ThrowWhenClientIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Client(null!));
    }

    [Fact]
    public void ThrowWhenClientFactoryIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.ClientFactory(null!));
    }

    [Fact]
    public void ThrowWhenBindConfigurationPathIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.BindConfiguration(null!));
    }

    [Fact]
    public void ThrowWhenBindConfigurationPathIsEmpty()
    {
        Should.Throw<ArgumentException>(() => _sut.BindConfiguration(""));
    }

    [Fact]
    public void ThrowWhenBindConfigurationPathIsWhitespace()
    {
        Should.Throw<ArgumentException>(() => _sut.BindConfiguration("  "));
    }

    [Fact]
    public void ThrowWhenIndexPrefixIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _sut.IndexPrefix(null!));
    }

    [Fact]
    public void ThrowWhenIndexPrefixIsEmpty()
    {
        Should.Throw<ArgumentException>(() => _sut.IndexPrefix(""));
    }

    [Fact]
    public void ThrowWhenIndexPrefixIsWhitespace()
    {
        Should.Throw<ArgumentException>(() => _sut.IndexPrefix("  "));
    }

    // ── Default state: all properties start null ──

    [Fact]
    public void HaveAllPropertiesNullByDefault()
    {
        var builder = new OpenSearchDataBuilder();

        builder.NodeUriValue.ShouldBeNull();
        builder.NodeUrisValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.IndexPrefixValue.ShouldBeNull();
    }
}
