// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.Firestore;

using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

namespace Excalibur.Data.Tests.Firestore.Builders;

/// <summary>
/// Unit tests for <see cref="FirestoreInboxBuilder"/> — 8 connection/config methods,
/// Firestore-specific last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Firestore")]
public sealed class FirestoreInboxBuilderShould : UnitTestBase
{
    private static (FirestoreInboxBuilder Builder, FirestoreInboxOptions Options) CreateBuilder()
    {
        var options = new FirestoreInboxOptions();
        var builder = new FirestoreInboxBuilder(options);
        return (builder, options);
    }

    private static FirestoreDb CreateTestClient()
        => FirestoreDb.Create("test-project", A.Fake<FirestoreClient>());

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new FirestoreInboxBuilder(null!));
    }

    // --- Connection overloads (happy path) ---

    [Fact]
    public void ProjectId_StoreValueOnBuilderAndOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.ProjectId("my-project");

        builder.ProjectIdValue.ShouldBe("my-project");
        options.ProjectId.ShouldBe("my-project");
    }

    [Fact]
    public void CredentialsPath_StoreValueOnBuilderAndOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.CredentialsPath("/path/to/creds.json");

        builder.CredentialsPathValue.ShouldBe("/path/to/creds.json");
        options.CredentialsPath.ShouldBe("/path/to/creds.json");
    }

    [Fact]
    public void CredentialsJson_StoreValueOnBuilderAndOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.CredentialsJsonValue.ShouldBe("{\"type\":\"service_account\"}");
        options.CredentialsJson.ShouldBe("{\"type\":\"service_account\"}");
    }

    [Fact]
    public void EmulatorHost_StoreValueOnBuilderAndOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.EmulatorHost("localhost:8080");

        builder.EmulatorHostValue.ShouldBe("localhost:8080");
        options.EmulatorHost.ShouldBe("localhost:8080");
    }

    [Fact]
    public void Client_StoreClientInstanceOnBuilder()
    {
        var (builder, _) = CreateBuilder();
        var client = CreateTestClient();

        builder.Client(client);

        builder.ClientInstance.ShouldBeSameAs(client);
    }

    [Fact]
    public void ClientFactory_StoreFactoryOnBuilder()
    {
        var (builder, _) = CreateBuilder();
        Func<IServiceProvider, FirestoreDb> factory = _ => CreateTestClient();

        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("Firestore:Inbox");

        builder.BindConfigurationPath.ShouldBe("Firestore:Inbox");
    }

    [Fact]
    public void CollectionName_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.CollectionName("my-inbox");

        builder.CollectionNameValue.ShouldBe("my-inbox");
        options.CollectionName.ShouldBe("my-inbox");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void CredentialsPath_ClearCredentialsJson()
    {
        var (builder, options) = CreateBuilder();

        builder.CredentialsJson("{\"type\":\"service_account\"}");
        builder.CredentialsPath("/path/to/creds.json");

        builder.CredentialsPathValue.ShouldBe("/path/to/creds.json");
        builder.CredentialsJsonValue.ShouldBeNull();
        options.CredentialsJson.ShouldBeNull();
    }

    [Fact]
    public void CredentialsJson_ClearCredentialsPath()
    {
        var (builder, options) = CreateBuilder();

        builder.CredentialsPath("/path/to/creds.json");
        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.CredentialsJsonValue.ShouldBe("{\"type\":\"service_account\"}");
        builder.CredentialsPathValue.ShouldBeNull();
        options.CredentialsPath.ShouldBeNull();
    }

    [Fact]
    public void EmulatorHost_ClearClientFactoryBindConfigAndCredentials()
    {
        var (builder, options) = CreateBuilder();
        var client = CreateTestClient();

        builder.Client(client);
        builder.CredentialsPath("/path/to/creds.json");
        builder.EmulatorHost("localhost:8080");

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.CredentialsPathValue.ShouldBeNull();
        builder.CredentialsJsonValue.ShouldBeNull();
        options.CredentialsPath.ShouldBeNull();
        options.CredentialsJson.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBe("localhost:8080");
    }

    [Fact]
    public void Client_ClearFactoryEmulatorAndBindConfig()
    {
        var (builder, options) = CreateBuilder();
        var client = CreateTestClient();

        builder.EmulatorHost("localhost:8080");
        builder.Client(client);

        builder.ClientFactoryFunc.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        options.EmulatorHost.ShouldBeNull();
        builder.ClientInstance.ShouldBeSameAs(client);
    }

    [Fact]
    public void ClientFactory_ClearClientEmulatorAndBindConfig()
    {
        var (builder, options) = CreateBuilder();
        var client = CreateTestClient();
        Func<IServiceProvider, FirestoreDb> factory = _ => CreateTestClient();

        builder.Client(client);
        builder.ClientFactory(factory);

        builder.ClientInstance.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        options.EmulatorHost.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_ClearClientFactoryAndEmulator()
    {
        var (builder, options) = CreateBuilder();
        var client = CreateTestClient();

        builder.Client(client);
        builder.BindConfiguration("Firestore:Inbox");

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBeNull();
        options.EmulatorHost.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Firestore:Inbox");
    }

    [Fact]
    public void ProjectId_AdditiveWithCredentialsPath()
    {
        var (builder, _) = CreateBuilder();

        builder.CredentialsPath("/path/to/creds.json");
        builder.ProjectId("my-project");

        builder.ProjectIdValue.ShouldBe("my-project");
        builder.CredentialsPathValue.ShouldBe("/path/to/creds.json");
    }

    [Fact]
    public void ProjectId_AdditiveWithClient()
    {
        var (builder, _) = CreateBuilder();
        var client = CreateTestClient();

        builder.Client(client);
        builder.ProjectId("my-project");

        builder.ProjectIdValue.ShouldBe("my-project");
        builder.ClientInstance.ShouldBeSameAs(client);
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();

        var result = builder
            .ProjectId("my-project")
            .CredentialsPath("/path/to/creds.json")
            .CollectionName("my-inbox");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void EmulatorHost_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.EmulatorHost("localhost:8080");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.Client(CreateTestClient());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ClientFactory(_ => CreateTestClient());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.BindConfiguration("Firestore:Inbox");
        result.ShouldBeSameAs(builder);
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
    public void EmulatorHost_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.EmulatorHost(invalidValue!));
    }

    [Fact]
    public void Client_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.Client(null!));
    }

    [Fact]
    public void ClientFactory_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ClientFactory(null!));
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CollectionName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CollectionName(invalidValue!));
    }
}
