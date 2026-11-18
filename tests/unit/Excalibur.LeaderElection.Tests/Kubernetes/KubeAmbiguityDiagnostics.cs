// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using Excalibur.LeaderElection.Kubernetes;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

internal sealed record KubeTimerSignature(string CallbackDeclaringType, string CallbackMethod, string CallbackTargetType, string StateType)
{
    internal static KubeTimerSignature Read(TimerCallback callback, object? state) => new(
        callback.Method.DeclaringType?.AssemblyQualifiedName ?? "<null>", $"{callback.Method.Module.ModuleVersionId:N}:{callback.Method.MetadataToken}:{callback.Method}",
        callback.Target?.GetType().AssemblyQualifiedName ?? "<null>", state?.GetType().AssemblyQualifiedName ?? "<null>");
}
internal sealed record KubeTimerIdentity(long Timer, long Callback, long State, KubeTimerSignature Signature);
internal sealed record KubeTimerTrace(long Sequence, KubeTimerIdentity Identity, string Operation, long Now, TimeSpan Due, TimeSpan Period, bool Succeeded);
internal sealed record KubeTimerObservation(long Sequence, KubeTimerIdentity Identity, string Operation, long Now, TimeSpan Due, TimeSpan Period);
internal sealed record KubeTimerPurposes(KubeTimerSignature Delay, KubeTimerSignature CallerWait);
internal sealed record KubeProofSnapshot(string Milestone, long ObservedNow, long KernelIdentity, long PublicationIdentity, long OwnershipIdentity, long Generation, long ProofRequestStart, long? FencingToken);

internal sealed partial class KubeTenureClock
{
    private readonly Lock _traceGate = new();
    private readonly List<KubeTimerTrace> _timerTrace = [];
    private readonly Dictionary<object, long> _timerObjectIdentities = new(ReferenceEqualityComparer.Instance);
    private readonly List<(KubeTimerSignature Signature, TimeSpan Due, long After, TaskCompletionSource<KubeTimerTrace> Signal)> _sourceWaiters = [];
    private long _traceSequence; private long _timerIdentity; private long _objectIdentity;
    internal long DiagnosticNow => base.GetUtcNow().UtcTicks - _origin; // Never calls the barrier-bearing GetTimestamp override.
    internal long TimerCheckpoint { get { lock (_traceGate) { return _traceSequence; } } }
    internal KubeTimerTrace[] SnapshotTimers() { lock (_traceGate) { return _timerTrace.OrderBy(value => value.Sequence).ToArray(); } }
    internal int LegacyDueCount(TimeSpan due) { lock (_gate) { return _dueCounts.GetValueOrDefault(due.Ticks); } }

