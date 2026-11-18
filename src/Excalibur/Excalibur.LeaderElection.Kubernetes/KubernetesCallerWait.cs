// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
namespace Excalibur.LeaderElection.Kubernetes;

// This bounds only a wait. Existing latches/retirement own actual SDK and cancellation callbacks.
internal static class KubernetesCallerWait
{
    private static readonly TimeSpan MaximumSlice = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);
    internal static async Task WaitAsync(Task operation, long callerStart, TimeSpan budget,
        TimeProvider clock, CancellationToken caller)
    {
        while (true)
        {
            caller.ThrowIfCancellationRequested();
            var age = clock.GetElapsedTime(callerStart);
            if (age < TimeSpan.Zero) { throw new InvalidOperationException("The monotonic caller clock moved backwards."); }
            if (age >= budget) { throw new TimeoutException("Kubernetes caller wait exceeded RenewInterval + GracePeriod."); }
            var remaining = budget - age;
            var ticks = Math.Min(remaining.Ticks, MaximumSlice.Ticks);
            var milliseconds = Math.Max(1L, (ticks + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond);
            var slice = TimeSpan.FromMilliseconds(milliseconds);
            try { await operation.WaitAsync(slice, clock, caller).ConfigureAwait(false); }
            catch (TimeoutException) when (!operation.IsFaulted && clock.GetElapsedTime(callerStart) < budget) { continue; }
            if (clock.GetElapsedTime(callerStart) >= budget)
            { throw new TimeoutException("Kubernetes completion arrived outside its caller budget."); }
            return;
        }
    }
}
