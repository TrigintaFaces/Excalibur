// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Azure;

namespace Excalibur.Dispatch.Security.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="SecurityAzureBuilder"/> -- 4 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Security")]
public sealed class SecurityAzureBuilderShould : UnitTestBase
{
    private static SecurityAzureBuilder CreateBuilder() => new();

    // --- Happy path ---

    [Fact]
    public void VaultUri_SetValueOnBuilder()
    {
        var builder = CreateBuilder();
        ((ISecurityAzureBuilder)builder).VaultUri("https://my-vault.vault.azure.net/");
        builder.VaultUri.ShouldBe("https://my-vault.vault.azure.net/");
    }

    [Fact]
    public void KeyPrefix_SetValueOnBuilder()
    {
        var builder = CreateBuilder();
        builder.KeyPrefix("my-prefix");
        builder.KeyPrefixValue.ShouldBe("my-prefix");
    }

    [Fact]
    public void EnableServiceBusValidation_SetTrueOnBuilder()
    {
        var builder = CreateBuilder();
        builder.EnableServiceBusValidation();
        builder.ServiceBusValidationEnabled.ShouldBeTrue();
    }

    [Fact]
    public void EnableServiceBusValidation_SetFalseOnBuilder()
    {
        var builder = CreateBuilder();
        builder.EnableServiceBusValidation(false);
        builder.ServiceBusValidationEnabled.ShouldBeFalse();
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("Security:Azure");
        builder.BindConfigurationPath.ShouldBe("Security:Azure");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = ((ISecurityAzureBuilder)builder)
            .VaultUri("https://my-vault.vault.azure.net/")
            .KeyPrefix("test")
            .EnableServiceBusValidation();
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("Security:Azure");
        result.ShouldBeSameAs(builder);
    }

    // --- Last-wins: VaultUri clears BindConfigurationPath ---

    [Fact]
    public void VaultUri_ClearBindConfigurationPath()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("Security:Azure");
        ((ISecurityAzureBuilder)builder).VaultUri("https://my-vault.vault.azure.net/");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_OverridesProgrammaticSettings()
    {
        var builder = CreateBuilder();
        ((ISecurityAzureBuilder)builder).VaultUri("https://my-vault.vault.azure.net/");
        builder.BindConfiguration("Security:Azure");
        builder.BindConfigurationPath.ShouldBe("Security:Azure");
    }

    [Fact]
    public void VaultUri_LastWins()
    {
        var builder = CreateBuilder();
        ((ISecurityAzureBuilder)builder).VaultUri("https://first.vault.azure.net/");
        ((ISecurityAzureBuilder)builder).VaultUri("https://second.vault.azure.net/");
        builder.VaultUri.ShouldBe("https://second.vault.azure.net/");
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VaultUri_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => ((ISecurityAzureBuilder)builder).VaultUri(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void KeyPrefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.KeyPrefix(invalidValue!));
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
    public void DefaultState_VaultUriIsNull()
    {
        var builder = CreateBuilder();
        builder.VaultUri.ShouldBeNull();
    }

    [Fact]
    public void DefaultState_KeyPrefixValueIsNull()
    {
        var builder = CreateBuilder();
        builder.KeyPrefixValue.ShouldBeNull();
    }

    [Fact]
    public void DefaultState_ServiceBusValidationEnabled()
    {
        var builder = CreateBuilder();
        builder.ServiceBusValidationEnabled.ShouldBeTrue();
    }

    [Fact]
    public void DefaultState_BindConfigurationPathIsNull()
    {
        var builder = CreateBuilder();
        builder.BindConfigurationPath.ShouldBeNull();
    }
}
