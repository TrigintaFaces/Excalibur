// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection;

/// <summary>Schedules serial election work from an actual request's monotonic start timestamp.</summary>
/// <remarks>
/// Submillisecond intervals have a one-millisecond scheduling floor. This does not extend a
/// tenure deadline: a budget shorter than the host's timer granularity can expire before a retry.
/// Oversized intervals are split into timer-supported slices. Timer callbacks do not prove elapsed
/// time; early callbacks re-arm against the original timestamp rather than completing early.
/// </remarks>
internal static class LeaderElectionDelay
{
    private const long MaximumDelayTicks = (uint.MaxValue - 1L) * TimeSpan.TicksPerMillisecond;

    internal static Task WaitAsync(TimeSpan interval, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        return WaitFromAsync(clock.GetTimestamp(), interval, clock, cancellationToken);
    }

    internal static async Task WaitFromAsync(long requestStartTimestamp, TimeSpan interval,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        // Normalize BEFORE comparing elapsed time. A fast failed submillisecond request must
        // not repeatedly appear overdue and turn retries into a CPU/backend hot loop.
        var effectiveIntervalTicks = Math.Max(interval.Ticks, TimeSpan.TicksPerMillisecond);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsed = clock.GetElapsedTime(requestStartTimestamp);
            if (elapsed < TimeSpan.Zero)
            {
                throw new InvalidOperationException("The election clock moved backwards relative to the request start.");
            }
            if (elapsed.Ticks >= effectiveIntervalTicks) { return; }
            // Subtraction is only performed when 0 <= elapsed < interval, including MaxValue.
            var remainingTicks = effectiveIntervalTicks - elapsed.Ticks;
            var sliceTicks = Math.Min(remainingTicks, MaximumDelayTicks);
            var milliseconds = sliceTicks / TimeSpan.TicksPerMillisecond;
            if (sliceTicks % TimeSpan.TicksPerMillisecond != 0) { milliseconds++; }
            // The multiplication cannot overflow: milliseconds is bounded by uint.MaxValue-1.
            var due = TimeSpan.FromTicks(milliseconds * TimeSpan.TicksPerMillisecond);
            await Task.Delay(due, clock, cancellationToken).ConfigureAwait(false);
        }
    }
}
