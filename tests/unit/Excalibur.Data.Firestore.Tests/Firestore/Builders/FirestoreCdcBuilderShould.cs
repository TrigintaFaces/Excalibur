// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Firestore;

using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

namespace Excalibur.Data.Tests.Firestore.Builders;

/// <summary>
/// Unit tests for <see cref="FirestoreCdcBuilder"/> — 6 connection overloads,
/// 7 domain methods, Firestore-specific last-wins semantics, fluent chaining,
/// and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Firestore")]
public sealed class FirestoreCdcBuilderShould : UnitTestBase
{
    private static FirestoreDb CreateTestClient()
        => FirestoreDb.Create("test-project", A.Fake<FirestoreClient>());

    private static (FirestoreCdcBuilder Builder, FirestoreCdcOptions Options) CreateBuilder()
    {
        var options = new FirestoreCdcOptions();
        var builder = new FirestoreCdcBuilder(options);
        return (builder, options);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new FirestoreCdcBuilder(null!));
    }

    // --- Connection overloads (happy path) ---

    [Fact]
    public void ProjectId_StoreValueOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.ProjectId("my-project");

        builder.ProjectIdValue.ShouldBe("my-project");
    }

    [Fact]
    public void CredentialsPath_StoreValueOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.CredentialsPath("/path/to/creds.json");

        builder.CredentialsPathValue.ShouldBe("/path/to/creds.json");
    }

    [Fact]
    public void CredentialsJson_StoreValueOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.CredentialsJsonValue.ShouldBe("{\"type\":\"service_account\"}");
    }

    [Fact]
    public void EmulatorHost_StoreValueOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.EmulatorHost("localhost:8080");

        builder.EmulatorHostValue.ShouldBe("localhost:8080");
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

    // --- Domain methods (happy path) ---

    [Fact]
    public void CollectionPath_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.CollectionPath("users");

        options.CollectionPath.ShouldBe("users");
    }

    [Fact]
    public void ProcessorName_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.ProcessorName("my-processor");

        options.ProcessorName.ShouldBe("my-processor");
    }

    [Fact]
    public void MaxBatchSize_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.MaxBatchSize(500);

        options.MaxBatchSize.ShouldBe(500);
    }

    [Fact]
    public void PollInterval_SetValueOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.PollInterval(TimeSpan.FromSeconds(5));

        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WithStateStore_StoreConfigureCallback()
    {
        var (builder, _) = CreateBuilder();
        Action<ICdcStateStoreBuilder> configure = _ => { };

        builder.WithStateStore(configure);

        builder.StateStoreConfigure.ShouldBe(configure);
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("Firestore:Cdc");

        builder.SourceBindConfigurationPath.ShouldBe("Firestore:Cdc");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void CredentialsPath_ClearCredentialsJson()
    {
        var (builder, _) = CreateBuilder();

        builder.CredentialsJson("{\"type\":\"service_account\"}");
        builder.CredentialsPath("/path/to/creds.json");

        builder.CredentialsPathValue.ShouldBe("/path/to/creds.json");
        builder.CredentialsJsonValue.ShouldBeNull();
    }

    [Fact]
    public void CredentialsJson_ClearCredentialsPath()
    {
        var (builder, _) = CreateBuilder();

        builder.CredentialsPath("/path/to/creds.json");
        builder.CredentialsJson("{\"type\":\"service_account\"}");

        builder.CredentialsJsonValue.ShouldBe("{\"type\":\"service_account\"}");
        builder.CredentialsPathValue.ShouldBeNull();
    }

    [Fact]
    public void EmulatorHost_ClearClientFactoryBindConfigAndCredentials()
    {
        var (builder, _) = CreateBuilder();
        var client = CreateTestClient();

        builder.Client(client);
        builder.CredentialsPath("/path/to/creds.json");
        builder.EmulatorHost("localhost:8080");

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.SourceBindConfigurationPath.ShouldBeNull();
        builder.CredentialsPathValue.ShouldBeNull();
        builder.CredentialsJsonValue.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBe("localhost:8080");
    }

    [Fact]
    public void Client_ClearFactoryEmulatorAndBindConfig()
    {
        var (builder, _) = CreateBuilder();
        Func<IServiceProvider, FirestoreDb> factory = _ => CreateTestClient();
        var client = CreateTestClient();

        builder.ClientFactory(factory);
        builder.EmulatorHost("localhost:8080");
        builder.Client(client);

        builder.ClientFactoryFunc.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBeNull();
        builder.SourceBindConfigurationPath.ShouldBeNull();
        builder.ClientInstance.ShouldBeSameAs(client);
    }

    [Fact]
    public void ClientFactory_ClearClientEmulatorAndBindConfig()
    {
        var (builder, _) = CreateBuilder();
        var client = CreateTestClient();
        Func<IServiceProvider, FirestoreDb> factory = _ => CreateTestClient();

        builder.Client(client);
        builder.EmulatorHost("localhost:8080");
        builder.ClientFactory(factory);

        builder.ClientInstance.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBeNull();
        builder.SourceBindConfigurationPath.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_ClearClientFactoryAndEmulator()
    {
        var (builder, _) = CreateBuilder();
        var client = CreateTestClient();

        builder.Client(client);
        builder.BindConfiguration("Firestore:Cdc");

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.EmulatorHostValue.ShouldBeNull();
        builder.SourceBindConfigurationPath.ShouldBe("Firestore:Cdc");
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

    [Fact]
    public void ProjectId_AdditiveWithEmulatorHost()
    {
        var (builder, _) = CreateBuilder();

        builder.EmulatorHost("localhost:8080");
        builder.ProjectId("my-project");

        builder.ProjectIdValue.ShouldBe("my-project");
        builder.EmulatorHostValue.ShouldBe("localhost:8080");
    }

    // --- Fluent chaining ---

    [Fact]
    public void ProjectId_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ProjectId("my-project");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void CredentialsPath_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.CredentialsPath("/path/to/creds.json");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void CredentialsJson_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.CredentialsJson("{\"type\":\"service_account\"}");
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
    public void CollectionPath_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.CollectionPath("users");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ProcessorName_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ProcessorName("my-processor");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void MaxBatchSize_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.MaxBatchSize(500);
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void PollInterval_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.PollInterval(TimeSpan.FromSeconds(5));
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void WithStateStore_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.WithStateStore(_ => { });
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.BindConfiguration("Firestore:Cdc");
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
    public void CollectionPath_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CollectionPath(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessorName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ProcessorName(invalidValue!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void MaxBatchSize_ThrowOnInvalidValue(int invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxBatchSize(invalidValue));
    }

    [Fact]
    public void PollInterval_ThrowOnZero()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.PollInterval(TimeSpan.Zero));
    }

    [Fact]
    public void PollInterval_ThrowOnNegative()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.PollInterval(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WithStateStore_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.WithStateStore(null!));
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
