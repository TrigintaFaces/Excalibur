// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Collection("HandlerInvokerRegistry")]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Handler")]
public sealed class HandlerInvokerOverloadShould : IDisposable
{
    private readonly HandlerInvoker _invoker = new();

    public HandlerInvokerOverloadShould()
    {
        HandlerInvoker.ClearCache();
        HandlerInvokerRegistry.ClearCache();
    }

    public void Dispose()
    {
        HandlerInvoker.ClearCache();
        HandlerInvokerRegistry.ClearCache();
    }

    [Fact]
    public async Task SelectPublicEventOverloadsBeforeAndAfterFreeze()
    {
        var handler = new PublicEvents();
        var first = new FirstEvent();
        var second = new SecondEvent();
        (await _invoker.InvokeAsync(handler, first, CancellationToken.None)).ShouldBeNull();
        (await _invoker.InvokeAsync(handler, second, CancellationToken.None)).ShouldBeNull();
        HandlerInvoker.FreezeCache();
        (await _invoker.InvokeAsync(handler, first, CancellationToken.None)).ShouldBeNull();
        (await _invoker.InvokeAsync(handler, second, CancellationToken.None)).ShouldBeNull();
        handler.FirstCount.ShouldBe(2);
        handler.SecondCount.ShouldBe(2);
        handler.LastFirst.ShouldBeSameAs(first);
        handler.LastSecond.ShouldBeSameAs(second);
    }

    [Fact]
    public async Task SelectExplicitEventContracts()
    {
        var handler = new ExplicitEvents();
        await _invoker.InvokeAsync(handler, new FirstEvent(), CancellationToken.None);
        await _invoker.InvokeAsync(handler, new SecondEvent(), CancellationToken.None);
        handler.FirstCount.ShouldBe(1);
        handler.SecondCount.ShouldBe(1);
    }

    [Fact]
    public async Task InvokeInheritedContravariantContract()
    {
        var handler = new InheritedEvents();
        var message = new DerivedEvent();
        await _invoker.InvokeAsync(handler, message, CancellationToken.None);
        handler.Last.ShouldBeSameAs(message);
    }

    [Fact]
    public async Task PreserveDistinctTypedResults()
    {
        var handler = new TypedActions();
        var id = Guid.NewGuid();
        (await _invoker.InvokeAsync(handler, new IdAction(id), CancellationToken.None)).ShouldBe(id);
        (await _invoker.InvokeAsync(handler, new TextAction("payload"), CancellationToken.None)).ShouldBe("payload");
    }

    [Fact]
    public async Task PreferNarrowerContractForColdFrozenAndStartupKnownPairs()
    {
        HandlerInvoker.FreezeCache();
        var handler = new SpecificEvents();
        await _invoker.InvokeAsync(handler, new FurtherEvent(), CancellationToken.None);
        handler.NarrowCount.ShouldBe(1);
        handler.BroadCount.ShouldBe(0);
        HandlerInvoker.ClearCache();
        HandlerInvoker.PreWarmGeneratedInvokerCache([new HandlerRegistryEntry(typeof(DerivedEvent), typeof(SpecificEvents), false)]);
        await _invoker.InvokeAsync(handler, new DerivedEvent(), CancellationToken.None);
        handler.NarrowCount.ShouldBe(2);
        handler.BroadCount.ShouldBe(0);
    }

