// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Azure;


using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ComplianceAzureBuilder"/> -- 6 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Compliance")]
public sealed class ComplianceAzureBuilderShould : UnitTestBase
{
    private static readonly Uri SampleVaultUri = new("https://my-vault.vault.azure.net/");
    private static readonly Uri AlternateVaultUri = new("https://other-vault.vault.azure.net/");

    private static (ComplianceAzureBuilder Builder, AzureKeyVaultOptions Options) CreateBuilder()
    {
        var options = new AzureKeyVaultOptions();
        var builder = new ComplianceAzureBuilder(options);
        return (builder, options);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new ComplianceAzureBuilder(null!));
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
    public void KeyNamePrefix_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.KeyNamePrefix("my-prefix");
        options.KeyNamePrefix.ShouldBe("my-prefix");
    }

    [Fact]
    public void RequirePremiumTier_SetTrueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.RequirePremiumTier();
        options.RequirePremiumTier.ShouldBeTrue();
    }

    [Fact]
    public void RequirePremiumTier_SetFalseOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.RequirePremiumTier(true);
        builder.RequirePremiumTier(false);
        options.RequirePremiumTier.ShouldBeFalse();
    }

    [Fact]
    public void MetadataCacheDuration_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        var duration = TimeSpan.FromMinutes(10);
        builder.MetadataCacheDuration(duration);
        options.MetadataCacheDuration.ShouldBe(duration);
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
        builder.BindConfiguration("Compliance:Azure");
        builder.BindConfigurationPath.ShouldBe("Compliance:Azure");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .VaultUri(SampleVaultUri)
            .KeyNamePrefix("test")
            .RequirePremiumTier()
            .MetadataCacheDuration(TimeSpan.FromMinutes(5))
            .EnableDetailedTelemetry();
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Azure").ShouldBeSameAs(builder);
    }

    // --- Last-wins: VaultUri clears BindConfigurationPath ---

    [Fact]
    public void VaultUri_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Compliance:Azure");
        builder.VaultUri(SampleVaultUri);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_OverridesProgrammaticSettings()
    {
        var (builder, _) = CreateBuilder();
        builder.VaultUri(SampleVaultUri);
        builder.BindConfiguration("Compliance:Azure");
        builder.BindConfigurationPath.ShouldBe("Compliance:Azure");
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
    public void KeyNamePrefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.KeyNamePrefix(invalidValue!));
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
