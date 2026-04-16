// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

using Excalibur.EventSourcing.AwsS3;

using Tests.Shared.Categories;

namespace Excalibur.EventSourcing.Tests.AwsS3.Builders;

/// <summary>
/// Unit tests for <see cref="EventSourcingAwsS3Builder"/> — 5 connection overloads,
/// BucketName, KeyPrefix, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "CloudStorage")]
public sealed class EventSourcingAwsS3BuilderShould : UnitTestBase
{
    private static EventSourcingAwsS3Builder CreateBuilder() => new();

    // --- Happy path: additive methods ---

    [Fact]
    public void BucketName_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BucketName("my-bucket");

        builder.BucketNameValue.ShouldBe("my-bucket");
    }

    [Fact]
    public void KeyPrefix_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.KeyPrefix("events/");

        builder.KeyPrefixValue.ShouldBe("events/");
    }

    // --- Happy path: connection methods ---

    [Fact]
    public void ServiceUrl_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.ServiceUrl("http://localhost:4566");

        builder.ServiceUrlValue.ShouldBe("http://localhost:4566");
    }

    [Fact]
    public void Region_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.Region("us-east-1");

        builder.RegionValue.ShouldBe("us-east-1");
    }

    [Fact]
    public void Client_StoreInstanceOnBuilder()
    {
        var builder = CreateBuilder();
        var client = A.Fake<IAmazonS3>();

        builder.Client(client);

        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_StoreFactoryOnBuilder()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonS3> factory = _ => A.Fake<IAmazonS3>();

        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("Aws:S3:ColdStore");

        builder.BindConfigurationPath.ShouldBe("Aws:S3:ColdStore");
    }

    // --- Last-wins semantics: each connection method clears the others ---

    [Fact]
    public void ServiceUrl_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        builder.Client(A.Fake<IAmazonS3>());

        builder.ServiceUrl("http://localhost:4566");

        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ServiceUrlValue.ShouldBe("http://localhost:4566");
    }

    [Fact]
    public void Region_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        builder.ServiceUrl("http://localhost:4566");

        builder.Region("eu-west-1");

        builder.ServiceUrlValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.RegionValue.ShouldBe("eu-west-1");
    }

    [Fact]
    public void Client_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        var client = A.Fake<IAmazonS3>();
        builder.ServiceUrl("http://localhost:4566");

        builder.Client(client);

        builder.ServiceUrlValue.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonS3> factory = _ => A.Fake<IAmazonS3>();
        builder.Region("us-east-1");

        builder.ClientFactory(factory);

        builder.ServiceUrlValue.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_ClearOtherConnectionMethods()
    {
        var builder = CreateBuilder();
        builder.Client(A.Fake<IAmazonS3>());

        builder.BindConfiguration("Aws:S3");

        builder.ServiceUrlValue.ShouldBeNull();
        builder.RegionValue.ShouldBeNull();
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Aws:S3");
    }

    [Fact]
    public void AdditiveProperties_PreservedAcrossConnectionChanges()
    {
        var builder = CreateBuilder();

        builder.BucketName("my-bucket")
            .KeyPrefix("events/")
            .ServiceUrl("http://localhost:4566");

        builder.Region("us-east-1");

        builder.BucketNameValue.ShouldBe("my-bucket");
        builder.KeyPrefixValue.ShouldBe("events/");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .BucketName("my-bucket")
            .KeyPrefix("events/")
            .ServiceUrl("http://localhost:4566");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Region_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.Region("us-east-1");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.Client(A.Fake<IAmazonS3>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.ClientFactory(_ => A.Fake<IAmazonS3>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("Aws:S3");
        result.ShouldBeSameAs(builder);
    }

    // --- Validation guards ---

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
    public void KeyPrefix_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.KeyPrefix(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceUrl_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ServiceUrl(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Region_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Region(invalidValue!));
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
