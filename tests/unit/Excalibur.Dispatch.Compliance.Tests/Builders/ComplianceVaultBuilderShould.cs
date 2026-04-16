// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Vault;

namespace Excalibur.Dispatch.Compliance.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ComplianceVaultBuilder"/> -- 6 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Compliance")]
public sealed class ComplianceVaultBuilderShould : UnitTestBase
{
    private static readonly Uri SampleVaultUri = new("https://vault.example.com:8200");
    private static readonly Uri AlternateVaultUri = new("https://vault2.example.com:8200");

    private static (ComplianceVaultBuilder Builder, VaultOptions Options) CreateBuilder()
    {
        var options = new VaultOptions();
        var builder = new ComplianceVaultBuilder(options);
        return (builder, options);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new ComplianceVaultBuilder(null!));
    }

    // --- Happy path ---

    [Fact]
    public void VaultUri_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.VaultUri(SampleVaultUri);
        options.VaultUri.ShouldBe(SampleVaultUri);
    }

    [Fact]
    public void TransitMountPath_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.TransitMountPath("secret-transit");
        options.TransitMountPath.ShouldBe("secret-transit");
    }

    [Fact]
    public void KeyNamePrefix_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.KeyNamePrefix("my-app-");
        options.KeyNamePrefix.ShouldBe("my-app-");
    }

    [Fact]
    public void Namespace_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.Namespace("admin/team-a");
        options.Namespace.ShouldBe("admin/team-a");
    }

    [Fact]
    public void EnableDetailedTelemetry_SetTrueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.EnableDetailedTelemetry();
        options.EnableDetailedTelemetry.ShouldBeTrue();
    }

    [Fact]
    public void EnableDetailedTelemetry_SetFalseOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.EnableDetailedTelemetry(true);
        builder.EnableDetailedTelemetry(false);
        options.EnableDetailedTelemetry.ShouldBeFalse();
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Vault");
        builder.BindConfigurationPath.ShouldBe("Compliance:Vault");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .VaultUri(SampleVaultUri)
            .TransitMountPath("transit")
            .KeyNamePrefix("test-")
            .Namespace("root")
            .EnableDetailedTelemetry();
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Vault").ShouldBeSameAs(builder);
    }

    // --- Last-wins: VaultUri clears BindConfigurationPath ---

    [Fact]
    public void VaultUri_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Vault");
        builder.VaultUri(SampleVaultUri);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_OverridesProgrammaticSettings()
    {
        var (builder, _) = CreateBuilder();
        builder.VaultUri(SampleVaultUri);
        builder.BindConfiguration("Compliance:Vault");
        builder.BindConfigurationPath.ShouldBe("Compliance:Vault");
    }

    [Fact]
    public void VaultUri_LastWins()
    {
        var (builder, options) = CreateBuilder();
        builder.VaultUri(SampleVaultUri);
        builder.VaultUri(AlternateVaultUri);
        options.VaultUri.ShouldBe(AlternateVaultUri);
    }

    // --- Validation guards ---

    [Fact]
    public void VaultUri_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.VaultUri(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TransitMountPath_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.TransitMountPath(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void KeyNamePrefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.KeyNamePrefix(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Namespace_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Namespace(invalidValue!));
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
