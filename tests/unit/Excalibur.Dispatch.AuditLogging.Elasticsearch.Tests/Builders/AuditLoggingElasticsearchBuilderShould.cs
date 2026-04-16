// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Elasticsearch;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="AuditLoggingElasticsearchBuilder"/> — 5 builder methods,
/// fluent chaining, validation guards, and mutual exclusion for connection overloads
/// (NodeUri/NodeUris/CloudId are last-wins; BindConfiguration clears all).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "AuditLogging")]
public sealed class AuditLoggingElasticsearchBuilderShould : UnitTestBase
{
    private static readonly Uri TestUri = new("https://es.example.com:9200");
    private static readonly Uri TestUri2 = new("https://es2.example.com:9200");

    private static (AuditLoggingElasticsearchBuilder Builder, ElasticsearchExporterOptions Options) CreateBuilder()
    {
        var options = new ElasticsearchExporterOptions
        {
            ElasticsearchUrl = "https://localhost:9200"
        };
        var builder = new AuditLoggingElasticsearchBuilder(options);
        return (builder, options);
    }

    // --- Happy path ---

    [Fact]
    public void NodeUri_SetElasticsearchUrlOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUri(TestUri);
        options.ElasticsearchUrl.ShouldBe(TestUri.AbsoluteUri);
    }

    [Fact]
    public void NodeUris_SetNodeUrlsOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUris([TestUri, TestUri2]);
        options.NodeUrls.ShouldNotBeNull();
        options.NodeUrls.Count.ShouldBe(2);
        options.NodeUrls[0].ShouldBe(TestUri.AbsoluteUri);
        options.NodeUrls[1].ShouldBe(TestUri2.AbsoluteUri);
        options.ElasticsearchUrl.ShouldBe(TestUri.AbsoluteUri);
    }

    [Fact]
    public void CloudId_SetElasticsearchUrlOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.CloudId("my-deployment:dXMtZWFzdC0xLmF3cy5mb3VuZC5pbw==");
        options.ElasticsearchUrl.ShouldBe("my-deployment:dXMtZWFzdC0xLmF3cy5mb3VuZC5pbw==");
    }

    [Fact]
    public void IndexName_SetIndexPrefixOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.IndexName("custom-audit");
        options.IndexPrefix.ShouldBe("custom-audit");
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Elasticsearch");
        builder.BindConfigurationPath.ShouldBe("Audit:Elasticsearch");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .NodeUri(TestUri)
            .IndexName("audit");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void NodeUris_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.NodeUris([TestUri]).ShouldBeSameAs(builder);
    }

    [Fact]
    public void CloudId_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.CloudId("cloud-id-123").ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:ES").ShouldBeSameAs(builder);
    }

    // --- Mutual exclusion: connection overloads (last-wins) ---

    [Fact]
    public void NodeUri_ClearNodeUrls()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUris([TestUri, TestUri2]);
        builder.NodeUri(TestUri);
        options.NodeUrls.ShouldBeNull();
        options.ElasticsearchUrl.ShouldBe(TestUri.AbsoluteUri);
    }

    [Fact]
    public void NodeUris_OverwriteNodeUri()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUri(TestUri);
        builder.NodeUris([TestUri2]);
        options.NodeUrls.ShouldNotBeNull();
        options.NodeUrls.Count.ShouldBe(1);
        options.ElasticsearchUrl.ShouldBe(TestUri2.AbsoluteUri);
    }

    [Fact]
    public void CloudId_ClearNodeUrls()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUris([TestUri, TestUri2]);
        builder.CloudId("cloud-id");
        options.NodeUrls.ShouldBeNull();
        options.ElasticsearchUrl.ShouldBe("cloud-id");
    }

    [Fact]
    public void NodeUri_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:ES");
        builder.NodeUri(TestUri);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void NodeUris_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:ES");
        builder.NodeUris([TestUri]);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void CloudId_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:ES");
        builder.CloudId("cloud-id");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearConnectionValues()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUris([TestUri, TestUri2]);
        builder.BindConfiguration("Audit:ES");
        options.ElasticsearchUrl.ShouldBeNull();
        options.NodeUrls.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Audit:ES");
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new AuditLoggingElasticsearchBuilder(null!));
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IndexName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.IndexName(invalidValue!));
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
}
