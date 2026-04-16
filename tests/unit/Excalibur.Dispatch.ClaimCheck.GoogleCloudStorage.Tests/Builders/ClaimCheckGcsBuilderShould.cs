// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;

using Google.Cloud.Storage.V1;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ClaimCheckGcsBuilder"/> — 5 connection overloads,
/// ProjectId, BucketName, Prefix, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "ClaimCheck")]
public sealed class ClaimCheckGcsBuilderShould : UnitTestBase
{
    private static ClaimCheckGcsBuilder CreateBuilder() => new();

    // --- Happy path: additive methods ---

    [Fact]
    public void ProjectId_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.ProjectId("my-gcp-project");

        builder.ProjectIdValue.ShouldBe("my-gcp-project");
    }

    [Fact]
    public void BucketName_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BucketName("claim-check-bucket");

        builder.BucketNameValue.ShouldBe("claim-check-bucket");
    }

    [Fact]
    public void Prefix_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.Prefix("claims/");

        builder.PrefixValue.ShouldBe("claims/");
    }

    // --- Happy path: connection methods ---

    [Fact]
    public void CredentialsPath_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.CredentialsPath("/path/to/credentials.json");

        builder.CredentialsPathValue.ShouldBe("/path/to/credentials.json");
    }

    [Fact]
    public void CredentialsJson_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.CredentialsJsonValue.ShouldBe("{\"type\":\"service_account\"}");
    }

    [Fact]
    public void Client_StoreInstanceOnBuilder()
    {
        var builder = CreateBuilder();
        var client = A.Fake<StorageClient>();

        builder.Client(client);

        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_StoreFactoryOnBuilder()
    {
        var builder = CreateBuilder();
        var clientInstance = A.Fake<StorageClient>();
        Func<IServiceProvider, StorageClient> factory = _ => clientInstance;

        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("ClaimCheck:Gcs");

        builder.BindConfigurationPath.ShouldBe("ClaimCheck:Gcs");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void CredentialsPath_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.CredentialsPath("/path/to/credentials.json");

        builder.CredentialsJsonValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.CredentialsPathValue.ShouldBe("/path/to/credentials.json");
    }

    [Fact]
    public void CredentialsJson_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        builder.CredentialsPath("/path/to/credentials.json");

        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.CredentialsPathValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.CredentialsJsonValue.ShouldBe("{\"type\":\"service_account\"}");
    }

    [Fact]
    public void Client_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        var client = A.Fake<StorageClient>();
        builder.CredentialsPath("/path/to/credentials.json");

        builder.Client(client);

        builder.CredentialsPathValue.ShouldBeNull();
        builder.CredentialsJsonValue.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        var clientInstance = A.Fake<StorageClient>();
        Func<IServiceProvider, StorageClient> factory = _ => clientInstance;
        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.ClientFactory(factory);

        builder.CredentialsPathValue.ShouldBeNull();
        builder.CredentialsJsonValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        var client = A.Fake<StorageClient>();
        builder.Client(client);

        builder.BindConfiguration("ClaimCheck:Gcs");

        builder.CredentialsPathValue.ShouldBeNull();
        builder.CredentialsJsonValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("ClaimCheck:Gcs");
    }

    [Fact]
    public void AdditiveProperties_PreservedAcrossConnectionChanges()
    {
        var builder = CreateBuilder();

        builder.ProjectId("my-project")
            .BucketName("my-bucket")
            .Prefix("claims/")
            .CredentialsPath("/path/to/creds.json");

        builder.BindConfiguration("ClaimCheck:Gcs");

        builder.ProjectIdValue.ShouldBe("my-project");
        builder.BucketNameValue.ShouldBe("my-bucket");
        builder.PrefixValue.ShouldBe("claims/");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .ProjectId("my-project")
            .BucketName("my-bucket")
            .Prefix("claims/")
            .CredentialsPath("/path/to/creds.json");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void CredentialsJson_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.CredentialsJson("{\"type\":\"service_account\"}");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.Client(A.Fake<StorageClient>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.ClientFactory(_ => A.Fake<StorageClient>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("ClaimCheck:Gcs");
        result.ShouldBeSameAs(builder);
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProjectId_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ProjectId(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BucketName_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BucketName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Prefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Prefix(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CredentialsPath_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CredentialsPath(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CredentialsJson_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CredentialsJson(invalidValue!));
    }

    [Fact]
    public void Client_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.Client(null!));
    }

    [Fact]
    public void ClientFactory_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ClientFactory(null!));
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
}
