// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;

using Excalibur.Dispatch.Compliance.Aws;

namespace Excalibur.Dispatch.Compliance.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ComplianceAwsBuilder"/> -- 6 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Compliance")]
public sealed class ComplianceAwsBuilderShould : UnitTestBase
{
    private static (ComplianceAwsBuilder Builder, AwsKmsOptions Options) CreateBuilder()
    {
        var options = new AwsKmsOptions();
        var builder = new ComplianceAwsBuilder(options);
        return (builder, options);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new ComplianceAwsBuilder(null!));
    }

    // --- Happy path ---

    [Fact]
    public void Region_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Region("us-east-1");
        options.Region.ShouldBe(RegionEndpoint.USEast1);
    }

    [Fact]
    public void UseFipsEndpoint_SetTrueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.UseFipsEndpoint();
        options.UseFipsEndpoint.ShouldBeTrue();
    }

    [Fact]
    public void UseFipsEndpoint_SetFalseOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.UseFipsEndpoint(true);
        builder.UseFipsEndpoint(false);
        options.UseFipsEndpoint.ShouldBeFalse();
    }

    [Fact]
    public void KeyAliasPrefix_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.KeyAliasPrefix("my-prefix");
        options.KeyAliasPrefix.ShouldBe("my-prefix");
    }

    [Fact]
    public void Environment_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Environment("staging");
        options.Environment.ShouldBe("staging");
    }

    [Fact]
    public void ServiceUrl_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.ServiceUrl("https://localhost:4566");
        options.ServiceUrl.ShouldBe("https://localhost:4566");
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Aws");
        builder.BindConfigurationPath.ShouldBe("Compliance:Aws");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .Region("us-west-2")
            .UseFipsEndpoint()
            .KeyAliasPrefix("test")
            .Environment("dev")
            .ServiceUrl("https://localhost");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Aws").ShouldBeSameAs(builder);
    }

    // --- Last-wins: Region clears BindConfigurationPath ---

    [Fact]
    public void Region_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Aws");
        builder.Region("eu-west-1");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_OverridesProgrammaticSettings()
    {
        var (builder, _) = CreateBuilder();
        builder.Region("us-east-1");
        builder.BindConfiguration("Compliance:Aws");
        builder.BindConfigurationPath.ShouldBe("Compliance:Aws");
    }

    // --- Validation guards ---

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
    public void KeyAliasPrefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.KeyAliasPrefix(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Environment_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Environment(invalidValue!));
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
