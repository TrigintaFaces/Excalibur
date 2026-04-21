// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.OpenSearch;

using Tests.Shared;
using Tests.Shared.Categories;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Elasticsearch.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="AuditLoggingOpenSearchBuilder"/> — 4 builder methods,
/// fluent chaining, validation guards, and mutual exclusion for connection overloads
/// (NodeUri/NodeUris are last-wins; BindConfiguration clears all).
/// </summary>
/// <remarks>
/// This test class lives in the Elasticsearch test project because the test project
/// already references the OpenSearch source project.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "AuditLogging")]
public sealed class AuditLoggingOpenSearchBuilderShould : UnitTestBase
{
    private static readonly Uri TestUri = new("https://os.example.com:9200");
    private static readonly Uri TestUri2 = new("https://os2.example.com:9200");

    private static (AuditLoggingOpenSearchBuilder Builder, OpenSearchExporterOptions Options) CreateBuilder()
    {
        var options = new OpenSearchExporterOptions
        {
            OpenSearchUrl = "https://localhost:9200"
        };
        var builder = new AuditLoggingOpenSearchBuilder(options);
        return (builder, options);
    }

    // --- Happy path ---

    [Fact]
    public void NodeUri_SetOpenSearchUrlOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUri(TestUri);
        options.OpenSearchUrl.ShouldBe(TestUri.AbsoluteUri);
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
        options.OpenSearchUrl.ShouldBe(TestUri.AbsoluteUri);
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
        builder.BindConfiguration("Audit:OpenSearch");
        builder.BindConfigurationPath.ShouldBe("Audit:OpenSearch");
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
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:OS").ShouldBeSameAs(builder);
    }

    // --- Mutual exclusion: connection overloads (last-wins) ---

    [Fact]
    public void NodeUri_ClearNodeUrls()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUris([TestUri, TestUri2]);
        builder.NodeUri(TestUri);
        options.NodeUrls.ShouldBeNull();
        options.OpenSearchUrl.ShouldBe(TestUri.AbsoluteUri);
    }

    [Fact]
    public void NodeUris_OverwriteNodeUri()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUri(TestUri);
        builder.NodeUris([TestUri2]);
        options.NodeUrls.ShouldNotBeNull();
        options.NodeUrls.Count.ShouldBe(1);
        options.OpenSearchUrl.ShouldBe(TestUri2.AbsoluteUri);
    }

    [Fact]
    public void NodeUri_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:OS");
        builder.NodeUri(TestUri);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void NodeUris_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:OS");
        builder.NodeUris([TestUri]);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearConnectionValues()
    {
        var (builder, options) = CreateBuilder();
        builder.NodeUris([TestUri, TestUri2]);
        builder.BindConfiguration("Audit:OS");
        options.OpenSearchUrl.ShouldBeNull();
        options.NodeUrls.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Audit:OS");
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new AuditLoggingOpenSearchBuilder(null!));
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
