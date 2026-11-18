// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Delivery;

// Only aggregate counts leave this check; envelope/tenant identifiers are never enumerated across tenants.
internal sealed class ScheduleDispositionHealth : IHealthCheck
{
    private sealed record Pending(int Count, DateTimeOffset NextAttempt, DateTimeOffset LastDiagnostic, int AdmissionAttempt, ScheduleFailureDisposition Disposition);
    private readonly Lock _gate = new();
    private readonly Dictionary<(Guid Id, Guid Generation, DateTimeOffset Occurrence), Pending> _pending = [];

    internal bool CanAttempt(Guid id, Guid generation, DateTimeOffset occurrence, DateTimeOffset now)
    {
        lock (_gate) { return !_pending.TryGetValue((id, generation, occurrence), out var pending) || now >= pending.NextAttempt; }
    }

    internal bool Unresolved(Guid id, Guid generation, DateTimeOffset occurrence, DateTimeOffset now, TimeSpan pollInterval, int admissionAttempt, ScheduleFailureDisposition disposition)
    {
        lock (_gate)
        {
            var key = (id, generation, occurrence);
            var count = _pending.TryGetValue(key, out var pending) ? Math.Min(pending.Count + 1, 7) : 1;
            var cap = TimeSpan.FromSeconds(60).Ticks;
            var initial = Math.Min(cap, Math.Max(TimeSpan.FromSeconds(1).Ticks, pollInterval.Ticks));
            var multiplier = 1L << Math.Min(count - 1, 6);
            var delay = initial >= cap / multiplier ? cap : initial * multiplier;
            var report = pending is null || now - pending.LastDiagnostic >= TimeSpan.FromSeconds(60);
            _pending[key] = new Pending(count, now.AddTicks(delay), report ? now : pending!.LastDiagnostic, admissionAttempt, disposition);
            return report;
        }
    }

    internal bool DeferReconciliation(Guid id, Guid generation, DateTimeOffset occurrence, DateTimeOffset now, TimeSpan pollInterval)
    {
        lock (_gate)
        {
            return _pending.TryGetValue((id, generation, occurrence), out var pending)
                && Unresolved(id, generation, occurrence, now, pollInterval, pending.AdmissionAttempt, pending.Disposition);
        }
    }

    internal bool IsPending(Guid id, Guid generation, DateTimeOffset occurrence)
    {
        lock (_gate) { return _pending.ContainsKey((id, generation, occurrence)); }
    }

    internal bool Confirms(ScheduleOccurrenceFailureSnapshot failure)
    {
        lock (_gate)
        {
            return _pending.TryGetValue((failure.Token.ScheduleId, failure.Token.Generation, failure.Token.OccurrenceUtc), out var pending)
                && (failure.Disposition == ScheduleFailureDisposition.Quarantined
                    || (pending.AdmissionAttempt > 0 && failure.AdmissionAttempts >= pending.AdmissionAttempt
                        && failure.Disposition == pending.Disposition));
        }
    }

    internal void Clear(Guid id, Guid generation, DateTimeOffset occurrence)
    {
        lock (_gate) { _pending.Remove((id, generation, occurrence)); }
    }

    internal void RetainCurrent(IEnumerable<(Guid Id, Guid Generation, DateTimeOffset? Occurrence)> inventory)
    {
        var retained = inventory.Where(x => x.Occurrence is not null)
            .Select(x => (x.Id, x.Generation, x.Occurrence!.Value)).ToHashSet();
        lock (_gate)
        {
            foreach (var key in _pending.Keys.Where(key => !retained.Contains(key)).ToArray()) { _pending.Remove(key); }
        }
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_pending.Count == 0 ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Scheduled occurrence disposition persistence is unresolved; retained work is not quarantined or fulfilled.",
                    data: new Dictionary<string, object> { ["pendingDispositionUnresolvedCount"] = _pending.Count }));
        }
    }
}
