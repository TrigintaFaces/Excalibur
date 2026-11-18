// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using Excalibur.Dispatch.Messaging;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Diagnostics;

/// <summary>Reports bounded dedup pressure only after positive state-write acknowledgement.</summary>
internal static partial class SagaDeduplicationDiagnostics
{
    private static readonly Meter Meter = new(TelemetrySagaStoreDecorator.MeterName);
    private static readonly Counter<long> Evictions = Meter.CreateCounter<long>(
        "excalibur.saga.dedup_evictions",
        description: "Coordinator-owned recent identity evictions observed after state-write acknowledgement.");

    internal static bool MarkProcessed(SagaState state, string identity)
    {
        var before = state.ProcessedEventIds.Count;
        var added = state.TryMarkEventProcessed(identity);
        return added && before > 0 && state.ProcessedEventIds.Count == before;
    }

    internal static void Report(ILogger logger, string mode)
    {
        // Observers are independent: neither may replace an acknowledged commit or prevent the other.
        try { Evictions.Add(1, new KeyValuePair<string, object?>("mode", mode)); }
        catch (Exception error) when (error is not (AccessViolationException or StackOverflowException)) { }
        try { LogEviction(logger, mode); }
        catch (Exception error) when (error is not (AccessViolationException or StackOverflowException)) { }
    }

    [LoggerMessage(SagaEventId.SagaDeduplicationEntryEvicted, LogLevel.Warning,
        "An acknowledged {Mode} saga state write evicted a recent delivery identity; an older duplicate can execute again.",
        EventName = "SagaDeduplicationEntryEvicted")]
    private static partial void LogEviction(ILogger logger, string mode);
}
