// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Gcs;

using Google.Cloud.Storage.V1;

using Tests.Shared.Categories;

namespace Excalibur.EventSourcing.Tests.Gcs.Builders;

/// <summary>
/// Unit tests for <see cref="EventSourcingGcsBuilder"/> — 5 connection overloads,
/// ProjectId, BucketName, ObjectPrefix, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "CloudStorage")]
public sealed class EventSourcingGcsBuilderShould : UnitTestBase
{
    private static EventSourcingGcsBuilder CreateBuilder() => new();

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

        builder.BucketName("cold-events-bucket");

        builder.BucketNameValue.ShouldBe("cold-events-bucket");
    }

    [Fact]
    public void ObjectPrefix_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.ObjectPrefix("events/archived/");

        builder.ObjectPrefixValue.ShouldBe("events/archived/");
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

        builder.BindConfiguration("Gcs:ColdStore");

        builder.BindConfigurationPath.ShouldBe("Gcs:ColdStore");
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

        builder.BindConfiguration("Gcs:ColdStore");

        builder.CredentialsPathValue.ShouldBeNull();
        builder.CredentialsJsonValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Gcs:ColdStore");
    }

    [Fact]
    public void AdditiveProperties_PreservedAcrossConnectionChanges()
    {
        var builder = CreateBuilder();

        builder.ProjectId("my-project")
            .BucketName("my-bucket")
            .ObjectPrefix("events/")
            .CredentialsPath("/path/to/creds.json");

        builder.BindConfiguration("Gcs:ColdStore");

        builder.ProjectIdValue.ShouldBe("my-project");
        builder.BucketNameValue.ShouldBe("my-bucket");
        builder.ObjectPrefixValue.ShouldBe("events/");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .ProjectId("my-project")
            .BucketName("my-bucket")
            .ObjectPrefix("events/")
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
        var result = builder.BindConfiguration("Gcs:ColdStore");
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
    public void ObjectPrefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ObjectPrefix(invalidValue!));
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
