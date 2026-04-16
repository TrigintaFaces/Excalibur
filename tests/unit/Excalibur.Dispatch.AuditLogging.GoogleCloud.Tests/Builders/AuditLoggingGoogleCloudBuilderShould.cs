// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.GoogleCloud;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="AuditLoggingGoogleCloudBuilder"/> — 5 builder methods,
/// fluent chaining, validation guards, and last-wins semantics for BindConfiguration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "AuditLogging")]
public sealed class AuditLoggingGoogleCloudBuilderShould : UnitTestBase
{
    private static (AuditLoggingGoogleCloudBuilder Builder, GoogleCloudAuditOptions Options) CreateBuilder()
    {
        var options = new GoogleCloudAuditOptions
        {
            ProjectId = "default-project"
        };
        var builder = new AuditLoggingGoogleCloudBuilder(options);
        return (builder, options);
    }

    // --- Happy path ---

    [Fact]
    public void ProjectId_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.ProjectId("my-gcp-project");
        options.ProjectId.ShouldBe("my-gcp-project");
    }

    [Fact]
    public void LogName_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.LogName("custom-audit-log");
        options.LogName.ShouldBe("custom-audit-log");
    }

    [Fact]
    public void BindConfiguration_StoreConfigPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:GoogleCloud");
        builder.BindConfigurationPath.ShouldBe("Audit:GoogleCloud");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .ProjectId("proj-1")
            .LogName("audit");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void CredentialsPath_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.CredentialsPath("/path/to/creds.json").ShouldBeSameAs(builder);
    }

    [Fact]
    public void CredentialsJson_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.CredentialsJson("{\"type\":\"service_account\"}").ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:GCP").ShouldBeSameAs(builder);
    }

    // --- Last-wins: programmatic setters clear BindConfiguration ---

    [Fact]
    public void ProjectId_ClearBindConfigurationPath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:GCP");
        builder.ProjectId("new-project");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new AuditLoggingGoogleCloudBuilder(null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProjectId_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ProjectId(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LogName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.LogName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CredentialsPath_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CredentialsPath(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CredentialsJson_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CredentialsJson(invalidValue!));
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
