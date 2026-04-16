// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

namespace Excalibur.Data.Tests.DynamoDb.Builders;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcBuilder"/> — CDC-specific builder methods,
/// state store configuration, argument guards, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "DynamoDB")]
public sealed class DynamoDbCdcBuilderShould : UnitTestBase
{
    private static DynamoDbCdcBuilder CreateBuilder()
    {
        var options = new DynamoDbCdcOptions();
        return new DynamoDbCdcBuilder(options);
    }

    private static (DynamoDbCdcBuilder Builder, DynamoDbCdcOptions Options) CreateBuilderWithOptions()
    {
        var options = new DynamoDbCdcOptions();
        var builder = new DynamoDbCdcBuilder(options);
        return (builder, options);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new DynamoDbCdcBuilder(null!));
    }

    // --- Feature methods (happy path) ---

    [Fact]
    public void TableName_SetOnOptions()
    {
        var (builder, options) = CreateBuilderWithOptions();

        builder.TableName("orders");

        options.TableName.ShouldBe("orders");
    }

    [Fact]
    public void StreamArn_SetOnOptions()
    {
        var (builder, options) = CreateBuilderWithOptions();

        builder.StreamArn("arn:aws:dynamodb:us-east-1:123456789:table/orders/stream/2026-01-01");

        options.StreamArn.ShouldBe("arn:aws:dynamodb:us-east-1:123456789:table/orders/stream/2026-01-01");
    }

    [Fact]
    public void ProcessorName_SetOnOptions()
    {
        var (builder, options) = CreateBuilderWithOptions();

        builder.ProcessorName("my-processor");

        options.ProcessorName.ShouldBe("my-processor");
    }

    [Fact]
    public void MaxBatchSize_SetOnOptions()
    {
        var (builder, options) = CreateBuilderWithOptions();

        builder.MaxBatchSize(500);

        options.MaxBatchSize.ShouldBe(500);
    }

    [Fact]
    public void PollInterval_SetOnOptions()
    {
        var (builder, options) = CreateBuilderWithOptions();

        builder.PollInterval(TimeSpan.FromSeconds(5));

        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("Cdc:DynamoDB");

        builder.SourceBindConfigurationPath.ShouldBe("Cdc:DynamoDB");
    }

    // --- WithStateStore ---

    [Fact]
    public void WithStateStore_StoreClientFactory()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();

        builder.WithStateStore(factory);

        builder.StateClientFactory.ShouldBeSameAs(factory);
    }

    [Fact]
    public void WithStateStore_WithConfigure_StoreBothCallbacks()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();
        Action<ICdcStateStoreBuilder> configure = _ => { };

        builder.WithStateStore(factory, configure);

        builder.StateClientFactory.ShouldBeSameAs(factory);
        builder.StateStoreConfigure.ShouldBeSameAs(configure);
    }

    // --- Null/invalid argument guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TableName_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().TableName(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StreamArn_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().StreamArn(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessorName_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().ProcessorName(value!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void MaxBatchSize_ThrowOnNonPositive(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateBuilder().MaxBatchSize(value));
    }

    [Fact]
    public void PollInterval_ThrowOnZero()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateBuilder().PollInterval(TimeSpan.Zero));
    }

    [Fact]
    public void PollInterval_ThrowOnNegative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateBuilder().PollInterval(TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnNullOrWhitespace(string? value)
    {
        Should.Throw<ArgumentException>(() => CreateBuilder().BindConfiguration(value!));
    }

    [Fact]
    public void WithStateStore_ThrowOnNullFactory()
    {
        Should.Throw<ArgumentNullException>(() =>
            CreateBuilder().WithStateStore((Func<IServiceProvider, IAmazonDynamoDB>)null!));
    }

    [Fact]
    public void WithStateStore_WithConfigure_ThrowOnNullFactory()
    {
        Should.Throw<ArgumentNullException>(() =>
            CreateBuilder().WithStateStore(null!, _ => { }));
    }

    [Fact]
    public void WithStateStore_WithConfigure_ThrowOnNullConfigure()
    {
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();
        Should.Throw<ArgumentNullException>(() =>
            CreateBuilder().WithStateStore(factory, null!));
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .TableName("orders")
            .StreamArn("arn:aws:dynamodb:us-east-1:123:table/orders/stream/2026")
            .ProcessorName("my-cdc")
            .MaxBatchSize(200)
            .PollInterval(TimeSpan.FromSeconds(2))
            .BindConfiguration("Cdc:DynamoDB");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void WithStateStore_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();

        var result = builder.WithStateStore(factory);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void WithStateStore_WithConfigure_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => A.Fake<IAmazonDynamoDB>();

        var result = builder.WithStateStore(factory, _ => { });

        result.ShouldBeSameAs(builder);
    }
}
