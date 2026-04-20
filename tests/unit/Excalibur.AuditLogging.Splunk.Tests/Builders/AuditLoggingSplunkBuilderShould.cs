// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Splunk;

using Tests.Shared;
using Tests.Shared.Categories;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Splunk.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="AuditLoggingSplunkBuilder"/> — 5 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// HecEndpoint and HecToken set values on <see cref="SplunkExporterOptions.Connection"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "AuditLogging")]
public sealed class AuditLoggingSplunkBuilderShould : UnitTestBase
{
    private static readonly Uri TestHecUri = new("https://splunk.example.com:8088/services/collector");

    private static (AuditLoggingSplunkBuilder Builder, SplunkExporterOptions Options) CreateBuilder()
    {
        var options = new SplunkExporterOptions();
        var builder = new AuditLoggingSplunkBuilder(options);
        return (builder, options);
    }

    // --- Happy path ---

    [Fact]
    public void HecEndpoint_SetValueOnConnectionOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.HecEndpoint(TestHecUri);
        options.Connection.HecEndpoint.ShouldBe(TestHecUri);
    }

    [Fact]
    public void HecToken_SetValueOnConnectionOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.HecToken("my-hec-token-12345");
        options.Connection.HecToken.ShouldBe("my-hec-token-12345");
    }

    [Fact]
    public void Index_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Index("audit_index");
        options.Index.ShouldBe("audit_index");
    }

    [Fact]
    public void SourceType_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.SourceType("custom:audit");
        options.SourceType.ShouldBe("custom:audit");
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Splunk");
        builder.BindConfigurationPath.ShouldBe("Audit:Splunk");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .HecEndpoint(TestHecUri)
            .HecToken("token-123")
            .Index("main")
            .SourceType("audit:dispatch");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Splunk").ShouldBeSameAs(builder);
    }

    // --- Last-wins: programmatic setters clear BindConfiguration ---

    [Fact]
    public void HecEndpoint_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Splunk");
        builder.HecEndpoint(TestHecUri);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void HecToken_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Splunk");
        builder.HecToken("new-token");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new AuditLoggingSplunkBuilder(null!));
    }

    // --- Validation guards ---

    [Fact]
    public void HecEndpoint_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.HecEndpoint(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HecToken_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.HecToken(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Index_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Index(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SourceType_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.SourceType(invalidValue!));
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
