// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Aws;

using Tests.Shared;
using Tests.Shared.Categories;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Aws.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="AuditLoggingAwsBuilder"/> — 6 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "AuditLogging")]
public sealed class AuditLoggingAwsBuilderShould : UnitTestBase
{
    private static (AuditLoggingAwsBuilder Builder, AwsAuditOptions Options) CreateBuilder()
    {
        var options = new AwsAuditOptions
        {
            LogGroupName = "default-group",
            Region = "us-east-1"
        };
        var builder = new AuditLoggingAwsBuilder(options);
        return (builder, options);
    }

    // --- Happy path ---

    [Fact]
    public void LogGroupName_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.LogGroupName("/aws/audit/my-app");
        options.LogGroupName.ShouldBe("/aws/audit/my-app");
    }

    [Fact]
    public void Region_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Region("eu-west-1");
        options.Region.ShouldBe("eu-west-1");
    }

    [Fact]
    public void StreamName_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.StreamName("my-stream");
        options.StreamName.ShouldBe("my-stream");
    }

    [Fact]
    public void ServiceUrl_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.ServiceUrl("https://logs.us-east-1.amazonaws.com");
        options.ServiceUrl.ShouldBe("https://logs.us-east-1.amazonaws.com");
    }

    [Fact]
    public void BatchSize_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.BatchSize(1000);
        options.BatchSize.ShouldBe(1000);
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Aws");
        builder.BindConfigurationPath.ShouldBe("Audit:Aws");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .LogGroupName("/aws/audit")
            .Region("us-west-2")
            .StreamName("stream-1")
            .ServiceUrl("https://localhost")
            .BatchSize(100);
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Aws").ShouldBeSameAs(builder);
    }

    // --- Last-wins: programmatic setters clear BindConfiguration ---

    [Fact]
    public void LogGroupName_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Aws");
        builder.LogGroupName("/aws/audit");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void Region_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Aws");
        builder.Region("eu-central-1");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new AuditLoggingAwsBuilder(null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LogGroupName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.LogGroupName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Region_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Region(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StreamName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.StreamName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceUrl_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ServiceUrl(invalidValue!));
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
