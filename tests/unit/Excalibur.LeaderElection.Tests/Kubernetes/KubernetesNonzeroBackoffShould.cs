// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using System.Collections.Concurrent;
using System.Reflection;
using k8s.Models;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesNonzeroBackoffShould(ITestOutputHelper output)
{
    [Fact]
    public async Task BoundOnePublicStartAcrossActualOneTwoFourSecondRetryDelaysAndAHeldFourthRead()
    {
        var fixture = new KubeTenureFixture();
        var diagnostics = new KubeAmbiguityDiagnostics(message => output.WriteLine(message));
        var held = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fourthEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        KubernetesLeaderElection? provider = null; Task? starting = null; Task? disposing = null;
        TimeoutException? observedTimeout = null; Exception? primary = null; var acquired = 0; var attempts = 0;
        var released = 0; var cleanupCount = 0; NativeReadContext? heldContext = null;
        var cleanupReads = new ConcurrentQueue<(long At, NativeReadContext Context)>();
        void Failure(string key, Exception error)
        {
            if (primary is null) { primary = error; } else { primary.Data[key] = error; }
        }
        try
        {
            var purposes = await diagnostics.CalibrateAsync();
            // Leave one retry available: post-deadline safety must not be credited to retry exhaustion.
            var actualProvider = fixture.CreateProvider(maxRetries: 4, maxRetryDelay: TimeSpan.FromSeconds(4)); provider = actualProvider;
            provider.BecameLeader += (_, _) => Interlocked.Increment(ref acquired);
            fixture.Read = token =>
            {
                if (Volatile.Read(ref released) != 0)
                {
                    var observed = CaptureNativeReadContext(actualProvider, token);
                    var original = heldContext ?? throw new InvalidOperationException("Cleanup has no actual held-operation provenance.");
                    string? rejectedFact = null;
                    bool Reject(bool invalid, string fact)
                    {
                        if (invalid) { rejectedFact = fact; }
                        return invalid;
                    }

                    if (Reject(!observed.Active, "ActiveScope")
                        || Reject(observed.HasAdmission, "NoOwnershipAdmission")
                        || Reject(!observed.Retiring, "RetiringAttempt")
                        || Reject(!observed.AcquisitionToken.IsCancellationRequested, "AcquisitionCancelled")
                        || Reject(!original.SdkToken.IsCancellationRequested, "OriginalSdkCancelled")
                        || Reject(observed.SdkToken == original.SdkToken, "DistinctSdkToken")
                        || Reject(!observed.SdkToken.CanBeCanceled, "CleanupTokenCancellable")
                        || Reject(observed.SdkToken.IsCancellationRequested, "CleanupTokenNotCancelled")
                        || Reject(observed.AcquisitionToken != original.AcquisitionToken, "SameAcquisitionToken")
                        || Reject(observed.Generation != original.Generation, "SameGeneration")
                        || Reject(!ReferenceEquals(observed.Kernel, original.Kernel), "SameKernel")
                        || Reject(!ReferenceEquals(observed.Attempt, original.Attempt), "SameAttempt")
                        || Reject(fixture.Clock.DiagnosticNow != TimeSpan.FromSeconds(30).Ticks, "CleanupAtThirty")
                        || Reject(Interlocked.Increment(ref cleanupCount) != 1, "ExactlyOneCleanupRead"))
                    {
                        throw new InvalidOperationException("A post-release read is not the one separately authorized actual retirement cleanup. FirstRejectedFact=" + rejectedFact);
                    }
                    var snapshot = fixture.Snapshot();
                    if (snapshot.Spec.HolderIdentity is not null) { throw new InvalidOperationException("Cleanup control must return the original unheld lease, never authorize a write."); }
                    cleanupReads.Enqueue((fixture.Clock.DiagnosticNow, observed)); return Task.FromResult(snapshot);
                }
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt <= 3) { return Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.ServiceUnavailable)); }
                if (attempt == 4)
                {
                    heldContext = CaptureNativeReadContext(actualProvider, token);
                    if (!heldContext.Active || !heldContext.HasAdmission || heldContext.Retiring || heldContext.AcquisitionToken.IsCancellationRequested)
                    { throw new InvalidOperationException("The fourth read is not the actual live acquiring operation."); }
                    fourthEntered.TrySetResult(); return held.Task;
                }
                return Task.FromException<V1Lease>(new InvalidOperationException("An additional SDK read was admitted before the finite held operation was released."));
            };
            // ONE actual public invocation and its original callerStart=0 throughout all retries.
            var actualStart = provider.StartAsync(CancellationToken.None); starting = actualStart;
            var first = await fixture.Clock.WaitForSourceDueAsync(purposes.Delay, TimeSpan.FromSeconds(1), 0);
            fixture.Clock.AssertUniqueSourceMilestone(first, purposes.Delay, TimeSpan.FromSeconds(1), 0);
            first.Now.ShouldBe(0); fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0 });
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(999));
            fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0 }); actualStart.IsCompleted.ShouldBeFalse();
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var second = await fixture.Clock.WaitForSourceDueAsync(purposes.Delay, TimeSpan.FromSeconds(2), first.Sequence);
            fixture.Clock.AssertUniqueSourceMilestone(second, purposes.Delay, TimeSpan.FromSeconds(2), first.Sequence);
            second.Now.ShouldBe(TimeSpan.FromSeconds(1).Ticks); fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0, TimeSpan.FromSeconds(1).Ticks });
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1999));
            fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0, TimeSpan.FromSeconds(1).Ticks }); actualStart.IsCompleted.ShouldBeFalse();
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var third = await fixture.Clock.WaitForSourceDueAsync(purposes.Delay, TimeSpan.FromSeconds(4), second.Sequence);
            fixture.Clock.AssertUniqueSourceMilestone(third, purposes.Delay, TimeSpan.FromSeconds(4), second.Sequence);
            third.Now.ShouldBe(TimeSpan.FromSeconds(3).Ticks); fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0, TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(3).Ticks });
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(3999));
            fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0, TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(3).Ticks }); actualStart.IsCompleted.ShouldBeFalse();
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
            await fourthEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.DiagnosticNow.ShouldBe(TimeSpan.FromSeconds(7).Ticks);
            var starts = new long[] { 0, TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(3).Ticks, TimeSpan.FromSeconds(7).Ticks };
            fixture.ReadStarts.ToArray().ShouldBe(starts); fixture.Calls.ShouldBe(4); attempts.ShouldBe(4); held.Task.IsCompleted.ShouldBeFalse();
            // Caller/actor WaitAsync can share a signature; do not claim a unique statement from it.
            // Positive original-deadline evidence is actual WaitAsync-shaped registration at t0/due10.
            fixture.Clock.SnapshotTimers().Any(value => value.Succeeded && value.Identity.Signature == purposes.CallerWait
                && value.Now == 0 && value.Due == TimeSpan.FromSeconds(10)).ShouldBeTrue();
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(2999));
            fixture.Clock.DiagnosticNow.ShouldBe(TimeSpan.FromMilliseconds(9999).Ticks);
            actualStart.IsCompleted.ShouldBeFalse(); held.Task.IsCompleted.ShouldBeFalse(); fixture.ReadStarts.ToArray().ShouldBe(starts);
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
            // The watchdog is OUTSIDE the expected operation exception assertion.
            await TerminalAsync(actualStart); actualStart.IsCompleted.ShouldBeTrue();
            observedTimeout = await Should.ThrowAsync<TimeoutException>(() => actualStart);
            fixture.Clock.DiagnosticNow.ShouldBe(TimeSpan.FromSeconds(10).Ticks);
            provider.IsLeader.ShouldBeFalse(); provider.CurrentLeadership.ShouldBeNull(); acquired.ShouldBe(0);
            fixture.OwnershipWrites.ShouldBe(0); fixture.Creates.ShouldBe(0); fixture.Calls.ShouldBe(4); fixture.ReadStarts.ToArray().ShouldBe(starts);
            held.Task.IsCompleted.ShouldBeFalse();
            disposing = provider.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
            fixture.Clock.Advance(TimeSpan.FromSeconds(20));
            fixture.ReadStarts.ToArray().ShouldBe(starts); fixture.Calls.ShouldBe(4); fixture.OwnershipWrites.ShouldBe(0); fixture.Creates.ShouldBe(0);
            held.Task.IsCompleted.ShouldBeFalse(); disposing.IsCompleted.ShouldBeFalse(); acquired.ShouldBe(0);
            Volatile.Write(ref released, 1); held.TrySetResult(fixture.Snapshot());
            await disposing; // Direct physical join only after the held actual SDK reply is released.
            attempts.ShouldBe(4); cleanupCount.ShouldBeGreaterThanOrEqualTo(0); cleanupCount.ShouldBeLessThanOrEqualTo(1);
            long[] expectedReads = cleanupCount == 0 ? starts : [.. starts, TimeSpan.FromSeconds(30).Ticks];
            fixture.ReadStarts.ToArray().ShouldBe(expectedReads); fixture.Calls.ShouldBe(4 + cleanupCount);
            fixture.OwnershipWrites.ShouldBe(0); fixture.Creates.ShouldBe(0); acquired.ShouldBe(0);
            fixture.Clock.Advance(TimeSpan.FromSeconds(20)); // Stability after the actual cleanup producer has joined.
            fixture.ReadStarts.ToArray().ShouldBe(expectedReads); fixture.Calls.ShouldBe(4 + cleanupCount);
            fixture.OwnershipWrites.ShouldBe(0); fixture.Creates.ShouldBe(0); acquired.ShouldBe(0);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            // Seal admission BEFORE releasing any pending SDK result on an early assertion/watchdog failure.
            if (provider is not null)
            {
                try { disposing ??= provider.DisposeAsync().AsTask(); } catch (Exception error) { Failure("SecondaryDisposeAdmissionFailure", error); }
            }
            fixture.Clock.ReleaseBarrier(); Volatile.Write(ref released, 1); held.TrySetResult(fixture.Snapshot());
            if (disposing is not null) { try { await disposing; } catch (Exception error) { Failure("SecondaryPhysicalDisposeFailure", error); } }
            if (starting is not null)
            {
                try { await starting; }
                catch (TimeoutException error) when (ReferenceEquals(error, observedTimeout)) { }
                catch (Exception error) { Failure("SecondaryActualStartFailure", error); }
            }
            foreach (var read in cleanupReads) { output.WriteLine($"AUTHORIZED_CLEANUP At={read.At}; Generation={read.Context.Generation}; Admission={read.Context.HasAdmission}; Retiring={read.Context.Retiring}; AcquisitionCancelled={read.Context.AcquisitionToken.IsCancellationRequested}; FreshSdkToken={read.Context.SdkToken != heldContext?.SdkToken}"); }
            try { output.WriteLine($"NONZERO_BACKOFF Start={starting?.Status}; Dispose={disposing?.Status}; Attempts={attempts}; Calls={fixture.Calls}; Acquired={acquired}; Writes={fixture.OwnershipWrites}; Creates={fixture.Creates}; ObservedTimeout={observedTimeout}"); diagnostics.Dump(fixture); }
            catch (Exception error) { Failure("SecondaryDiagnosticFailure", error); }
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); // Actual test provider is physically joined; unused fixture provider may now tear down.
            try { await fixture.DisposeAsync(); } catch (Exception error) { Failure("SecondaryFixtureTeardownFailure", error); }
        }
        if (primary is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private sealed record NativeReadContext(object Kernel, object Attempt, long Generation, bool Active, bool HasAdmission, bool Retiring)
    {
        internal CancellationToken AcquisitionToken { get; init; }
        internal CancellationToken SdkToken { get; init; }
    }

    private static NativeReadContext CaptureNativeReadContext(KubernetesLeaderElection owner, CancellationToken sdkToken)
    {
        static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetValue(target)
            ?? throw new InvalidOperationException("Actual native-read field missing/null: " + name);
        static object Property(object target, string name) => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetValue(target)
            ?? throw new InvalidOperationException("Actual native-read property missing/null: " + name);
        var ambient = typeof(KubernetesNativeOperation).GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetValue(null)
            ?? throw new InvalidOperationException("Actual SDK operation scope metadata missing.");
        var scope = ambient.GetType().GetProperty("Value")?.GetValue(ambient) ?? throw new InvalidOperationException("No actual active SDK operation scope.");
        if (!ReferenceEquals(Property(scope, "Owner"), owner)) { throw new InvalidOperationException("SDK operation belongs to another provider."); }
        var admissionProperty = scope.GetType().GetProperty("Admission", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) ?? throw new InvalidOperationException("Actual admission metadata missing.");
        var admission = admissionProperty.GetValue(scope); var active = (bool)Field(scope, "IsActive");
        var runGate = Field(owner, "_runGate") as Lock ?? throw new InvalidOperationException("Actual run gate type changed.");
        lock (runGate)
        {
            var run = Property(Field(owner, "_slot"), "Run"); var kernel = Property(run, "Kernel");
            var kernelGate = Field(kernel, "_gate") as Lock ?? throw new InvalidOperationException("Actual kernel gate type changed.");
            lock (kernelGate)
            {
                var attempt = Field(kernel, "_acquisitionOwner");
                return new NativeReadContext(kernel, attempt, (long)Field(kernel, "_generation"), active, admission is not null, (bool)Field(attempt, "Retiring"))
                {
                    AcquisitionToken = (CancellationToken)Property(attempt, "Token"),
                    SdkToken = sdkToken
                };
            }
        }
    }

    private static async Task TerminalAsync(Task operation)
    {
        var terminal = operation.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await terminal.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