    private KubeTimerObservation BeginTimerObservation(TimerCallback callback, object? state, TimeSpan due, TimeSpan period)
    {
        var now = DiagnosticNow; var signature = KubeTimerSignature.Read(callback, state);
        lock (_traceGate)
        {
            long Identity(object? value)
            {
                if (value is null) { return 0; }
                if (_timerObjectIdentities.TryGetValue(value, out var identity)) { return identity; }
                identity = ++_objectIdentity; _timerObjectIdentities.Add(value, identity); return identity;
            }
            var timer = new KubeTimerIdentity(++_timerIdentity, Identity(callback), Identity(state), signature);
            return new(++_traceSequence, timer, "Create", now, due, period);
        }
    }
    private KubeTimerObservation BeginTimerChange(KubeTimerIdentity identity, TimeSpan due, TimeSpan period)
    {
        var now = DiagnosticNow;
        lock (_traceGate) { return new(++_traceSequence, identity, "Change", now, due, period); }
    }
    private void CompleteTimerObservation(KubeTimerObservation observation, bool result)
    {
        var record = new KubeTimerTrace(observation.Sequence, observation.Identity, observation.Operation, observation.Now, observation.Due, observation.Period, result);
        lock (_traceGate)
        {
            _timerTrace.Add(record);
            foreach (var waiter in _sourceWaiters.Where(value => Matches(record, value.Signature, value.Due, value.After))) { waiter.Signal.TrySetResult(record); }
        }
    }
    private void CancelDiagnosticWaiters()
    {
        lock (_traceGate) { foreach (var waiter in _sourceWaiters) { waiter.Signal.TrySetCanceled(); } }
    }
    internal Task<KubeTimerTrace> WaitForSourceDueAsync(KubeTimerSignature source, TimeSpan due, long after)
    {
        lock (_traceGate)
        {
            var seen = _timerTrace.Where(value => Matches(value, source, due, after)).ToArray();
            if (seen.Length > 1) { throw new InvalidOperationException("Ambiguous source-specific timer milestone."); }
            if (seen.Length == 1) { return Task.FromResult(seen[0]); }
            var signal = new TaskCompletionSource<KubeTimerTrace>(TaskCreationOptions.RunContinuationsAsynchronously);
            _sourceWaiters.Add((source, due, after, signal)); return signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
    internal void AssertUniqueSourceMilestone(KubeTimerTrace selected, KubeTimerSignature source, TimeSpan due, long after)
    {
        lock (_traceGate)
        {
            var seen = _timerTrace.Where(value => Matches(value, source, due, after)).ToArray();
            if (seen.Length != 1 || seen[0] != selected) { throw new InvalidOperationException("Missing or ambiguous actual source timer after milestone."); }
        }
    }
    private static bool Matches(KubeTimerTrace value, KubeTimerSignature source, TimeSpan due, long after) =>
        value.Sequence > after && value.Succeeded && value.Identity.Signature == source && value.Due == due;
}

internal sealed class KubeAmbiguityDiagnostics(Action<string> output)
{
    private readonly Dictionary<object, long> _identities = new(ReferenceEqualityComparer.Instance);
    private readonly List<KubeProofSnapshot> _proofs = [];
    private long _nextIdentity;
    private KubeTimerPurposes? _purposes;
    private bool? _legacySatisfied;
    private bool? _waitDueFiveSeen;

    internal async Task<KubeTimerPurposes> CalibrateAsync()
    {
        var delayClock = new KubeTenureClock(); var waitClock = new KubeTenureClock();
        using var delayCancellation = new CancellationTokenSource(); using var waitCancellation = new CancellationTokenSource();
        var actual = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task delay = Task.CompletedTask; Task wait = Task.CompletedTask;
        try
        {
            delay = Task.Delay(TimeSpan.FromSeconds(5), delayClock, delayCancellation.Token); // delay-ok: This delay uses the fake clock only to calibrate timer identity; it is canceled and joined without a wall-clock wait.
            wait = KubernetesCallerWait.WaitAsync(actual.Task, waitClock.GetTimestamp(), TimeSpan.FromSeconds(5), waitClock, waitCancellation.Token);
            var delaySource = delayClock.SnapshotTimers().Single(value => value.Operation == "Create");
            var waitSource = waitClock.SnapshotTimers().Single(value => value.Operation == "Create");
            if (delaySource.Due != TimeSpan.FromSeconds(5) || waitSource.Due != delaySource.Due || delaySource.Identity.Signature == waitSource.Identity.Signature)
            {
                throw new InvalidOperationException("Actual same-due Task.Delay and CallerWait timer signatures collide or are unproven.");
            }
            return _purposes = new(delaySource.Identity.Signature, waitSource.Identity.Signature);
        }
        finally
        {
            actual.TrySetResult(); await delayCancellation.CancelAsync(); await waitCancellation.CancelAsync();
            try { await delay; } catch (OperationCanceledException) when (delay.IsCanceled) { }
            try { await wait; } catch (OperationCanceledException) when (wait.IsCanceled) { }
            foreach (var trace in delayClock.SnapshotTimers()) { output("CALIBRATION_DELAY " + trace); }
            foreach (var trace in waitClock.SnapshotTimers()) { output("CALIBRATION_CALLER_WAIT " + trace); }
            delayClock.Dispose(); waitClock.Dispose();
        }
    }

    internal KubeProofSnapshot CaptureProof(KubernetesLeaderElection provider, KubeTenureClock clock, string milestone)
    {
        var runGate = Field<Lock>(provider, "_runGate");
        RenewalTenureDeadline kernel; RenewalTenureDeadline.PublishedTenure published;
        lock (runGate) // Typed System.Threading.Lock: NOT an object/Monitor lock on the same instance.
        {
            var slot = Field<object>(provider, "_slot");
            var run = Property(slot, "Run");
            kernel = Property(run, "Kernel") as RenewalTenureDeadline ?? throw new InvalidOperationException("Actual run kernel type changed.");
            var kernelGate = Field<Lock>(kernel, "_gate");
            lock (kernelGate)
            {
                // Capture ONE reference to the actual immutable record. Never call Current/ReadStatus.
                published = Field<RenewalTenureDeadline.PublishedTenure>(kernel, "_current");
            }
        }
        var snapshot = new KubeProofSnapshot(milestone, clock.DiagnosticNow, Identity(kernel), Identity(published), Identity(published.OwnershipProof),
            published.Generation, published.ProofRequestStart, published.FencingToken);
        _proofs.Add(snapshot); output("PROOF " + snapshot); return snapshot;
    }
    internal void ObserveLegacyGate(KubeTenureClock clock, KubeTimerPurposes purposes)
    {
        _legacySatisfied = clock.LegacyDueCount(TimeSpan.FromSeconds(5)) >= 2;
        _waitDueFiveSeen = clock.SnapshotTimers().Any(value => value.Due == TimeSpan.FromSeconds(5) && value.Identity.Signature == purposes.CallerWait);
        output($"LEGACY_GATE now={clock.DiagnosticNow} dueFiveOccurrence2={_legacySatisfied} calibratedCallerWaitDueFive={_waitDueFiveSeen}");
    }
    internal void Dump(KubeTenureFixture fixture)
    {
        output($"Runtime={Environment.Version}; Purposes={_purposes}; LegacyGate={_legacySatisfied}; WaitDueFive={_waitDueFiveSeen}");
        foreach (var snapshot in _proofs) { output("PROOF_RETAINED " + snapshot); }
        foreach (var trace in fixture.Clock.SnapshotTimers()) { output("TIMER " + trace); }
        output("READ_STARTS " + string.Join(",", fixture.ReadStarts));
    }
    private long Identity(object value)
    {
        if (_identities.TryGetValue(value, out var identity)) { return identity; }
        identity = ++_nextIdentity; _identities.Add(value, identity); return identity;
    }
    private static T Field<T>(object target, string name) where T : class
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            ?? throw new InvalidOperationException("Actual diagnostic field metadata missing: " + name);
        var value = field.GetValue(target) ?? throw new InvalidOperationException("Actual diagnostic field value is null: " + name);
        return value as T ?? throw new InvalidOperationException("Actual diagnostic field type changed: " + name);
    }
    private static object Property(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetValue(target)
        ?? throw new InvalidOperationException("Actual diagnostic property missing/null: " + name);
}
