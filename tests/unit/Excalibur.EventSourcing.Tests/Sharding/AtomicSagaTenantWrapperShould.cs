// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Sharding;

namespace Excalibur.EventSourcing.Tests.Sharding;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "Regression")]
public sealed class AtomicSagaTenantWrapperShould
{
    private static readonly int[] Operations = [0, 1, 2, 3, 4, 5];
    private static readonly int[] TimerOperations = [3, 4, 5];
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(false, 5)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    [InlineData(true, 5)]
    public async Task ForwardExactInputsAndOutcomeThroughTheSelectedOuterAuthority(bool routed, int operation)
    {
        var tenant = new FlowTenant { Current = "tenantA " };
        var leaf = new AtomicLeaf();
        var wrapper = Wrap(routed, tenant, leaf);
        var facade = wrapper.GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        facade.ShouldNotBeSameAs(leaf);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var input = Inputs(tenant.Current.ShouldNotBeNull());
        var actual = await Invoke(facade, operation, input, budget.Token);
        if (operation < 3) { actual.ShouldBeSameAs(operation == 0 ? leaf.Snapshot : leaf.Result); }
        else if (operation == 3) { actual.ShouldBeNull(); }
        else { actual.ShouldBe(false); }
        var call = leaf.Calls.ShouldHaveSingleItem();
        call.Input.ShouldBeSameAs(input[operation]);
        call.Token.ShouldBe(budget.Token);
        if (operation == 3) { call.Secondary.ShouldBeSameAs(input[6]); }
        if (operation == 5) { call.Secondary.ShouldBeSameAs(input[7]); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevalidateTheTenantForEveryCallWithoutCapturingTheDiscoveryTenant(bool routed)
    {
        var tenant = new FlowTenant { Current = "tenantA" };
        var leaf = new AtomicLeaf();
        var wrapper = Wrap(routed, tenant, leaf);
        var facade = wrapper.GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        tenant.Current = "tenantB";
        var wrong = Inputs("tenantA");
        await Should.ThrowAsync<InvalidOperationException>(() => Invoke(facade, 2, wrong, CancellationToken.None));
        leaf.Calls.ShouldBeEmpty();
        foreach (var operation in TimerOperations)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => Invoke(facade, operation, wrong, CancellationToken.None));
        }
        tenant.Current = null;
        await Should.ThrowAsync<TenantRequiredException>(() => Invoke(facade, 0, wrong, CancellationToken.None));
        leaf.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveTheCurrentFacetAgainAndFailWhenSupportDisappears(bool routed)
    {
        var tenant = new FlowTenant { Current = "tenantA" };
        var leaf = new AtomicLeaf();
        var changing = new SwitchingLeaf { Capability = leaf };
        var wrapper = Wrap(routed, tenant, changing);
        var facade = wrapper.GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        changing.Capability = null;
        foreach (var operation in Operations)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => Invoke(facade, operation, Inputs("tenantA"), CancellationToken.None));
        }
        leaf.Calls.ShouldBeEmpty();
        wrapper.GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeNull();
    }

    [Fact]
    public async Task RouteSeparateFlowsThroughTheCurrentShardRatherThanTheDiscoveryShard()
    {
        var tenant = new FlowTenant { Current = "tenantA" };
        var first = new AtomicLeaf();
        var second = new AtomicLeaf();
        var resolver = new Resolver(new Dictionary<string, ISagaStore>(StringComparer.Ordinal)
        { ["tenantA"] = first, ["tenantB"] = second });
        ISagaStore wrapper = new TenantRoutingSagaStore(resolver, tenant);
        var facade = wrapper.GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        await Task.WhenAll(Task.Run(async () =>
        {
            tenant.Current = "tenantA";
            await Invoke(facade, 2, Inputs("tenantA"), CancellationToken.None);
        }), Task.Run(async () =>
        {
            tenant.Current = "tenantB";
            await Invoke(facade, 2, Inputs("tenantB"), CancellationToken.None);
        }));
        first.Calls.ShouldHaveSingleItem().Input.ShouldBeOfType<SagaPreparedTransition>().ShouldNotBeNull().Identity.TenantId.ShouldBe("tenantA");
        second.Calls.ShouldHaveSingleItem().Input.ShouldBeOfType<SagaPreparedTransition>().ShouldNotBeNull().Identity.TenantId.ShouldBe("tenantB");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveStateOnlyModesAndWithholdUnknownWrapperCapabilities(bool routed)
    {
        var tenant = new FlowTenant { Current = "tenantA" };
        var raw = new AtomicLeaf();
        var unknown = new UnknownWrapper(raw);
        var wrapper = Wrap(routed, tenant, unknown);
        wrapper.GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeNull();
        wrapper.GetService(typeof(IDisposable)).ShouldBeNull();
        await wrapper.LoadAsync<State>(Guid.NewGuid(), CancellationToken.None);
        unknown.LoadCalls.ShouldBe(1);
        raw.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveExactProviderFaultAndCancellationWithoutWeakFallback(bool routed)
    {
        var tenant = new FlowTenant { Current = "tenantA" };
        var primary = new IOException("exact provider primary");
        var leaf = new AtomicLeaf { Failure = primary };
        var facade = Wrap(routed, tenant, leaf).GetService(typeof(ISagaAtomicTransitionStore))
            .ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        (await Should.ThrowAsync<IOException>(() => Invoke(facade, 2, Inputs("tenantA"), CancellationToken.None))).ShouldBeSameAs(primary);
        leaf.Failure = null;
        leaf.Cancel = true;
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        var error = await Should.ThrowAsync<OperationCanceledException>(() => Invoke(facade, 1, Inputs("tenantA"), stop.Token));
        error.CancellationToken.ShouldBe(stop.Token);
        leaf.Calls.Count.ShouldBe(2);
        foreach (var operation in TimerOperations)
        {
            leaf.Cancel = false;
            leaf.Failure = primary;
            (await Should.ThrowAsync<IOException>(() => Invoke(facade, operation, Inputs("tenantA"), CancellationToken.None))).ShouldBeSameAs(primary);
            leaf.Failure = null;
            leaf.Cancel = true;
            var canceled = await Should.ThrowAsync<OperationCanceledException>(() => Invoke(facade, operation, Inputs("tenantA"), stop.Token));
            canceled.CancellationToken.ShouldBe(stop.Token);
        }
        leaf.Calls.Count.ShouldBe(8);
    }

    private static ISagaStore Wrap(bool routed, FlowTenant tenant, ISagaStore leaf) => routed
        ? new TenantRoutingSagaStore(new Resolver(new Dictionary<string, ISagaStore>(StringComparer.Ordinal)
            { [tenant.Current.ShouldNotBeNull()] = leaf }), tenant)
        : new TenantScopedSagaStore(leaf, tenant);

    private static object[] Inputs(string tenant)
    {
        var identity = new SagaTransitionIdentity(tenant, "known-state", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "delivery");
        return [new SagaScopeIdentity(tenant, identity.SagaType, identity.SagaId),
            new SagaDeliveryIdentity(tenant, identity.SagaType, identity.SagaId, identity.IncarnationId, identity.DeliveryId.ShouldNotBeNull()),
            new SagaPreparedTransition(identity, 0, 0, 0, false, "{}"u8.ToArray(), [], []),
            new SagaScopeIdentity(tenant, identity.SagaType, identity.SagaId),
            new SagaTimeoutDeliveryIdentity(new SagaScopeIdentity(tenant, identity.SagaType, identity.SagaId), "timeout", identity.IncarnationId, 0),
            new SagaScopeIdentity(tenant, identity.SagaType, identity.SagaId),
            A.Fake<ISagaAtomicTimeoutParticipant>(), "owned-timeout"];
    }
    private static async Task<object?> Invoke(ISagaAtomicTransitionStore store, int operation, object[] inputs, CancellationToken token) => operation switch
    {
        0 => await store.LoadSnapshotAsync((SagaScopeIdentity)inputs[0], token),
        1 => await store.FindCommittedDeliveryAsync((SagaDeliveryIdentity)inputs[1], token),
        2 => await store.CommitAsync((SagaPreparedTransition)inputs[2], token),
        3 => await Validate(store, (SagaScopeIdentity)inputs[3], (ISagaAtomicTimeoutParticipant)inputs[6], token),
        4 => await store.IsTimeoutDeliveryCurrentAsync((SagaTimeoutDeliveryIdentity)inputs[4], token),
        5 => await store.HasOwnedTimeoutIdentityAsync((SagaScopeIdentity)inputs[5], (string)inputs[7], token),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
    private static async Task<object?> Validate(ISagaAtomicTransitionStore store, SagaScopeIdentity identity,
        ISagaAtomicTimeoutParticipant participant, CancellationToken token)
    {
        await store.ValidateTimerParticipantAsync(identity, participant, token);
        return null;
    }
    private sealed class FlowTenant : ITenantContext
    {
        private readonly AsyncLocal<string?> _current = new();
        public string? Current { get => _current.Value; set => _current.Value = value; }
        public string? TenantId => Current;
        public bool HasTenant => !string.IsNullOrEmpty(Current);
    }
    private sealed class Resolver(IReadOnlyDictionary<string, ISagaStore> stores) : ITenantStoreResolver<ISagaStore>
    {
        public ISagaStore Resolve(string tenantId) => stores[tenantId];
    }
    private sealed class State : SagaState;
    private class StateOnlyLeaf : ISagaStore
    {
        public int LoadCalls { get; protected set; }
        public virtual object? GetService(Type type) => type.IsInstanceOfType(this) ? this : null;
        public virtual Task<T?> LoadAsync<T>(Guid id, CancellationToken token) where T : SagaState { LoadCalls++; return Task.FromResult<T?>(null); }
        [RequiresUnreferencedCode("Matches the state-only store contract; no serialization runs in this probe.")]
        [RequiresDynamicCode("Matches the state-only store contract; no serialization runs in this probe.")]
        public Task SaveAsync<T>(T state, CancellationToken token) where T : SagaState => Task.CompletedTask;
    }
    private sealed class UnknownWrapper(ISagaStore inner) : StateOnlyLeaf
    {
        public override Task<T?> LoadAsync<T>(Guid id, CancellationToken token) where T : class
        { LoadCalls++; return inner.LoadAsync<T>(id, token); }
    }
    private sealed class SwitchingLeaf : StateOnlyLeaf
    {
        public ISagaAtomicTransitionStore? Capability { get; set; }
        public override object? GetService(Type type) => type == typeof(ISagaAtomicTransitionStore) ? Capability : base.GetService(type);
    }
    private sealed class AtomicLeaf : StateOnlyLeaf, ISagaAtomicTransitionStore
    {
        public ConcurrentQueue<(object Input, object? Secondary, CancellationToken Token)> Calls { get; } = new();
        public SagaDurableSnapshot Snapshot { get; } = new(Guid.NewGuid(), 1, 0, 0, false, "{}"u8.ToArray(), null);
        public SagaTransitionCommitResult Result { get; } = new(SagaTransitionCommitOutcome.DeliveryAlreadyCommitted, 1);
        public Exception? Failure { get; set; }
        public bool Cancel { get; set; }
        public ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(SagaScopeIdentity identity, CancellationToken token) => Reply<SagaDurableSnapshot?>(identity, Snapshot, null, token);
        public ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(SagaDeliveryIdentity identity, CancellationToken token) => Reply(identity, Result, null, token);
        public ValueTask<SagaTransitionCommitResult> CommitAsync(SagaPreparedTransition transition, CancellationToken token) => Reply(transition, Result, null, token);
        public async ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity, ISagaAtomicTimeoutParticipant participant, CancellationToken token)
            => _ = await Reply(identity, false, participant, token);
        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken token)
            => Reply(delivery, false, null, token);
        public ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string deliveryId, CancellationToken token)
            => Reply(identity, false, deliveryId, token);
        private ValueTask<T> Reply<T>(object input, T result, object? secondary, CancellationToken token)
        {
            Calls.Enqueue((input, secondary, token));
            if (Failure is not null) { return ValueTask.FromException<T>(Failure); }
            return Cancel ? ValueTask.FromCanceled<T>(token) : ValueTask.FromResult(result);
        }
    }
}
