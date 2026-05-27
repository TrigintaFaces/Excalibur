// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Tests for InMemorySagaStore default DI registration (bd-5yg2g8).
/// Validates that <c>AddExcaliburSaga()</c> registers a usable ISagaStore
/// without requiring <c>WithCoordination()</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.DependencyInjection")]
public sealed class SagaDefaultStoreRegistrationShould
{
    [Fact]
    public void ResolveInMemorySagaStoreWithoutWithCoordination()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();

        // Act
        var store = sp.GetService<InMemorySagaStore>();

        // Assert
        store.ShouldNotBeNull();
    }

    [Fact]
    public void ResolveISagaStoreViaDefaultKeyedService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();

        // Act
        var store = sp.GetKeyedService<ISagaStore>("default");

        // Assert
        store.ShouldNotBeNull();
        store.ShouldBeOfType<InMemorySagaStore>();
    }

    [Fact]
    public void ResolveISagaStoreViaConvenienceNonKeyedAlias()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();

        // Act
        var store = sp.GetService<ISagaStore>();

        // Assert
        store.ShouldNotBeNull();
        store.ShouldBeOfType<InMemorySagaStore>();
    }

    [Fact]
    public void ReturnSameInstanceForKeyedAndNonKeyedResolution()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();

        // Act
        var keyed = sp.GetKeyedService<ISagaStore>("default");
        var nonKeyed = sp.GetService<ISagaStore>();

        // Assert — both should resolve from the same InMemorySagaStore singleton
        keyed.ShouldNotBeNull();
        nonKeyed.ShouldNotBeNull();
        keyed.ShouldBeSameAs(nonKeyed);
    }

    [Fact]
    public void AllowPersistentStoreToOverrideDefault()
    {
        // Arrange — register a fake persistent store BEFORE AddExcaliburSaga
        var fakePersistentStore = A.Fake<ISagaStore>();
        var services = new ServiceCollection();

        // Register persistent store first (TryAdd in AddExcaliburSaga should not override)
        services.AddSingleton(fakePersistentStore);
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();

        // Act
        var store = sp.GetService<ISagaStore>();

        // Assert — persistent store wins, not InMemory
        store.ShouldNotBeNull();
        store.ShouldBeSameAs(fakePersistentStore);
    }

    [Fact]
    public void RegisterInMemorySagaStoreAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();

        // Act — resolve twice
        var first = sp.GetRequiredService<InMemorySagaStore>();
        var second = sp.GetRequiredService<InMemorySagaStore>();

        // Assert — same instance (singleton)
        first.ShouldBeSameAs(second);
    }
}
