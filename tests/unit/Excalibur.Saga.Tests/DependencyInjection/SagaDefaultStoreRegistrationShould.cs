// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// bd-iuv3s1 — saga store registration is fail-fast + opt-in, NOT a silent in-memory default.
/// <para>
/// Saga state is as stateful as the outbox / event store (lost on restart/scale-out), so the framework
/// must NOT silently bind an in-memory store as the production default. <c>AddExcaliburSaga()</c> and
/// <c>AddExcaliburOrchestration()</c> register NO store; the in-memory store is available only via the
/// explicit <c>AddInMemorySagaStore()</c> / <c>UseInMemoryStore()</c> opt-in, and
/// <c>SagaPrerequisiteValidator</c> fails loud at host startup when neither a persistent provider nor the
/// opt-in registered a "default" store. (Pre-fix, <c>AddExcaliburSaga()</c> implicitly registered the
/// in-memory store as "default".)
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.DependencyInjection")]
public sealed class SagaDefaultStoreRegistrationShould
{
    [Fact]
    public void NotRegisterAnyDefaultSagaStoreImplicitly()
    {
        // Arrange — bare saga registration, no explicit store opt-in.
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();

        // Assert — RED on the pre-fix code, which implicitly registered InMemorySagaStore as "default".
        sp.GetKeyedService<ISagaStore>("default").ShouldBeNull();
        sp.GetService<ISagaStore>().ShouldBeNull();
    }

    [Fact]
    public void RegisterInMemoryStoreWhenExplicitlyOptedIn()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        services.AddInMemorySagaStore();
        var sp = services.BuildServiceProvider();

        // Assert — the explicit opt-in resolves the in-memory store, keyed and non-keyed.
        sp.GetKeyedService<ISagaStore>("default").ShouldBeOfType<InMemorySagaStore>();
        sp.GetService<ISagaStore>().ShouldBeOfType<InMemorySagaStore>();
        sp.GetKeyedService<ISagaStore>("inmemory").ShouldBeOfType<InMemorySagaStore>();
    }

    [Fact]
    public void ResolveSameInMemoryInstanceForKeyedAndNonKeyed()
    {
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        services.AddInMemorySagaStore();
        var sp = services.BuildServiceProvider();

        var keyed = sp.GetKeyedService<ISagaStore>("default");
        var nonKeyed = sp.GetService<ISagaStore>();

        keyed.ShouldNotBeNull();
        nonKeyed.ShouldNotBeNull();
        keyed.ShouldBeSameAs(nonKeyed);
    }

    [Fact]
    public void RegisterInMemorySagaStoreAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        services.AddInMemorySagaStore();
        var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<InMemorySagaStore>();
        var second = sp.GetRequiredService<InMemorySagaStore>();

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void AllowPersistentStoreToOverrideTheInMemoryOptIn()
    {
        // Arrange — a persistent provider registers the keyed "default" BEFORE the in-memory opt-in.
        var fakePersistentStore = A.Fake<ISagaStore>();
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        services.AddKeyedSingleton<ISagaStore>("default", (_, _) => fakePersistentStore);
        services.AddInMemorySagaStore();
        var sp = services.BuildServiceProvider();

        // Assert — TryAdd semantics: the persistent store wins, not InMemory.
        sp.GetKeyedService<ISagaStore>("default").ShouldBeSameAs(fakePersistentStore);
    }

    [Fact]
    public async Task FailFastAtStartupWhenNoSagaStoreRegistered()
    {
        // Arrange — sagas configured, but no store opted in.
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        var sp = services.BuildServiceProvider();
        var validator = GetSagaPrerequisiteValidator(sp);

        // Act + Assert — the prerequisite validator fails loud at host start (RED on the pre-fix code,
        // which had no validator and silently bound the in-memory store).
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));
        ex.Message.ShouldContain("ISagaStore");
    }

    [Fact]
    public async Task PassStartupValidationWhenInMemoryStoreOptedIn()
    {
        var services = new ServiceCollection();
        services.AddExcaliburSaga();
        services.AddInMemorySagaStore();
        var sp = services.BuildServiceProvider();
        var validator = GetSagaPrerequisiteValidator(sp);

        // Should not throw — a "default" store is registered.
        await validator.StartAsync(CancellationToken.None);
    }

    private static IHostedService GetSagaPrerequisiteValidator(IServiceProvider sp)
        => sp.GetServices<IHostedService>()
            .Single(h => h.GetType().Name == "SagaPrerequisiteValidator");
}
