// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;
using System.Net;
using Excalibur.LeaderElection.Kubernetes;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

internal sealed class KubeTenureFixture : IAsyncDisposable
{
    internal readonly IKubernetes Client = A.Fake<IKubernetes>();
    internal readonly ICoordinationV1Operations Api = A.Fake<ICoordinationV1Operations>();
    internal readonly KubeTenureClock Clock = new();
    private readonly Lock _serverGate = new();
    private V1Lease _record = new()
    {
        Metadata = new V1ObjectMeta { Name = "orders", NamespaceProperty = "test", Uid = "original-uid", ResourceVersion = "1", Annotations = new Dictionary<string, string>(StringComparer.Ordinal) },
        Spec = new V1LeaseSpec { HolderIdentity = null, LeaseDurationSeconds = 15, LeaseTransitions = 0 }
    };
    private int _version = 1;
    internal KubernetesLeaderElection Sut { get; }
    internal DateTimeOffset Origin { get; }
    internal Func<CancellationToken, Task<V1Lease>>? Read;
    internal Func<V1Lease, CancellationToken, Task<V1Lease>>? Replace;
    internal readonly ConcurrentQueue<long> ReadStarts = new();
    internal readonly TaskCompletionSource AcquiredSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource LostSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Acquired; internal int Lost; internal int Calls; internal int OwnershipWrites; internal int Creates;

