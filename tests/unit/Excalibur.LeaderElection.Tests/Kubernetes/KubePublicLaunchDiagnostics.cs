// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;
using System.Reflection;
using Excalibur.LeaderElection.Kubernetes;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

internal sealed record KubeReadyLatchSnapshot(string Milestone, long Kernel, long Operation, long Acquire, long Generation, string Phase,
    int State, bool Completed, bool Stopped, bool Published, long? ProofStart, bool KernelGateHeldAtSelector, bool RunGateHeldAtSelector);
internal sealed record KubeLaunchSdkObservation(string Operation, long At, bool Active, bool OwnerMatches,
    bool HasAdmission, bool TokenCanceledAtEntry, bool ParentCanceledAtEntry, string? Holder)
{
    internal CancellationToken Token { get; init; }
}

internal sealed partial class KubeTenureClock
{
    private KubePublicLaunchProbe? _readyLaunchProbe;
    internal void ArmReadyLaunchBarrier(KubePublicLaunchProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _release.Reset(); BarrierEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _readyLaunchProbe = probe; _actorOnlyBarrier = true; Volatile.Write(ref _barrierCountdown, 1);
    }
    private bool ShouldEnterTimestampBarrier()
    {
        if (Volatile.Read(ref _barrierCountdown) <= 0) { return false; }
        if (_readyLaunchProbe is { } probe)
        {
            // Actor frames select candidate reads; ACTUAL state/gate ownership proves the window.
            if (!IsActorTimestamp()) { return false; }
            try { if (!probe.TrySelectReadyOutsideGates()) { return false; } }
            catch (Exception error) { probe.RecordSelectorFailure(error); BarrierEntered.TrySetException(error); throw; }
            return Interlocked.CompareExchange(ref _barrierCountdown, 0, 1) == 1;
        }
        return (!_actorOnlyBarrier || IsActorTimestamp()) && Interlocked.Decrement(ref _barrierCountdown) == 0;
    }
}

