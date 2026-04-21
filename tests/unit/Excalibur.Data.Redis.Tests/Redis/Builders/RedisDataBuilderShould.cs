// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis;

using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis.Builders;

/// <summary>
/// Unit tests for <see cref="RedisDataBuilder"/> — 4 connection overloads,
/// feature methods, last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Redis")]
public sealed class RedisDataBuilderShould : UnitTestBase
{
    private const string TestConnectionString = "localhost:6379";

    private static (RedisDataBuilder Builder, RedisProviderOptions Options) CreateBuilder()
    {
        var options = new RedisProviderOptions();
        var builder = new RedisDataBuilder(options);
        return (builder, options);
    }

    // --- Connection overloads (happy path) ---

    [Fact]
    public void ConnectionString_SetConnectionStringOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.ConnectionString(TestConnectionString);

        options.ConnectionString.ShouldBe(TestConnectionString);
        builder.ConnectionStringValue.ShouldBe(TestConnectionString);
    }

    [Fact]
    public void ConnectionMultiplexer_StoreInstance()
    {
        var (builder, _) = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);

        builder.MultiplexerInstance.ShouldBe(multiplexer);
    }

    [Fact]
    public void ConnectionMultiplexerFactory_StoreFactory()
    {
        var (builder, _) = CreateBuilder();
        Func<IServiceProvider, IConnectionMultiplexer> factory = _ => A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexerFactory(factory);

        builder.MultiplexerFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("Data:Redis");

        builder.BindConfigurationPath.ShouldBe("Data:Redis");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void ConnectionString_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);
        builder.ConnectionString(TestConnectionString);

        options.ConnectionString.ShouldBe(TestConnectionString);
        builder.MultiplexerInstance.ShouldBeNull();
        builder.MultiplexerFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ConnectionMultiplexer_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionString(TestConnectionString);
        builder.ConnectionMultiplexer(multiplexer);

        options.ConnectionString.ShouldBe(null!);
        builder.MultiplexerInstance.ShouldBe(multiplexer);
        builder.MultiplexerFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ConnectionMultiplexerFactory_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();
        Func<IServiceProvider, IConnectionMultiplexer> factory = _ => A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);
        builder.ConnectionMultiplexerFactory(factory);

        options.ConnectionString.ShouldBe(null!);
        builder.MultiplexerInstance.ShouldBeNull();
        builder.MultiplexerFactoryFunc.ShouldBe(factory);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);
        builder.BindConfiguration("Data:Redis");

        options.ConnectionString.ShouldBe(null!);
        builder.MultiplexerInstance.ShouldBeNull();
        builder.MultiplexerFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Data:Redis");
    }

    // --- Feature methods ---

    [Fact]
    public void KeyPrefix_StoreValue()
    {
        var (builder, _) = CreateBuilder();

        builder.KeyPrefix("myapp");

        builder.KeyPrefixValue.ShouldBe("myapp");
    }

    [Fact]
    public void Database_StoreValue()
    {
        var (builder, _) = CreateBuilder();

        builder.Database(3);

        builder.DatabaseValue.ShouldBe(3);
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();

        var result = builder
            .ConnectionString(TestConnectionString)
            .KeyPrefix("prefix")
            .Database(1);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ConnectionMultiplexer_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ConnectionMultiplexer(A.Fake<IConnectionMultiplexer>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ConnectionMultiplexerFactory_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ConnectionMultiplexerFactory(_ => A.Fake<IConnectionMultiplexer>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.BindConfiguration("Data:Redis");
        result.ShouldBeSameAs(builder);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RedisDataBuilder(null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionString_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionString(value!));
    }

    [Fact]
    public void ConnectionMultiplexer_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ConnectionMultiplexer(null!));
    }

    [Fact]
    public void ConnectionMultiplexerFactory_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ConnectionMultiplexerFactory(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void KeyPrefix_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.KeyPrefix(value!));
    }

    [Fact]
    public void Database_ThrowOnNegativeValue()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Database(-1));
    }

    // --- Composition (3 tests) ---

    [Fact]
    public void Compose_ConnectionStringWithFeatures()
    {
        var (builder, options) = CreateBuilder();

        builder
            .ConnectionString(TestConnectionString)
            .KeyPrefix("myapp")
            .Database(2);

        options.ConnectionString.ShouldBe(TestConnectionString);
        builder.KeyPrefixValue.ShouldBe("myapp");
        builder.DatabaseValue.ShouldBe(2);
    }

    [Fact]
    public void Compose_MultiplexerWithFeatures()
    {
        var (builder, _) = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder
            .ConnectionMultiplexer(multiplexer)
            .KeyPrefix("app")
            .Database(5);

        builder.MultiplexerInstance.ShouldBe(multiplexer);
        builder.KeyPrefixValue.ShouldBe("app");
        builder.DatabaseValue.ShouldBe(5);
    }

    [Fact]
    public void Compose_FactoryWithFeatures()
    {
        var (builder, _) = CreateBuilder();
        Func<IServiceProvider, IConnectionMultiplexer> factory = _ => A.Fake<IConnectionMultiplexer>();

        builder
            .ConnectionMultiplexerFactory(factory)
            .KeyPrefix("svc")
            .Database(0);

        builder.MultiplexerFactoryFunc.ShouldBe(factory);
        builder.KeyPrefixValue.ShouldBe("svc");
        builder.DatabaseValue.ShouldBe(0);
    }
}
