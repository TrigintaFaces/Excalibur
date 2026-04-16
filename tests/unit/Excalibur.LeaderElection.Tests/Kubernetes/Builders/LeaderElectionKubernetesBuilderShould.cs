// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Kubernetes;

using Tests.Shared.Categories;

namespace Excalibur.LeaderElection.Tests.Kubernetes.Builders;

/// <summary>
/// Unit tests for <see cref="LeaderElectionKubernetesBuilder"/> — Namespace, LeaseName,
/// LeaseIdentity, LeaseDuration, RenewDeadline, RetryPeriod, InCluster,
/// BindConfiguration, last-wins semantics, fluent chaining, and validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "LeaderElection")]
public sealed class LeaderElectionKubernetesBuilderShould : UnitTestBase
{
    private static LeaderElectionKubernetesBuilder CreateBuilder() => new();

    // --- Happy path: additive methods ---

    [Fact]
    public void Namespace_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.Namespace("my-namespace");

        builder.NamespaceValue.ShouldBe("my-namespace");
    }

    [Fact]
    public void LeaseName_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.LeaseName("my-lease");

        builder.LeaseNameValue.ShouldBe("my-lease");
    }

    [Fact]
    public void LeaseIdentity_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.LeaseIdentity("pod-abc-123");

        builder.LeaseIdentityValue.ShouldBe("pod-abc-123");
    }

    [Fact]
    public void LeaseDuration_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.LeaseDuration(15);

        builder.LeaseDurationSeconds.ShouldBe(15);
    }

    [Fact]
    public void RenewDeadline_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.RenewDeadline(10000);

        builder.RenewDeadlineMilliseconds.ShouldBe(10000);
    }

    [Fact]
    public void RetryPeriod_StoreValueOnBuilder()
    {
        var builder = CreateBuilder();

        builder.RetryPeriod(2000);

        builder.RetryPeriodMilliseconds.ShouldBe(2000);
    }

    // --- Happy path: connection methods ---

    [Fact]
    public void InCluster_SetFlagOnBuilder()
    {
        var builder = CreateBuilder();

        builder.InCluster();

        builder.UseInCluster.ShouldBeTrue();
    }

    [Fact]
    public void BindConfiguration_StorePathOnBuilder()
    {
        var builder = CreateBuilder();

        builder.BindConfiguration("K8s:LeaderElection");

        builder.BindConfigurationPath.ShouldBe("K8s:LeaderElection");
    }

    // --- Last-wins semantics: InCluster vs BindConfiguration ---

    [Fact]
    public void InCluster_ClearBindConfiguration()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("K8s:LeaderElection");

        builder.InCluster();

        builder.BindConfigurationPath.ShouldBeNull();
        builder.UseInCluster.ShouldBeTrue();
    }

    [Fact]
    public void BindConfiguration_ClearInCluster()
    {
        var builder = CreateBuilder();
        builder.InCluster();

        builder.BindConfiguration("K8s:LeaderElection");

        builder.UseInCluster.ShouldBeFalse();
        builder.BindConfigurationPath.ShouldBe("K8s:LeaderElection");
    }

    [Fact]
    public void AdditiveProperties_PreservedAcrossConnectionChanges()
    {
        var builder = CreateBuilder();

        builder.Namespace("my-ns")
            .LeaseName("my-lease")
            .LeaseIdentity("pod-1")
            .LeaseDuration(15)
            .RenewDeadline(10000)
            .RetryPeriod(2000)
            .InCluster();

        builder.BindConfiguration("K8s:LE");

        builder.NamespaceValue.ShouldBe("my-ns");
        builder.LeaseNameValue.ShouldBe("my-lease");
        builder.LeaseIdentityValue.ShouldBe("pod-1");
        builder.LeaseDurationSeconds.ShouldBe(15);
        builder.RenewDeadlineMilliseconds.ShouldBe(10000);
        builder.RetryPeriodMilliseconds.ShouldBe(2000);
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();

        var result = builder
            .Namespace("my-ns")
            .LeaseName("my-lease")
            .LeaseIdentity("pod-1")
            .LeaseDuration(15)
            .RenewDeadline(10000)
            .RetryPeriod(2000)
            .InCluster();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        var result = builder.BindConfiguration("K8s:LE");
        result.ShouldBeSameAs(builder);
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Namespace_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.Namespace(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LeaseName_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.LeaseName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LeaseIdentity_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.LeaseIdentity(invalidValue!));
    }

    [Fact]
    public void LeaseDuration_ThrowOnZero()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.LeaseDuration(0));
    }

    [Fact]
    public void LeaseDuration_ThrowOnNegative()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.LeaseDuration(-1));
    }

    [Fact]
    public void RenewDeadline_ThrowOnZero()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RenewDeadline(0));
    }

    [Fact]
    public void RenewDeadline_ThrowOnNegative()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RenewDeadline(-5));
    }

    [Fact]
    public void RetryPeriod_ThrowOnZero()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RetryPeriod(0));
    }

    [Fact]
    public void RetryPeriod_ThrowOnNegative()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RetryPeriod(-100));
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
