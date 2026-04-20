// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Sentinel;

using Tests.Shared;
using Tests.Shared.Categories;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Sentinel.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="AuditLoggingSentinelBuilder"/> — 4 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "AuditLogging")]
public sealed class AuditLoggingSentinelBuilderShould : UnitTestBase
{
    private static (AuditLoggingSentinelBuilder Builder, SentinelExporterOptions Options) CreateBuilder()
    {
        var options = new SentinelExporterOptions
        {
            WorkspaceId = "00000000-0000-0000-0000-000000000000",
            SharedKey = "dGVzdC1zaGFyZWQta2V5"
        };
        var builder = new AuditLoggingSentinelBuilder(options);
        return (builder, options);
    }

    // --- Happy path ---

    [Fact]
    public void WorkspaceId_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.WorkspaceId("11111111-1111-1111-1111-111111111111");
        options.WorkspaceId.ShouldBe("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public void SharedKey_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.SharedKey("bXktc2hhcmVkLWtleQ==");
        options.SharedKey.ShouldBe("bXktc2hhcmVkLWtleQ==");
    }

    [Fact]
    public void LogType_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.LogType("CustomAuditType");
        options.LogType.ShouldBe("CustomAuditType");
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Sentinel");
        builder.BindConfigurationPath.ShouldBe("Audit:Sentinel");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .WorkspaceId("ws-id")
            .SharedKey("key-123")
            .LogType("AuditLog");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Sentinel").ShouldBeSameAs(builder);
    }

    // --- Last-wins: programmatic setters clear BindConfiguration ---

    [Fact]
    public void WorkspaceId_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Sentinel");
        builder.WorkspaceId("new-ws-id");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void SharedKey_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Sentinel");
        builder.SharedKey("new-key");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new AuditLoggingSentinelBuilder(null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WorkspaceId_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.WorkspaceId(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SharedKey_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.SharedKey(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LogType_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.LogType(invalidValue!));
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
