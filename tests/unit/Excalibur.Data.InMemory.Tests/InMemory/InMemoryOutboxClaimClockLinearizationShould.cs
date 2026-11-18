// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Pattern", "Regression")]
public sealed class InMemoryOutboxClaimClockLinearizationShould(ITestOutputHelper output)
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StampAContendedAcquisitionWithTheClockInsideItsCriticalSection()
    {
        var clock = new GatedClock();
        var store = new InMemoryOutboxStore(Options.Create(new InMemoryOutboxOptions
        {
            LeaseTimeoutSeconds = 2, FailureBackoffFloorSeconds = 1,
        }), NullLogger<InMemoryOutboxStore>.Instance, clock);
        var first = new OutboundMessage("first", [1], "destination") { Priority = 0 };
        var second = new OutboundMessage("second", [2], "destination") { Priority = 1 };
        await store.StageMessageAsync(first, CancellationToken.None);
        await store.StageMessageAsync(second, CancellationToken.None);
        var firstClaim = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        firstClaim.Id.ShouldBe(first.Id);

        var aResult = new TaskCompletionSource<Task<OutboxCompletionOutcome>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bResult = new TaskCompletionSource<Task<IEnumerable<OutboundMessage>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var aExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bEntered = 0;
        var aStarted = false;
        var bStarted = false;
        Exception? primary = null;
        Exception? cleanupError = null;
        var a = new Thread(() =>
        {
            try { aResult.TrySetResult(store.MarkSentAsync(first.Id, firstClaim.DispatcherId!, CancellationToken.None).AsTask()); }
            catch (Exception error) { aResult.TrySetException(error); }
            finally { aExited.TrySetResult(); }
        }) { IsBackground = true };
        var b = new Thread(() =>
        {
            // Before the synchronous store call: only a nonblocking publication, no signal/log callback.
            Volatile.Write(ref bEntered, 1);
            try { bResult.TrySetResult(store.GetUnsentMessagesAsync(1, 7, CancellationToken.None).AsTask()); }
            catch (Exception error) { bResult.TrySetException(error); }
            finally { bExited.TrySetResult(); }
        }) { IsBackground = true };

        try
        {
            clock.Arm();
            a.Start();
            aStarted = true;
            await clock.Entered.Task.WaitAsync(Deadline);
            b.Start();
            bStarted = true;

            // Narrow diagnostic by exclusion, not a general ThreadState synchronization guarantee:
            // A owns the store monitor; B has no blocking path except that monitor. Its clock reads
            // use only Interlocked. A cannot release the monitor before this test releases its gate.
            var observed = await Task.Run(() => SpinWait.SpinUntil(
                () => Volatile.Read(ref bEntered) == 1 &&
                    (b.ThreadState & ThreadState.WaitSleepJoin) != 0, Deadline));
            observed.ShouldBeTrue("the dedicated claimant must reach the contended monitor; timeout is a measurement failure");
            clock.Advance(TimeSpan.FromSeconds(3));
            clock.Release();

            (await aResult.Task.Unwrap().WaitAsync(Deadline)).ShouldBe(OutboxCompletionOutcome.Applied);
            var acquired = (await bResult.Task.Unwrap().WaitAsync(Deadline)).Single();
            acquired.Id.ShouldBe(second.Id);
            acquired.DispatcherId.ShouldNotBeNullOrWhiteSpace();
            (await store.MarkSentAsync(second.Id, acquired.DispatcherId!, CancellationToken.None))
                .ShouldBe(OutboxCompletionOutcome.Applied, "a receipt just published after contention must not already be expired");
        }
        catch (Exception error) { primary = error; }
        finally
        {
            clock.Release();
            var joins = new List<Task>();
            if (aStarted) { joins.Add(aResult.Task.Unwrap()); joins.Add(aExited.Task); joins.Add(JoinOwnedThreadAsync(a)); }
            if (bStarted) { joins.Add(bResult.Task.Unwrap()); joins.Add(bExited.Task); joins.Add(JoinOwnedThreadAsync(b)); }
            var joined = Task.WhenAll(joins);
            try { await joined.WaitAsync(Deadline); }
            catch (Exception error)
            {
                cleanupError = error;
                if (primary is not null) { output.WriteLine($"Secondary owned-operation cleanup failure: {error.GetType().Name}"); }
            }
            if (joined.IsCompleted)
            {
                store.Dispose();
                clock.Dispose();
            }
            else
            {
                // Timeout remains a test failure. Do not dispose the provider or gate while an owned
                // operation can still use either; retain their lifetime until the actual join completes.
                _ = joined.ContinueWith(_ => { store.Dispose(); clock.Dispose(); }, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        if (cleanupError is not null) { ExceptionDispatchInfo.Capture(cleanupError).Throw(); }
    }

    private static Task JoinOwnedThreadAsync(Thread thread) => Task.Run(() =>
    {
#pragma warning disable RS0030 // Join only the exact dedicated thread owned by this test, under an independent finite cleanup bound.
        if (!thread.Join(Deadline)) { throw new TimeoutException("Owned store-operation thread did not exit."); }
#pragma warning restore RS0030
    });

    private sealed class GatedClock : TimeProvider, IDisposable
    {
        private readonly ManualResetEventSlim _release = new(false);
        private long _ticks;
        private int _armed;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Arm() => Volatile.Write(ref _armed, 1);
        internal void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);
        internal void Release() => _release.Set();
        public override DateTimeOffset GetUtcNow()
        {
            var captured = Interlocked.Read(ref _ticks);
            if (Interlocked.CompareExchange(ref _armed, 0, 1) == 1)
            {
                Entered.TrySetResult();
#pragma warning disable RS0030 // Deliberately suspend the synchronous injected clock while the actual store monitor is held.
                if (!_release.Wait(Deadline)) { throw new TimeoutException("Owned completion clock gate was not released."); }
#pragma warning restore RS0030
            }
            return DateTimeOffset.UnixEpoch.AddTicks(captured);
        }
        public void Dispose() => _release.Dispose();
    }
}
