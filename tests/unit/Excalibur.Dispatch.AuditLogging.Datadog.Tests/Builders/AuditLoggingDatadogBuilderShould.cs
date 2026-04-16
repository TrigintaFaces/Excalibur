// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Datadog;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.AuditLogging.Datadog.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="AuditLoggingDatadogBuilder"/> — 5 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "AuditLogging")]
public sealed class AuditLoggingDatadogBuilderShould : UnitTestBase
{
    private static (AuditLoggingDatadogBuilder Builder, DatadogExporterOptions Options) CreateBuilder()
    {
        var options = new DatadogExporterOptions
        {
            ApiKey = "default-key"
        };
        var builder = new AuditLoggingDatadogBuilder(options);
        return (builder, options);
    }

    // --- Happy path ---

    [Fact]
    public void ApiKey_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.ApiKey("my-api-key-12345");
        options.ApiKey.ShouldBe("my-api-key-12345");
    }

    [Fact]
    public void Site_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Site("datadoghq.eu");
        options.Site.ShouldBe("datadoghq.eu");
    }

    [Fact]
    public void Service_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Service("my-audit-service");
        options.Service.ShouldBe("my-audit-service");
    }

    [Fact]
    public void Source_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Source("custom-source");
        options.Source.ShouldBe("custom-source");
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Datadog");
        builder.BindConfigurationPath.ShouldBe("Audit:Datadog");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .ApiKey("key-123")
            .Site("datadoghq.com")
            .Service("svc")
            .Source("src");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Datadog").ShouldBeSameAs(builder);
    }

    // --- Last-wins: programmatic setters clear BindConfiguration ---

    [Fact]
    public void ApiKey_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Datadog");
        builder.ApiKey("new-key");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void Site_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Datadog");
        builder.Site("us5.datadoghq.com");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new AuditLoggingDatadogBuilder(null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApiKey_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ApiKey(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Site_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Site(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Service_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Service(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Source_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Source(invalidValue!));
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
