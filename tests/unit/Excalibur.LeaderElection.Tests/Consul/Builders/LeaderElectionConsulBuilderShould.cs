// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Consul;

using Tests.Shared.Categories;

namespace Excalibur.LeaderElection.Tests.Consul.Builders;

/// <summary>
/// Unit tests for <see cref="LeaderElectionConsulBuilder"/> — Address, Token, Datacenter,
/// SessionTtl, LockKey, BindConfiguration, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "LeaderElection")]
public sealed class LeaderElectionConsulBuilderShould : UnitTestBase
{
    private static LeaderElectionConsulBuilder CreateBuilder() => new();

    // --- Happy path: connection methods ---

    [Fact]
    public void Address_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.Address("http://localhost:8500");

        builder.AddressValue.ShouldBe("http://localhost:8500");
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("Consul:LeaderElection");

        builder.BindConfigurationPath.ShouldBe("Consul:LeaderElection");
    }

    // --- Happy path: additive methods ---

    [Fact]
    public void Token_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.Token("my-acl-token");

        builder.TokenValue.ShouldBe("my-acl-token");
    }

    [Fact]
    public void Datacenter_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.Datacenter("dc1");

        builder.DatacenterValue.ShouldBe("dc1");
    }

    [Fact]
    public void SessionTtl_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.SessionTtl(TimeSpan.FromSeconds(30));

        builder.SessionTtlValue.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void LockKey_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.LockKey("service/leader");

        builder.LockKeyValue.ShouldBe("service/leader");
    }

    // --- Last-wins semantics: Address vs BindConfiguration ---

    [Fact]
    public void Address_ClearBindConfiguration()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("Consul:LeaderElection");

        builder.Address("http://localhost:8500");

        builder.BindConfigurationPath.ShouldBeNull();
        builder.AddressValue.ShouldBe("http://localhost:8500");
    }

    [Fact]
    public void BindConfiguration_ClearAddress()
    {
        var builder = CreateBuilder();
        builder.Address("http://localhost:8500");

        builder.BindConfiguration("Consul:LeaderElection");

        builder.AddressValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Consul:LeaderElection");
    }

    [Fact]
    public void AdditiveProperties_PreservedAcrossConnectionChanges()
    {
        var builder = CreateBuilder();

        builder.Token("my-token")
            .Datacenter("dc1")
            .SessionTtl(TimeSpan.FromSeconds(30))
            .LockKey("service/leader")
            .Address("http://localhost:8500");

        builder.BindConfiguration("Consul:LeaderElection");

        builder.TokenValue.ShouldBe("my-token");
        builder.DatacenterValue.ShouldBe("dc1");
        builder.SessionTtlValue.ShouldBe(TimeSpan.FromSeconds(30));
        builder.LockKeyValue.ShouldBe("service/leader");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .Address("http://localhost:8500")
            .Token("my-token")
            .Datacenter("dc1")
            .SessionTtl(TimeSpan.FromSeconds(30))
            .LockKey("service/leader");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("Consul:LeaderElection");
        result.ShouldBeSameAs(builder);
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Address_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Address(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Token_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Token(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Datacenter_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Datacenter(invalidValue!));
    }

    [Fact]
    public void SessionTtl_ThrowOnZero()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SessionTtl(TimeSpan.Zero));
    }

    [Fact]
    public void SessionTtl_ThrowOnNegative()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SessionTtl(TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LockKey_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.LockKey(invalidValue!));
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
