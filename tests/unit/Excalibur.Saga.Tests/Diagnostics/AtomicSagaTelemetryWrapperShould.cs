// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Diagnostics;

namespace Excalibur.Saga.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "Regression")]
public sealed class AtomicSagaTelemetryWrapperShould
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    public async Task PreserveExactCallAndCountOneOperationForSuccessFaultAndCancellation(int operation, int outcome)
    {
        var type = "atomic-telemetry-" + Guid.NewGuid().ToString("N");
        var primary = new IOException("exact atomic provider primary");
        var leaf = new Leaf { Failure = outcome == 1 ? primary : null, Cancel = outcome == 2 };
        using var listener = new MeterListener();
        var counts = new ConcurrentQueue<(string Operation, long Value)>();
        var durations = new ConcurrentQueue<(string Operation, double Value)>();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == TelemetrySagaStoreDecorator.MeterName) { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "excalibur.saga.operations" && Tag(tags, "saga_type") == type)
            { counts.Enqueue((Tag(tags, "operation"), value)); }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "excalibur.saga.operation_duration" && Tag(tags, "saga_type") == type)
            { durations.Enqueue((Tag(tags, "operation"), value)); }
        });
        listener.Start();
        using var decorator = new TelemetrySagaStoreDecorator(leaf);
        ISagaStore outer = decorator;
        var facet = outer.GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        facet.ShouldNotBeSameAs(leaf);
        var identity = new SagaTransitionIdentity("tenantA", type, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "delivery");
        var inputs = Inputs(identity);
        using var stop = new CancellationTokenSource();
        if (outcome == 2) { await stop.CancelAsync(); }
        if (outcome == 1)
        {
            (await Should.ThrowAsync<IOException>(() => Invoke(facet, operation, inputs, stop.Token))).ShouldBeSameAs(primary);
        }
        else if (outcome == 2)
        {
            var error = await Should.ThrowAsync<OperationCanceledException>(() => Invoke(facet, operation, inputs, stop.Token));
            error.CancellationToken.ShouldBe(stop.Token);
        }
        else
        {
            var actual = await Invoke(facet, operation, inputs, stop.Token);
            if (operation < 3) { actual.ShouldBeSameAs(operation == 0 ? leaf.Snapshot : leaf.Result); }
            else if (operation == 3) { actual.ShouldBeNull(); }
            else { actual.ShouldBe(operation == 4); }
        }
        leaf.Input.ShouldBeSameAs(inputs[operation]);
        if (operation == 3) { leaf.Participant.ShouldBeSameAs(inputs[6]); }
        if (operation == 5) { leaf.DeliveryId.ShouldBeSameAs(inputs[7]); }
        leaf.Token.ShouldBe(stop.Token);
        leaf.Calls.ShouldBe(1);
        var expected = operation switch
        {
            0 => "load_durable", 1 => "lookup_delivery", 2 => "commit_durable",
            3 => "atomic_timer_admission", 4 => "atomic_timeout_current", _ => "atomic_timeout_owned",
        };
        var counter = counts.ShouldHaveSingleItem();
        counter.Operation.ShouldBe(expected); counter.Value.ShouldBe(1);
        var duration = durations.ShouldHaveSingleItem();
        duration.Operation.ShouldBe(expected); duration.Value.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task WithholdUnsupportedCapabilityAndRecheckSupportOnAlreadyDiscoveredFacade()
    {
        using var plain = new TelemetrySagaStoreDecorator(new StateOnly());
        ((ISagaStore)plain).GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeNull();
        var leaf = new Leaf();
        using var decorator = new TelemetrySagaStoreDecorator(leaf);
        var facade = ((ISagaStore)decorator).GetService(typeof(ISagaAtomicTransitionStore))
            .ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        leaf.Advertise = false;
        var identity = new SagaScopeIdentity("tenantA", "state", Guid.NewGuid());
        await Should.ThrowAsync<InvalidOperationException>(() => facade.LoadSnapshotAsync(identity, CancellationToken.None).AsTask());
        leaf.Calls.ShouldBe(0);
        ((ISagaStore)decorator).GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeNull();
    }

    [Theory]
    [InlineData(false,0)]
    [InlineData(false,1)]
    [InlineData(false,2)]
    [InlineData(false,3)]
    [InlineData(true,0)]
    [InlineData(true,1)]
    [InlineData(true,2)]
    [InlineData(true,3)]
    public async Task NeverReplaceDurableAuthorityOrPrimaryFaultWithAThrowingMetricObserver(bool histogram,int outcome)
    {
        var type="throwing-atomic-observer-"+Guid.NewGuid().ToString("N");
        var primary=new IOException("exact durable provider primary");
        var result=new SagaTransitionCommitResult(outcome==3?SagaTransitionCommitOutcome.Unknown:SagaTransitionCommitOutcome.Committed,
            outcome==3?null:1) { Failure=outcome==3?primary:null };
        var leaf=new Leaf { Failure=outcome==1?primary:null,Cancel=outcome==2,Result=result };
        var callbacks=0;
        using var listener=new MeterListener();
        listener.InstrumentPublished=(instrument,owner)=>
        {
            if(instrument.Meter.Name==TelemetrySagaStoreDecorator.MeterName){owner.EnableMeasurementEvents(instrument);}
        };
        listener.SetMeasurementEventCallback<long>((instrument,_,tags,_)=>
        {
            if(!histogram&&instrument.Name=="excalibur.saga.operations"&&Tag(tags,"saga_type")==type)
            {Interlocked.Increment(ref callbacks);throw new InvalidOperationException("throwing counter listener");}
        });
        listener.SetMeasurementEventCallback<double>((instrument,_,tags,_)=>
        {
            if(histogram&&instrument.Name=="excalibur.saga.operation_duration"&&Tag(tags,"saga_type")==type)
            {Interlocked.Increment(ref callbacks);throw new InvalidOperationException("throwing histogram listener");}
        });
        listener.Start();
        using var decorator=new TelemetrySagaStoreDecorator(leaf);
        var facade=((ISagaStore)decorator).GetService(typeof(ISagaAtomicTransitionStore))
            .ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        var identity=new SagaTransitionIdentity("tenantA",type,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"delivery");
        var batch=new SagaPreparedTransition(identity,0,0,0,false,"{}"u8.ToArray(),[],[]);
        using var stop=new CancellationTokenSource();
        if(outcome==2){await stop.CancelAsync();}
        if(outcome==1)
        {
            (await Should.ThrowAsync<IOException>(()=>facade.CommitAsync(batch,stop.Token).AsTask())).ShouldBeSameAs(primary);
        }
        else if(outcome==2)
        {
            (await Should.ThrowAsync<OperationCanceledException>(()=>facade.CommitAsync(batch,stop.Token).AsTask()))
                .CancellationToken.ShouldBe(stop.Token);
        }
        else
        {
            var actual=await facade.CommitAsync(batch,stop.Token);
            actual.ShouldBeSameAs(result);
            actual.Outcome.ShouldBe(result.Outcome);
            actual.Failure.ShouldBeSameAs(result.Failure);
        }
        callbacks.ShouldBe(1);
        leaf.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    public async Task PreserveTimeoutAuthorityAndPrimaryFailureWhenBothMetricObserversThrow(int operation, int outcome)
    {
        var type = "throwing-timeout-observers-" + Guid.NewGuid().ToString("N");
        var primary = new IOException("exact timeout provider fault");
        var leaf = new Leaf { Failure = outcome == 1 ? primary : null, Cancel = outcome == 2 };
        var callbacks = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == TelemetrySagaStoreDecorator.MeterName) { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "excalibur.saga.operations" && Tag(tags, "saga_type") == type)
            { Interlocked.Increment(ref callbacks); throw new InvalidOperationException("counter observer"); }
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "excalibur.saga.operation_duration" && Tag(tags, "saga_type") == type)
            { Interlocked.Increment(ref callbacks); throw new InvalidOperationException("duration observer"); }
        });
        listener.Start();
        using var decorator = new TelemetrySagaStoreDecorator(leaf);
        var facade = ((ISagaStore)decorator).GetService(typeof(ISagaAtomicTransitionStore))
            .ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        var identity = new SagaTransitionIdentity("tenantA", type, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "delivery");
        var inputs = Inputs(identity);
        using var stop = new CancellationTokenSource();
        if (outcome == 2) { await stop.CancelAsync(); }
        if (outcome == 1)
        {
            (await Should.ThrowAsync<IOException>(() => Invoke(facade, operation, inputs, stop.Token))).ShouldBeSameAs(primary);
        }
        else if (outcome == 2)
        {
            (await Should.ThrowAsync<OperationCanceledException>(() => Invoke(facade, operation, inputs, stop.Token)))
                .CancellationToken.ShouldBe(stop.Token);
        }
        else
        {
            var result = await Invoke(facade, operation, inputs, stop.Token);
            if (operation == 3) { result.ShouldBeNull(); }
            else { result.ShouldBe(operation == 4); }
        }
        leaf.Input.ShouldBeSameAs(inputs[operation]);
        if (operation == 3) { leaf.Participant.ShouldBeSameAs(inputs[6]); }
        if (operation == 5) { leaf.DeliveryId.ShouldBeSameAs(inputs[7]); }
        leaf.Token.ShouldBe(stop.Token);
        leaf.Calls.ShouldBe(1);
        callbacks.ShouldBe(2);
    }

    private static object[] Inputs(SagaTransitionIdentity identity)
    {
        var scope = new SagaScopeIdentity(identity.TenantId, identity.SagaType, identity.SagaId);
        return [scope, new SagaDeliveryIdentity(identity.TenantId, identity.SagaType, identity.SagaId, identity.IncarnationId, "delivery"),
            new SagaPreparedTransition(identity, 0, 0, 0, false, "{}"u8.ToArray(), [], []), scope,
            new SagaTimeoutDeliveryIdentity(scope, "original-timeout", identity.IncarnationId, 7), scope,
            A.Fake<ISagaAtomicTimeoutParticipant>(), new string("owned-timeout".ToCharArray())];
    }

    private static async Task<object?> AdmitTimer(ISagaAtomicTransitionStore store, object[] inputs, CancellationToken token)
    {
        await store.ValidateTimerParticipantAsync((SagaScopeIdentity)inputs[3], (ISagaAtomicTimeoutParticipant)inputs[6], token);
        return null;
    }

    private static string Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var pair in tags) { if (pair.Key == key) { return pair.Value?.ToString() ?? string.Empty; } }
        return string.Empty;
    }
    private static async Task<object?> Invoke(ISagaAtomicTransitionStore store, int operation, object[] inputs, CancellationToken token) => operation switch
    {
        0 => await store.LoadSnapshotAsync((SagaScopeIdentity)inputs[0], token),
        1 => await store.FindCommittedDeliveryAsync((SagaDeliveryIdentity)inputs[1], token),
        2 => await store.CommitAsync((SagaPreparedTransition)inputs[2], token),
        3 => await AdmitTimer(store, inputs, token),
        4 => await store.IsTimeoutDeliveryCurrentAsync((SagaTimeoutDeliveryIdentity)inputs[4], token),
        5 => await store.HasOwnedTimeoutIdentityAsync((SagaScopeIdentity)inputs[5], (string)inputs[7], token),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
    private class StateOnly : ISagaStore
    {
        public virtual object? GetService(Type type) => type.IsInstanceOfType(this) ? this : null;
        public Task<T?> LoadAsync<T>(Guid id, CancellationToken token) where T : SagaState => Task.FromResult<T?>(null);
        [RequiresUnreferencedCode("Matches the state-only contract; no serializer runs in this probe.")]
        [RequiresDynamicCode("Matches the state-only contract; no serializer runs in this probe.")]
        public Task SaveAsync<T>(T state, CancellationToken token) where T : SagaState => Task.CompletedTask;
    }
    private sealed class Leaf : StateOnly, ISagaAtomicTransitionStore
    {
        public bool Advertise { get; set; } = true;
        public Exception? Failure { get; set; }
        public bool Cancel { get; set; }
        public int Calls { get; private set; }
        public object? Input { get; private set; }
        public CancellationToken Token { get; private set; }
        public ISagaAtomicTimeoutParticipant? Participant { get; private set; }
        public string? DeliveryId { get; private set; }
        public SagaDurableSnapshot Snapshot { get; } = new(Guid.NewGuid(), 1, 0, 0, false, "{}"u8.ToArray(), null);
        public SagaTransitionCommitResult Result { get; set; } = new(SagaTransitionCommitOutcome.DeliveryAlreadyCommitted, 1);
        public override object? GetService(Type type) => type == typeof(ISagaAtomicTransitionStore) ? Advertise ? this : null : base.GetService(type);
        public async ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity, ISagaAtomicTimeoutParticipant participant, CancellationToken token)
        {
            Participant = participant;
            await Reply(identity, true, token);
        }
        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken token)
            => Reply(delivery, true, token);
        public ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string deliveryId, CancellationToken token)
        {
            DeliveryId = deliveryId;
            return Reply(identity, false, token);
        }
        public ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(SagaScopeIdentity identity, CancellationToken token) => Reply<SagaDurableSnapshot?>(identity, Snapshot, token);
        public ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(SagaDeliveryIdentity identity, CancellationToken token) => Reply(identity, Result, token);
        public ValueTask<SagaTransitionCommitResult> CommitAsync(SagaPreparedTransition transition, CancellationToken token) => Reply(transition, Result, token);
        private ValueTask<T> Reply<T>(object input, T result, CancellationToken token)
        {
            Calls++; Input = input; Token = token;
            if (Failure is not null) { return ValueTask.FromException<T>(Failure); }
            return Cancel ? ValueTask.FromCanceled<T>(token) : ValueTask.FromResult(result);
        }
    }
}