internal sealed class KubePublicLaunchProbe(KubernetesLeaderElection owner, KubeTenureClock clock, bool renewal, Action<string> output)
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private readonly Lock _diagnosticGate = new();
    private readonly ConcurrentQueue<string> _rejections = new();
    private readonly ConcurrentQueue<KubeLaunchSdkObservation> _sdk = new();
    private readonly Dictionary<object, long> _identities = new(ReferenceEqualityComparer.Instance);
    private readonly List<KubeReadyLatchSnapshot> _snapshots = [];
    private Raw? _baseline; private Raw? _selected; private long _identity;

    private sealed record Raw(object Kernel, OwnedOperationLatch Operation, OwnedOperationLatch Acquire, long Generation, string Phase,
        int State, bool Completed, bool Stopped, RenewalTenureDeadline.PublishedTenure? Published,
        bool KernelGateHeldAtSelector, bool RunGateHeldAtSelector)
    {
        internal CancellationToken Parent { get; init; }
    }

    internal void ObserveSdk(string operation, string? holder, CancellationToken token)
    {
        var ambient = typeof(KubernetesNativeOperation).GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetValue(null)
            ?? throw new InvalidOperationException("Actual native scope metadata missing.");
        var scope = ambient.GetType().GetProperty("Value")?.GetValue(ambient) ?? throw new InvalidOperationException("No actual native scope at SDK callback.");
        var admission = RequiredPropertyInfo(scope, "Admission").GetValue(scope);
        var sameOwner = ReferenceEquals(RequiredProperty(scope, "Owner"), owner);
        var active = (bool)RequiredField(scope, "IsActive");
        CancellationToken parent;
        lock (_diagnosticGate) { parent = (_selected ?? _baseline)?.Parent ?? default; }
        _sdk.Enqueue(new(operation, clock.DiagnosticNow, active, sameOwner, admission is not null, token.IsCancellationRequested, parent.IsCancellationRequested, holder) { Token = token });
    }
    internal int SdkCheckpoint => _sdk.Count;

    internal KubeReadyLatchSnapshot CaptureRenewalBaseline()
    {
        if (!renewal) { throw new InvalidOperationException("Initial probe has no pre-acquisition baseline."); }
        var raw = ReadCurrent(false, false) ?? throw new InvalidOperationException("Actual renewal baseline missing.");
        if (raw.Published is null || raw.Published.Generation != raw.Generation || !ReferenceEquals(raw.Operation, raw.Acquire)
            || !raw.Completed || raw.State != 1 || raw.Stopped || raw.Phase != "Holding")
        { throw new InvalidOperationException("Actual baseline is not the completed acquisition latch and published tenure."); }
        lock (_diagnosticGate) { _baseline = raw; }
        return Save(raw, "renewal-baseline");
    }

    internal bool TrySelectReadyOutsideGates()
    {
        // These unsynchronized reference samples establish NO state assertion. They find the actual
        // gate only so a descendant GetTimestamp already holding it can be rejected BEFORE runGate.
        var slot = OptionalField(owner, "_slot");
        if (slot is null) { _rejections.Enqueue("no installed slot reference"); return false; }
        var run = RequiredPropertyInfo(slot, "Run").GetValue(slot);
        if (run is null) { _rejections.Enqueue("no installed run reference"); return false; }
        var kernel = RequiredProperty(run, "Kernel");
        var runGate = RequiredField(owner, "_runGate") as Lock ?? throw new InvalidOperationException("Actual run gate type changed.");
        var kernelGate = RequiredField(kernel, "_gate") as Lock ?? throw new InvalidOperationException("Actual kernel gate type changed.");
        var kernelHeld = kernelGate.IsHeldByCurrentThread; var runHeld = runGate.IsHeldByCurrentThread;
        if (kernelHeld || runHeld) { _rejections.Enqueue($"candidate rejected: actual gate held kernel={kernelHeld}, run={runHeld}"); return false; }
        var raw = ReadCurrent(kernelHeld, runHeld, slot, run, kernel);
        if (raw is null || raw.Stopped || raw.State != 0 || raw.Completed || raw.Generation <= 0) { _rejections.Enqueue("not an actual registered Ready operation"); return false; }
        Raw? baseline;
        lock (_diagnosticGate) { baseline = _baseline; }
        if (renewal)
        {
            if (baseline is null || !ReferenceEquals(raw.Kernel, baseline.Kernel) || raw.Generation != baseline.Generation
                || !ReferenceEquals(raw.Acquire, baseline.Acquire) || ReferenceEquals(raw.Operation, raw.Acquire)
                || raw.Published is null || raw.Published.Generation != raw.Generation || raw.Phase != "Holding")
            { _rejections.Enqueue("not the same generation's distinct registered renewal latch"); return false; }
        }
        else if (!ReferenceEquals(raw.Operation, raw.Acquire) || raw.Published is not null || raw.Phase != "Acquiring")
        { _rejections.Enqueue("not the actual admitted initial acquisition latch"); return false; }
        lock (_diagnosticGate) { if (_selected is not null) { return false; } _selected = raw; }
        Save(raw, "selected-Ready-outside-gates"); return true;
    }

    internal KubeReadyLatchSnapshot RequireSelected()
    {
        Raw raw; lock (_diagnosticGate) { raw = _selected ?? throw new InvalidOperationException("No positively witnessed real Ready latch."); }
        return Save(raw, "held-before-Stop");
    }
    internal KubeReadyLatchSnapshot RequireSameLatchSealedBySynchronousStop()
    {
        Raw selected; lock (_diagnosticGate) { selected = _selected ?? throw new InvalidOperationException("No selected real operation."); }
        var raw = ReadCurrent(false, false) ?? throw new InvalidOperationException("Actual stopped run/kernel missing.");
        if (!ReferenceEquals(raw.Kernel, selected.Kernel) || !ReferenceEquals(raw.Operation, selected.Operation)
            || !ReferenceEquals(raw.Acquire, selected.Acquire) || raw.Generation != selected.Generation
            || !raw.Stopped || raw.State != 2 || !raw.Completed || raw.Published is not null)
        { throw new InvalidOperationException("Synchronous public Stop did not seal THAT SAME unlaunched latch/remove authority."); }
        return Save(raw, "same-latch-SealedUnlaunched-before-barrier-release");
    }

    internal void RequireNoSdkAfter(int checkpoint)
    {
        if (_sdk.Count != checkpoint) { throw new InvalidOperationException("Ownership SDK work started while the real Ready launch barrier was held."); }
    }
    internal void RequireOnlyAuthorizedCleanupAfter(int checkpoint)
    {
        Raw selected; lock (_diagnosticGate) { selected = _selected ?? throw new InvalidOperationException("No selected operation provenance."); }
        var all = _sdk.ToArray(); var after = all.Skip(checkpoint).ToArray();
        var initialToken = all.FirstOrDefault()?.Token ?? default;
        if (after.Any(value => !value.Active || !value.OwnerMatches || value.HasAdmission || !value.ParentCanceledAtEntry
                || !value.Token.CanBeCanceled || value.TokenCanceledAtEntry || value.Token == initialToken
                || (value.Operation == "Replace" && value.Holder is not null))
            || after.Count(value => value.Operation == "Read") > 1 || after.Count(value => value.Operation == "Replace") > 1
            || after.Any(value => value.Operation is not ("Read" or "Replace"))
            || (after.Any(value => value.Operation == "Replace") && after[0].Operation != "Read"))
        { throw new InvalidOperationException("A post-Stop SDK action is ownership work or not the pinned fresh-token read/clear cleanup."); }
        if (after.Length > 0 && !selected.Parent.IsCancellationRequested) { throw new InvalidOperationException("Actual acquisition token is not cancelled before cleanup."); }
    }
    internal void RecordSelectorFailure(Exception error) => _rejections.Enqueue("SELECTOR_FAILURE " + error);
    internal void Dump()
    {
        KubeReadyLatchSnapshot[] copy; lock (_diagnosticGate) { copy = _snapshots.ToArray(); }
        foreach (var value in copy) { output("ACTUAL_LATCH " + value); }
        foreach (var reason in _rejections) { output("SELECTOR " + reason); }
        foreach (var value in _sdk) { output("ACTUAL_SDK " + value); }
    }

    private Raw? ReadCurrent(bool kernelHeld, bool runHeld, object? expectedSlot = null, object? expectedRun = null, object? expectedKernel = null)
    {
        var gate = RequiredField(owner, "_runGate") as Lock ?? throw new InvalidOperationException("Actual run gate type changed.");
        lock (gate)
        {
            var slot = OptionalField(owner, "_slot"); if (slot is null) { return null; }
            var run = RequiredPropertyInfo(slot, "Run").GetValue(slot); if (run is null) { return null; }
            var kernel = RequiredProperty(run, "Kernel");
            if ((expectedSlot is not null && !ReferenceEquals(slot, expectedSlot)) || (expectedRun is not null && !ReferenceEquals(run, expectedRun))
                || (expectedKernel is not null && !ReferenceEquals(kernel, expectedKernel))) { return null; }
            var kernelGate = RequiredField(kernel, "_gate") as Lock ?? throw new InvalidOperationException("Actual kernel gate type changed.");
            lock (kernelGate)
            {
                var operationValue = OptionalField(kernel, "_operation");
                if (operationValue is not null && operationValue is not OwnedOperationLatch) { throw new InvalidOperationException("Actual registered operation type changed."); }
                var operation = operationValue as OwnedOperationLatch;
                var acquisition = OptionalField(kernel, "_acquisitionOwner"); if (operation is null || acquisition is null) { return null; }
                var acquire = RequiredProperty(acquisition, "Acquire") as OwnedOperationLatch ?? throw new InvalidOperationException("Actual acquisition latch type changed.");
                var publication = OptionalField(kernel, "_current");
                if (publication is not null && publication is not RenewalTenureDeadline.PublishedTenure) { throw new InvalidOperationException("Actual publication field type changed."); }
                var published = publication as RenewalTenureDeadline.PublishedTenure;
                return new(kernel, operation, acquire, (long)RequiredField(kernel, "_generation"), RequiredField(kernel, "_phase").ToString() ?? "<null>",
                    (int)RequiredField(operation, "_state"), operation.Completion.IsCompleted, (bool)RequiredField(kernel, "_stopped"), published,
                    kernelHeld, runHeld) { Parent = (CancellationToken)RequiredProperty(acquisition, "Token") };
            }
        }
    }
    private KubeReadyLatchSnapshot Save(Raw raw, string milestone)
    {
        KubeReadyLatchSnapshot value;
        lock (_diagnosticGate)
        {
            long Identity(object target)
            {
                if (_identities.TryGetValue(target, out var identity)) { return identity; }
                identity = ++_identity; _identities.Add(target, identity); return identity;
            }
            value = new(milestone, Identity(raw.Kernel), Identity(raw.Operation), Identity(raw.Acquire), raw.Generation, raw.Phase, raw.State, raw.Completed,
                raw.Stopped, raw.Published is not null, raw.Published?.ProofRequestStart, raw.KernelGateHeldAtSelector, raw.RunGateHeldAtSelector);
            _snapshots.Add(value);
        }
        return value;
    }
    private static FieldInfo RequiredFieldInfo(object target, string name) => target.GetType().GetField(name, Members)
        ?? throw new InvalidOperationException("Actual diagnostic field metadata missing: " + name);
    private static object? OptionalField(object target, string name) => RequiredFieldInfo(target, name).GetValue(target);
    private static object RequiredField(object target, string name) => OptionalField(target, name)
        ?? throw new InvalidOperationException("Actual diagnostic field value null: " + name);
    private static PropertyInfo RequiredPropertyInfo(object target, string name) => target.GetType().GetProperty(name, Members)
        ?? throw new InvalidOperationException("Actual diagnostic property metadata missing: " + name);
    private static object RequiredProperty(object target, string name) => RequiredPropertyInfo(target, name).GetValue(target)
        ?? throw new InvalidOperationException("Actual diagnostic property value null: " + name);
}
