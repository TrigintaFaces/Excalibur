// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Storage.Blobs;

using Excalibur.EventSourcing.AzureBlob;

using Tests.Shared.Categories;

namespace Excalibur.EventSourcing.Tests.AzureBlob.Builders;

/// <summary>
/// Unit tests for <see cref="EventSourcingAzureBlobBuilder"/> — 4 connection overloads,
/// ContainerName, CreateContainerIfNotExists, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "CloudStorage")]
public sealed class EventSourcingAzureBlobBuilderShould : UnitTestBase
{
    private const string TestConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdA==;EndpointSuffix=core.windows.net";

    private static EventSourcingAzureBlobBuilder CreateBuilder() => new();

    // --- Happy path: connection methods ---

    [Fact]
    public void ConnectionString_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.ConnectionString(TestConnectionString);

        builder.ConnectionStringValue.ShouldBe(TestConnectionString);
    }

    [Fact]
    public void Client_StoreInstanceOnBuilder()
    {
        var builder = CreateBuilder();
        var client = A.Fake<BlobServiceClient>();

        builder.Client(client);

        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_StoreFactoryOnBuilder()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, BlobServiceClient> factory = _ => A.Fake<BlobServiceClient>();

        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("Azure:BlobStorage:ColdStore");

        builder.BindConfigurationPath.ShouldBe("Azure:BlobStorage:ColdStore");
    }

    // --- Happy path: additive methods ---

    [Fact]
    public void ContainerName_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.ContainerName("cold-events");

        builder.ContainerNameValue.ShouldBe("cold-events");
    }

    [Fact]
    public void CreateContainerIfNotExists_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.CreateContainerIfNotExists(true);

        builder.CreateContainerIfNotExistsValue.ShouldBe(true);
    }

    [Fact]
    public void CreateContainerIfNotExists_DefaultsToTrue()
    {
        var builder = CreateBuilder();

        builder.CreateContainerIfNotExists();

        builder.CreateContainerIfNotExistsValue.ShouldBe(true);
    }

    [Fact]
    public void CreateContainerIfNotExists_SetFalse()
    {
        var builder = CreateBuilder();

        builder.CreateContainerIfNotExists(false);

        builder.CreateContainerIfNotExistsValue.ShouldBe(false);
    }

    // --- Last-wins semantics ---

    [Fact]
    public void ConnectionString_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        builder.Client(A.Fake<BlobServiceClient>());

        builder.ConnectionString(TestConnectionString);

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ConnectionStringValue.ShouldBe(TestConnectionString);
    }

    [Fact]
    public void Client_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        var client = A.Fake<BlobServiceClient>();
        builder.ConnectionString(TestConnectionString);

        builder.Client(client);

        builder.ConnectionStringValue.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, BlobServiceClient> factory = _ => A.Fake<BlobServiceClient>();
        builder.ConnectionString(TestConnectionString);

        builder.ClientFactory(factory);

        builder.ConnectionStringValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        builder.Client(A.Fake<BlobServiceClient>());

        builder.BindConfiguration("Azure:Blob");

        builder.ConnectionStringValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Azure:Blob");
    }

    [Fact]
    public void AdditiveProperties_PreservedAcrossConnectionChanges()
    {
        var builder = CreateBuilder();

        builder.ContainerName("cold-events")
            .CreateContainerIfNotExists()
            .ConnectionString(TestConnectionString);

        builder.Client(A.Fake<BlobServiceClient>());

        builder.ContainerNameValue.ShouldBe("cold-events");
        builder.CreateContainerIfNotExistsValue.ShouldBe(true);
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .ConnectionString(TestConnectionString)
            .ContainerName("cold-events")
            .CreateContainerIfNotExists();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.Client(A.Fake<BlobServiceClient>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.ClientFactory(_ => A.Fake<BlobServiceClient>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("Azure:Blob");
        result.ShouldBeSameAs(builder);
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionString(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ContainerName_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ContainerName(invalidValue!));
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
