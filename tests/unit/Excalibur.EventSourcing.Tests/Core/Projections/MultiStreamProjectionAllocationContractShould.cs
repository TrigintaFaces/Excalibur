// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class MultiStreamProjectionAllocationContractShould
{
    [Theory]
    [InlineData(null, true, true, "aggregateId", true)]
    [InlineData("", true, true, "aggregateId", false)]
    [InlineData(null, false, false, "aggregateId", true)]
    [InlineData("", false, false, "aggregateId", false)]
    [InlineData("id", true, true, "projection", true)]
    [InlineData("id", false, true, "domainEvent", true)]
    public void PreserveValidationOrder(string? aggregateId, bool nullProjection, bool nullEvent, string parameter, bool nullException)
    {
        var projection = new MultiStreamProjection<State>();
        var exception = Should.Throw<ArgumentException>(() => projection.Apply(
            nullProjection ? null! : new State(), nullEvent ? null! : new BaseEvent(), aggregateId!));

        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(nullException ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptWhitespaceIdentityAndApplyExactlyOnce(bool contextAware)
    {
        var projection = new MultiStreamProjection<State>();
        if (contextAware)
        {
            projection.AddContextHandler<BaseEvent>((state, _, context) =>
            {
                context.AggregateId.ShouldBe(" ");
                state.Count++;
            });
        }
        else
        {
            projection.AddHandler<BaseEvent>((state, _) => state.Count++);
        }

        var state = new State();
        projection.Apply(state, new BaseEvent(), " ").ShouldBeTrue();
        state.Count.ShouldBe(1);
    }

    [Fact]
    public void GiveContextHandlersFreshLiveContextsWithTheSuppliedIdentity()
    {
        var contexts = new List<ProjectionContext>();
        var projection = new MultiStreamProjection<State>();
        projection.AddContextHandler<BaseEvent>((state, _, context) => { contexts.Add(context); state.Count++; });
        var state = new State();
        var domainEvent = new BaseEvent();

        projection.Apply(state, domainEvent, "supplied-one").ShouldBeTrue();
        projection.Apply(state, domainEvent, "supplied-two").ShouldBeTrue();

        contexts.Count.ShouldBe(2);
        contexts[0].ShouldNotBeSameAs(contexts[1]);
        contexts[0].AggregateId.ShouldBe("supplied-one");
        contexts[1].AggregateId.ShouldBe("supplied-two");
        foreach (var context in contexts)
        {
            context.IsReplay.ShouldBeFalse();
            context.GlobalPosition.ShouldBeNull();
        }
        state.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("derived")]
    [InlineData("async")]
    [InlineData("empty")]
    public void PreserveFalseResultsAndExactEventTypeMatching(string kind)
    {
        var projection = new MultiStreamProjection<State>();
        if (kind == "derived") projection.AddHandler<BaseEvent>((state, _) => state.Count++);
        if (kind == "async") projection.AddAsyncHandler<BaseEvent>((state, _, _, _, _) => { state.Count++; return Task.CompletedTask; });
        if (kind == "empty") Entries(projection)[typeof(BaseEvent)] = default;
        IDomainEvent domainEvent = kind == "derived" ? new DerivedEvent() : new BaseEvent();
        var state = new State();

        projection.Apply(state, domainEvent, "id").ShouldBeFalse();
        projection.Apply(state, domainEvent, new ProjectionContext(false, null, "id")).ShouldBeFalse();
        state.Count.ShouldBe(0);
    }

    [Fact]
    public void PreservePlainHandlerPrecedenceForMalformedCombinedEntries()
    {
        var projection = new MultiStreamProjection<State>();
        Entries(projection)[typeof(BaseEvent)] = new(
            (state, _) => state.Count++,
            (_, _, _) => throw new InvalidOperationException("Context handler must not run"),
            (_, _, _, _, _) => throw new InvalidOperationException("Async handler must not run"));
        var state = new State();

        projection.Apply(state, new BaseEvent(), "id").ShouldBeTrue();
        projection.Apply(state, new BaseEvent(), new ProjectionContext(false, null, "id")).ShouldBeTrue();
        state.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropagateTheOriginalHandlerException(bool contextAware)
    {
        var expected = new InvalidOperationException("handler failure");
        var projection = new MultiStreamProjection<State>();
        if (contextAware) projection.AddContextHandler<BaseEvent>((_, _, _) => throw expected);
        else projection.AddHandler<BaseEvent>((_, _) => throw expected);

        Should.Throw<InvalidOperationException>(() => projection.Apply(new State(), new BaseEvent(), "id")).ShouldBeSameAs(expected);
    }

    [Fact]
    public void PreserveContextHandlerPrecedenceOverAnAsyncHandler()
    {
        var projection = new MultiStreamProjection<State>();
        Entries(projection)[typeof(BaseEvent)] = new(
            null,
            (state, _, context) => { context.AggregateId.ShouldBe("id"); state.Count++; },
            (_, _, _, _, _) => throw new InvalidOperationException("Async handler must not run"));
        var state = new State();

        projection.Apply(state, new BaseEvent(), "id").ShouldBeTrue();
        projection.Apply(state, new BaseEvent(), new ProjectionContext(false, null, "id")).ShouldBeTrue();
        state.Count.ShouldBe(2);
    }

    [Fact]
    public void PreserveAnExplicitReplayContextInstance()
    {
        var projection = new MultiStreamProjection<State>();
        var expected = new ProjectionContext(true, 123, "replay-id");
        ProjectionContext? actual = null;
        projection.AddContextHandler<BaseEvent>((_, _, context) => actual = context);

        projection.Apply(new State(), new BaseEvent(), expected).ShouldBeTrue();
        actual.ShouldBeSameAs(expected);
        actual.IsReplay.ShouldBeTrue();
        actual.GlobalPosition.ShouldBe(123);
    }

    private static Dictionary<Type, MultiStreamProjection<State>.ProjectionHandlerEntry> Entries(MultiStreamProjection<State> projection) =>
        (Dictionary<Type, MultiStreamProjection<State>.ProjectionHandlerEntry>)typeof(MultiStreamProjection<State>)
            .GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(projection)!;

    private sealed class State
    {
        public State() { }
        public int Count { get; set; }
    }

    [MessageName("Test.ProjectionAllocation.Base")]
    private class BaseEvent : IDomainEvent
    {
        public string EventId { get; init; } = "event";
        public long Version { get; init; } = 1;
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;
        public IDictionary<string, object>? Metadata { get; init; }
    }

    [MessageName("Test.ProjectionAllocation.Derived")]
    private sealed class DerivedEvent : BaseEvent;
}
