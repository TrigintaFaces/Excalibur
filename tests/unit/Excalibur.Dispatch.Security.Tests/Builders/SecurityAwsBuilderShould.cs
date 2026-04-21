// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security.Aws;

namespace Excalibur.Dispatch.Security.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="SecurityAwsBuilder"/> -- 2 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Security")]
public sealed class SecurityAwsBuilderShould : UnitTestBase
{
    private static SecurityAwsBuilder CreateBuilder() => new();

    // --- Happy path ---

    [Fact]
    public void Region_SetValueOnBuilder()
    {
        var builder = CreateBuilder();
        ((ISecurityAwsBuilder)builder).Region("us-east-1");
        builder.Region.ShouldBe("us-east-1");
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("Security:Aws");
        builder.BindConfigurationPath.ShouldBe("Security:Aws");
    }

    // --- Fluent chaining ---

    [Fact]
    public void Region_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = ((ISecurityAwsBuilder)builder).Region("us-west-2");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("Security:Aws");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = ((ISecurityAwsBuilder)builder)
            .Region("eu-central-1")
            .BindConfiguration("Security:Aws");
        result.ShouldBeSameAs(builder);
    }

    // --- Last-wins: Region clears BindConfigurationPath ---

    [Fact]
    public void Region_ClearBindConfigurationPath()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("Security:Aws");
        ((ISecurityAwsBuilder)builder).Region("us-east-1");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_OverridesProgrammaticSettings()
    {
        var builder = CreateBuilder();
        ((ISecurityAwsBuilder)builder).Region("us-east-1");
        builder.BindConfiguration("Security:Aws");
        builder.BindConfigurationPath.ShouldBe("Security:Aws");
    }

    [Fact]
    public void Region_LastWins()
    {
        var builder = CreateBuilder();
        ((ISecurityAwsBuilder)builder).Region("us-east-1");
        ((ISecurityAwsBuilder)builder).Region("eu-west-1");
        builder.Region.ShouldBe("eu-west-1");
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Region_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => ((ISecurityAwsBuilder)builder).Region(invalidValue!));
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

    // --- Default state ---

    [Fact]
    public void DefaultState_RegionIsNull()
    {
        var builder = CreateBuilder();
        builder.Region.ShouldBeNull();
    }

    [Fact]
    public void DefaultState_BindConfigurationPathIsNull()
    {
        var builder = CreateBuilder();
        builder.BindConfigurationPath.ShouldBeNull();
    }
}
