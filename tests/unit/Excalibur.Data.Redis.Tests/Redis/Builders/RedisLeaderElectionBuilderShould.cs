// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Redis;

using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis.Builders;

/// <summary>
/// Unit tests for <see cref="RedisLeaderElectionBuilder"/> — 4 connection overloads,
/// feature methods (KeyPrefix, Database, LockKey), last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Redis")]
public sealed class RedisLeaderElectionBuilderShould : UnitTestBase
{
    private const string TestConnectionString = "localhost:6379";

    private static RedisLeaderElectionBuilder CreateBuilder()
    {
        return new RedisLeaderElectionBuilder();
    }

    // --- Connection overloads (happy path) ---

    [Fact]
    public void ConnectionString_StoreValue()
    {
        var builder = CreateBuilder();

        builder.ConnectionString(TestConnectionString);

        builder.ConnectionStringValue.ShouldBe(TestConnectionString);
    }

    [Fact]
    public void ConnectionMultiplexer_StoreInstance()
    {
        var builder = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);

        builder.MultiplexerInstance.ShouldBe(multiplexer);
    }

    [Fact]
    public void ConnectionMultiplexerFactory_StoreFactory()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IConnectionMultiplexer> factory = _ => A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexerFactory(factory);

        builder.MultiplexerFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("LeaderElection:Redis");

        builder.BindConfigurationPath.ShouldBe("LeaderElection:Redis");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void ConnectionString_ClearOtherOverloads()
    {
        var builder = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);
        builder.ConnectionString(TestConnectionString);

        builder.ConnectionStringValue.ShouldBe(TestConnectionString);
        builder.MultiplexerInstance.ShouldBeNull();
        builder.MultiplexerFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ConnectionMultiplexer_ClearOtherOverloads()
    {
        var builder = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionString(TestConnectionString);
        builder.ConnectionMultiplexer(multiplexer);

        builder.ConnectionStringValue.ShouldBeNull();
        builder.MultiplexerInstance.ShouldBe(multiplexer);
        builder.MultiplexerFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ConnectionMultiplexerFactory_ClearOtherOverloads()
    {
        var builder = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();
        Func<IServiceProvider, IConnectionMultiplexer> factory = _ => A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);
        builder.ConnectionMultiplexerFactory(factory);

        builder.ConnectionStringValue.ShouldBeNull();
        builder.MultiplexerInstance.ShouldBeNull();
        builder.MultiplexerFactoryFunc.ShouldBe(factory);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearOtherOverloads()
    {
        var builder = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder.ConnectionMultiplexer(multiplexer);
        builder.BindConfiguration("LE:Redis");

        builder.ConnectionStringValue.ShouldBeNull();
        builder.MultiplexerInstance.ShouldBeNull();
        builder.MultiplexerFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("LE:Redis");
    }

    // --- Feature methods ---

    [Fact]
    public void KeyPrefix_StoreValue()
    {
        var builder = CreateBuilder();

        builder.KeyPrefix("myapp");

        builder.KeyPrefixValue.ShouldBe("myapp");
    }

    [Fact]
    public void Database_StoreValue()
    {
        var builder = CreateBuilder();

        builder.Database(3);

        builder.DatabaseValue.ShouldBe(3);
    }

    [Fact]
    public void LockKey_StoreValue()
    {
        var builder = CreateBuilder();

        builder.LockKey("my-leader-lock");

        builder.LockKeyValue.ShouldBe("my-leader-lock");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .ConnectionString(TestConnectionString)
            .KeyPrefix("prefix")
            .Database(1)
            .LockKey("lock");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ConnectionMultiplexer_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.ConnectionMultiplexer(A.Fake<IConnectionMultiplexer>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ConnectionMultiplexerFactory_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.ConnectionMultiplexerFactory(_ => A.Fake<IConnectionMultiplexer>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("LE:Redis");
        result.ShouldBeSameAs(builder);
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionString_ThrowOnInvalidValue(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionString(value!));
    }

    [Fact]
    public void ConnectionMultiplexer_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ConnectionMultiplexer(null!));
    }

    [Fact]
    public void ConnectionMultiplexerFactory_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ConnectionMultiplexerFactory(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void KeyPrefix_ThrowOnInvalidValue(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.KeyPrefix(value!));
    }

    [Fact]
    public void Database_ThrowOnNegativeValue()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Database(-1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LockKey_ThrowOnInvalidValue(string? value)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.LockKey(value!));
    }

    // --- Composition (3 tests) ---

    [Fact]
    public void Compose_ConnectionStringWithFeatures()
    {
        var builder = CreateBuilder();

        builder
            .ConnectionString(TestConnectionString)
            .KeyPrefix("myapp")
            .Database(2)
            .LockKey("leader");

        builder.ConnectionStringValue.ShouldBe(TestConnectionString);
        builder.KeyPrefixValue.ShouldBe("myapp");
        builder.DatabaseValue.ShouldBe(2);
        builder.LockKeyValue.ShouldBe("leader");
    }

    [Fact]
    public void Compose_MultiplexerWithFeatures()
    {
        var builder = CreateBuilder();
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        builder
            .ConnectionMultiplexer(multiplexer)
            .KeyPrefix("app")
            .Database(5)
            .LockKey("my-lock");

        builder.MultiplexerInstance.ShouldBe(multiplexer);
        builder.KeyPrefixValue.ShouldBe("app");
        builder.DatabaseValue.ShouldBe(5);
        builder.LockKeyValue.ShouldBe("my-lock");
    }

    [Fact]
    public void Compose_FactoryWithFeatures()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, IConnectionMultiplexer> factory = _ => A.Fake<IConnectionMultiplexer>();

        builder
            .ConnectionMultiplexerFactory(factory)
            .KeyPrefix("svc")
            .Database(0)
            .LockKey("election");

        builder.MultiplexerFactoryFunc.ShouldBe(factory);
        builder.KeyPrefixValue.ShouldBe("svc");
        builder.DatabaseValue.ShouldBe(0);
        builder.LockKeyValue.ShouldBe("election");
    }
}
