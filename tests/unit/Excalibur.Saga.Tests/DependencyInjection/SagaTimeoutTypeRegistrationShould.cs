// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "Saga")]
public sealed class SagaTimeoutTypeRegistrationShould
{
    [Fact]
    public void RegisterBothDeclaredTimeoutPayloads()
    {
        using var provider = CreateProvider();
        var registry = provider.GetRequiredService<ISagaTypeRegistry>();
        registry.ResolveType(typeof(FirstTimeout).AssemblyQualifiedName!).ShouldBe(typeof(FirstTimeout));
        registry.ResolveType(typeof(SecondTimeout).AssemblyQualifiedName!).ShouldBe(typeof(SecondTimeout));
    }

    [Fact]
    public void PreserveSagaAndStateRegistration()
    {
        using var provider = CreateProvider();
        var registry = provider.GetRequiredService<ISagaTypeRegistry>();
        registry.ResolveType(typeof(TimedSaga).AssemblyQualifiedName!).ShouldBe(typeof(TimedSaga));
        registry.ResolveType(typeof(TimedState).AssemblyQualifiedName!).ShouldBe(typeof(TimedState));
    }

    [Fact]
    public void RefuseUndeclaredPayload()
    {
        using var provider = CreateProvider();
        provider.GetRequiredService<ISagaTypeRegistry>().ResolveType(typeof(UndeclaredTimeout).AssemblyQualifiedName!).ShouldBeNull();
    }

    [Fact]
    public void RetainFrozenRegistryAfterComposition()
    {
        using var provider = CreateProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<ISagaTypeRegistry>().RegisterType(typeof(UndeclaredTimeout)));
    }

    [Fact]
    public void RegisterInheritedTimeoutContracts()
    {
        using var provider = CreateProvider(inherited: true);
        var registry = provider.GetRequiredService<ISagaTypeRegistry>();
        registry.ResolveType(typeof(FirstTimeout).AssemblyQualifiedName!).ShouldBe(typeof(FirstTimeout));
        registry.ResolveType(typeof(SecondTimeout).AssemblyQualifiedName!).ShouldBe(typeof(SecondTimeout));
        registry.ResolveType(typeof(TimedSaga).AssemblyQualifiedName!).ShouldBeNull();
    }

    [Fact]
    public void PermitRepeatedCompositionWithoutLosingTimeoutTypes()
    {
        using var provider = CreateProvider(repeat: true);
        provider.GetRequiredService<ISagaTypeRegistry>().ResolveType(typeof(FirstTimeout).AssemblyQualifiedName!).ShouldBe(typeof(FirstTimeout));
    }

    private static ServiceProvider CreateProvider(bool inherited = false, bool repeat = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (inherited) services.AddSaga<InheritedTimedSaga, TimedState>();
        else services.AddSaga<TimedSaga, TimedState>();
        if (repeat) services.AddSaga<TimedSaga, TimedState>();
        var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<SagaOptions>>().Value;
        return provider;
    }

    private sealed class TimedState : SagaState;
    private sealed class FirstTimeout : IDispatchEvent;
    private sealed class SecondTimeout : IDispatchEvent;
    private sealed class UndeclaredTimeout : IDispatchEvent;
    private class TimedSaga(TimedState state, IDispatcher dispatcher, ILogger<TimedSaga> logger)
        : SagaBase<TimedState>(state, dispatcher, logger), ISagaTimeout<FirstTimeout>, ISagaTimeout<SecondTimeout>
    {
        public override bool HandlesEvent(object eventMessage) => false;
        public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.FromResult(SagaEventOutcome.Declined);
        public Task HandleTimeoutAsync(FirstTimeout message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleTimeoutAsync(SecondTimeout message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class InheritedTimedSaga(TimedState state, IDispatcher dispatcher, ILogger<TimedSaga> logger)
        : TimedSaga(state, dispatcher, logger);
}