    internal KubeTenureFixture()
    {
        Origin = Clock.GetUtcNow();
        A.CallTo(() => Client.CoordinationV1).Returns(Api);
        A.CallTo(() => Api.ReadNamespacedLeaseWithHttpMessagesAsync(A<string>._, A<string>._, A<bool?>._,
            A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                Interlocked.Increment(ref Calls); ReadStarts.Enqueue(Clock.GetTimestamp());
                var token = (CancellationToken)call.Arguments[4]!;
                var value = Read is { } read ? await read(token) : Snapshot();
                return new HttpOperationResponse<V1Lease> { Body = value };
            });
        A.CallTo(() => Api.ReplaceNamespacedLeaseWithHttpMessagesAsync(A<V1Lease>._, A<string>._, A<string>._,
            A<string>._, A<string>._, A<string>._, A<bool?>._,
            A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
            .ReturnsLazily(async call =>
            {
                Interlocked.Increment(ref Calls);
                var lease = KubernetesLeaseProtocol.Clone((V1Lease)call.Arguments[0]!);
                if (lease.Spec.HolderIdentity is not null) { Interlocked.Increment(ref OwnershipWrites); }
                var token = (CancellationToken)call.Arguments[8]!;
                var value = Replace is { } replace ? await replace(lease, token) : Persist(lease);
                return new HttpOperationResponse<V1Lease> { Body = value };
            });
        A.CallTo(() => Api.CreateNamespacedLeaseWithHttpMessagesAsync(A<V1Lease>._, A<string>._, A<string>._,
            A<string>._, A<string>._, A<bool?>._, A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
            .ReturnsLazily(() => { Interlocked.Increment(ref Calls); Interlocked.Increment(ref Creates); return Task.FromException<HttpOperationResponse<V1Lease>>(Status(HttpStatusCode.Conflict)); });
        Sut = CreateProvider();
        Sut.BecameLeader += (_, _) => { Interlocked.Increment(ref Acquired); AcquiredSignal.TrySetResult(); };
        Sut.LostLeadership += (_, _) => { Interlocked.Increment(ref Lost); LostSignal.TrySetResult(); };
    }

    internal KubernetesLeaderElection CreateProvider(int maxRetries = 0, TimeSpan? maxRetryDelay = null) => new(Client, "orders", Options.Create(new KubernetesLeaderElectionOptions
    { Namespace = "test", LeaseName = "orders", InstanceId = "candidate", LeaseDuration = TimeSpan.FromSeconds(15),
        RenewInterval = TimeSpan.FromSeconds(5), GracePeriod = TimeSpan.FromSeconds(5), RetryInterval = TimeSpan.FromSeconds(2), MaxRetries = maxRetries, MaxRetryDelay = maxRetryDelay ?? TimeSpan.Zero }), null, Clock);

    internal async Task StartAsync()
    {
        await Sut.StartAsync(CancellationToken.None);
        await AcquiredSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    internal V1Lease Snapshot() { lock (_serverGate) { return KubernetesLeaseProtocol.Clone(_record); } }
    internal V1Lease Persist(V1Lease lease)
    {
        lock (_serverGate)
        {
            if (lease.Metadata.Uid != _record.Metadata.Uid || lease.Metadata.ResourceVersion != _record.Metadata.ResourceVersion)
            { throw Status(HttpStatusCode.Conflict); }
            _record = KubernetesLeaseProtocol.Clone(lease);
            _record.Metadata.ResourceVersion = Interlocked.Increment(ref _version).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return KubernetesLeaseProtocol.Clone(_record);
        }
    }
    internal void SetUnheldTransitions(int transitions) { lock (_serverGate) { _record.Spec.LeaseTransitions = transitions; } }
    internal void RecreateWithForeignOwner()
    {
        lock (_serverGate)
        {
            _record = new V1Lease
            {
                Metadata = new V1ObjectMeta { Name = "orders", NamespaceProperty = "test", Uid = "replacement-uid", ResourceVersion = "1",
                    Annotations = new Dictionary<string, string>(StringComparer.Ordinal) { [KubernetesLeaseProtocol.OwnerAnnotation] = "foreign-owner" } },
                Spec = new V1LeaseSpec { HolderIdentity = "candidate", LeaseTransitions = 1, LeaseDurationSeconds = 15, RenewTime = Clock.GetUtcNow().UtcDateTime }
            };
        }
    }
    internal static HttpOperationException Status(HttpStatusCode status) => new("native status")
    { Response = new HttpResponseMessageWrapper(new HttpResponseMessage(status), string.Empty) };
    public async ValueTask DisposeAsync()
    {
        Clock.ReleaseBarrier();
        var failures = new List<Exception>();
        try
        {
            try { await Sut.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
            try { await Sut.NotificationsCompleted; } catch (Exception error) { failures.Add(error); }
        }
        finally { try { Clock.Dispose(); } finally { Client.Dispose(); } }
        KubernetesElectionFailures.Throw(failures, "Kubernetes fixture teardown failed.");
    }
}

internal sealed partial class KubeTenureClock : FakeTimeProvider, IDisposable
{
    private readonly long _origin;
    private readonly Lock _gate = new();
    private readonly Dictionary<long, int> _dueCounts = [];
    private readonly List<(long Due, int Occurrence, TaskCompletionSource Signal)> _waiters = [];
    private TimerCallback? _deadlineCallback; private object? _deadlineState;
    private long _utcOffset;
    private int _barrierCountdown;
    private volatile bool _actorOnlyBarrier;
    private readonly ManualResetEventSlim _release = new(true);
    internal TaskCompletionSource BarrierEntered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal KubeTenureClock() : base(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) => _origin = base.GetUtcNow().UtcTicks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp()
    {
        if (ShouldEnterTimestampBarrier())
        {
            BarrierEntered.TrySetResult();
#pragma warning disable RS0030 // Actual actor request-start capture barrier, never a fake native result.
            _release.Wait();
#pragma warning restore RS0030
        }
        return base.GetUtcNow().UtcTicks - _origin;
    }
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _utcOffset));
    internal TimeSpan UtcOffset { set => Interlocked.Exchange(ref _utcOffset, value.Ticks); }
    // Exclude public-wait reads from the old actor launch-gap oracle. Missing runtime actor
    // frames make the positive milestone time out red, rather than silently measuring another site.
    private static bool IsActorTimestamp() => new System.Diagnostics.StackTrace().GetFrames().Any(frame =>
        frame.GetMethod()?.DeclaringType?.Name.StartsWith("<RunActorAsync>", StringComparison.Ordinal) == true);
    internal void ArmTimestampBarrier(int ordinal, bool actorOnly = false)
    { _release.Reset(); BarrierEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); _readyLaunchProbe = null; _actorOnlyBarrier = actorOnly; Volatile.Write(ref _barrierCountdown, ordinal); }
    internal void ReleaseBarrier() => _release.Set();
    public void Dispose() { _release.Set(); CancelDiagnosticWaiters(); _release.Dispose(); }
    internal void FireDeadlineEarly() => (_deadlineCallback ?? throw new InvalidOperationException("Actual deadline timer was not captured."))(_deadlineState);
    internal Task WaitForDueAsync(TimeSpan due, int occurrence)
    {
        lock (_gate)
        {
            if (_dueCounts.GetValueOrDefault(due.Ticks) >= occurrence) { return Task.CompletedTask; }
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((due.Ticks, occurrence, signal)); return signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
    private void Record(TimeSpan due)
    {
        lock (_gate)
        {
            var count = _dueCounts.GetValueOrDefault(due.Ticks) + 1; _dueCounts[due.Ticks] = count;
            foreach (var waiter in _waiters.Where(item => item.Due == due.Ticks && item.Occurrence <= count)) { waiter.Signal.TrySetResult(); }
        }
    }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (state is RenewalTenureDeadline && _deadlineCallback is null) { _deadlineCallback = callback; _deadlineState = state; }
        var observation = BeginTimerObservation(callback, state, dueTime, period);
        var timer = base.CreateTimer(callback, state, dueTime, period);
        Record(dueTime); CompleteTimerObservation(observation, true);
        return new ObservedTimer(timer, this, observation.Identity);
    }
    private sealed class ObservedTimer(ITimer inner, KubeTenureClock owner, KubeTimerIdentity identity) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            var observation = owner.BeginTimerChange(identity, dueTime, period);
            var result = inner.Change(dueTime, period);
            owner.Record(dueTime); owner.CompleteTimerObservation(observation, result); return result;
        }
        public void Dispose() => inner.Dispose();
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