    [Fact]
    public async Task RefuseIncomparableContractsWithoutInvokingEither()
    {
        var handler = new AmbiguousEvents();
        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            _invoker.InvokeAsync(handler, new CombinedEvent(), CancellationToken.None));
        error.Message.ShouldContain("Ambiguous HandleAsync contracts");
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CollapseContractsSharingOneImplementation()
    {
        var handler = new SharedContractHandler();
        await _invoker.InvokeAsync(handler, new ActionEvent(), CancellationToken.None);
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task RefuseDistinctResponseContractsWithoutGuessing()
    {
        var handler = new AmbiguousActions<object>();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _invoker.InvokeAsync(handler, new CombinedAction(), CancellationToken.None));
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task SelectDuckTypedOverloadAndPreserveValueTaskResult()
    {
        var handler = new DuckOverloads();
        (await _invoker.InvokeAsync(handler, new DerivedEvent(), CancellationToken.None)).ShouldBe("narrow");
        (await _invoker.InvokeAsync(handler, new FurtherEvent(), CancellationToken.None)).ShouldBe("narrow");
        (await _invoker.InvokeAsync(handler, new BaseEvent(), CancellationToken.None)).ShouldBe("broad");
        handler.UnrelatedCalls.ShouldBe(0);
    }

    private sealed class FirstEvent : IDispatchEvent;
    private sealed class SecondEvent : IDispatchEvent;
    private class BaseEvent : IDispatchEvent;
    private class DerivedEvent : BaseEvent;
    private sealed class FurtherEvent : DerivedEvent;
    private interface ILeftEvent : IDispatchEvent;
    private interface IRightEvent : IDispatchEvent;
    private sealed class CombinedEvent : ILeftEvent, IRightEvent;
    private sealed class ActionEvent : IDispatchAction, IDispatchEvent;
    private sealed class CombinedAction : IDispatchAction<Guid>, IDispatchAction<string>;
    private sealed record IdAction(Guid Id) : IDispatchAction<Guid>;
    private sealed record TextAction(string Text) : IDispatchAction<string>;

    private sealed class PublicEvents : IEventHandler<FirstEvent>, IEventHandler<SecondEvent>
    {
        public int FirstCount { get; private set; }
        public int SecondCount { get; private set; }
        public FirstEvent? LastFirst { get; private set; }
        public SecondEvent? LastSecond { get; private set; }
        public Task HandleAsync(FirstEvent message, CancellationToken cancellationToken)
        { FirstCount++; LastFirst = message; return Task.CompletedTask; }
        public Task HandleAsync(SecondEvent message, CancellationToken cancellationToken)
        { SecondCount++; LastSecond = message; return Task.CompletedTask; }
    }

    private sealed class ExplicitEvents : IEventHandler<FirstEvent>, IEventHandler<SecondEvent>
    {
        public int FirstCount { get; private set; }
        public int SecondCount { get; private set; }
        Task IEventHandler<FirstEvent>.HandleAsync(FirstEvent message, CancellationToken cancellationToken)
        { FirstCount++; return Task.CompletedTask; }
        Task IEventHandler<SecondEvent>.HandleAsync(SecondEvent message, CancellationToken cancellationToken)
        { SecondCount++; return Task.CompletedTask; }
    }

    private class BaseEvents : IEventHandler<BaseEvent>
    {
        public BaseEvent? Last { get; private set; }
        public Task HandleAsync(BaseEvent message, CancellationToken cancellationToken)
        { Last = message; return Task.CompletedTask; }
    }
    private sealed class InheritedEvents : BaseEvents;

    private sealed class TypedActions : IActionHandler<IdAction, Guid>, IActionHandler<TextAction, string>
    {
        public Task<Guid> HandleAsync(IdAction action, CancellationToken cancellationToken) => Task.FromResult(action.Id);
        public Task<string> HandleAsync(TextAction action, CancellationToken cancellationToken) => Task.FromResult(action.Text);
    }

    private sealed class SpecificEvents : IEventHandler<BaseEvent>, IEventHandler<DerivedEvent>
    {
        public int BroadCount { get; private set; }
        public int NarrowCount { get; private set; }
        public Task HandleAsync(BaseEvent message, CancellationToken cancellationToken)
        { BroadCount++; return Task.CompletedTask; }
        public Task HandleAsync(DerivedEvent message, CancellationToken cancellationToken)
        { NarrowCount++; return Task.CompletedTask; }
    }

    private sealed class AmbiguousEvents : IEventHandler<ILeftEvent>, IEventHandler<IRightEvent>
    {
        public int Calls { get; private set; }
        Task IEventHandler<ILeftEvent>.HandleAsync(ILeftEvent message, CancellationToken cancellationToken)
        { Calls++; return Task.CompletedTask; }
        Task IEventHandler<IRightEvent>.HandleAsync(IRightEvent message, CancellationToken cancellationToken)
        { Calls++; return Task.CompletedTask; }
    }

    private sealed class SharedContractHandler : IActionHandler<ActionEvent>, IEventHandler<ActionEvent>
    {
        public int Calls { get; private set; }
        public Task HandleAsync(ActionEvent message, CancellationToken cancellationToken)
        { Calls++; return Task.CompletedTask; }
    }

    // Open generic fixtures are excluded from zero-configuration assembly scanning; this deliberately
    // invalid closed contract belongs only to the explicit refusal control above.
    private sealed class AmbiguousActions<T> : IActionHandler<CombinedAction, Guid>, IActionHandler<CombinedAction, string>
    {
        public int Calls { get; private set; }
        Task<Guid> IActionHandler<CombinedAction, Guid>.HandleAsync(CombinedAction action, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Guid.Empty); }
        Task<string> IActionHandler<CombinedAction, string>.HandleAsync(CombinedAction action, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult("wrong"); }
    }

    private sealed class DuckOverloads
    {
        public int UnrelatedCalls { get; private set; }
        public Task<string> HandleAsync(BaseEvent message, CancellationToken cancellationToken) => Task.FromResult("broad");
        public ValueTask<string> HandleAsync(DerivedEvent message, CancellationToken cancellationToken) => ValueTask.FromResult("narrow");
        public Task HandleAsync(string unrelated, CancellationToken cancellationToken)
        { UnrelatedCalls++; return Task.CompletedTask; }
    }
}
