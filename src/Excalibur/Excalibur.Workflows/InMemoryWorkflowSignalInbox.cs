// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Workflows;

/// <summary>
/// In-process default <see cref="IWorkflowSignalInbox"/>: a dedup-keyed, append-ordered signal mailbox held
/// in memory.
/// </summary>
/// <remarks>
/// <para>
/// Admission dedups on <c>(instanceId, signalId)</c> via a conditional add — redelivery of the same
/// identifier returns <see langword="false"/> and does not append. Entries carry a per-instance monotonic
/// sequence so drain order is deterministic.
/// </para>
/// <para>
/// Correct only for a single process whose signals may be lost on restart. Both the admitted signals and the
/// deduplication keys live in this object: when the process ends, an admitted-but-undrained signal is gone,
/// and a producer redelivering the same <c>(instanceId, signalId)</c> afterwards is admitted again. A
/// workflow that treats a signal as an effect will therefore apply it twice across a restart.
/// </para>
/// <para>
/// No durable implementation ships. A deployment that requires signals to survive a restart, or to cross
/// processes, must supply its own <see cref="IWorkflowSignalInbox"/>; the registration uses
/// <c>TryAddSingleton</c> so a consumer registration takes precedence over this one.
/// </para>
/// </remarks>
internal sealed class InMemoryWorkflowSignalInbox : IWorkflowSignalInbox
{
    private readonly ConcurrentDictionary<string, InstanceInbox> _byInstance = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask<bool> TryEnqueueAsync(
        string instanceId,
        string signalId,
        string signalName,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        var inbox = _byInstance.GetOrAdd(instanceId, static _ => new InstanceInbox());
        return ValueTask.FromResult(inbox.TryAdd(signalId, signalName, payloadJson));
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WorkflowSignalEntry>> DrainAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        return _byInstance.TryGetValue(instanceId, out var inbox)
            ? ValueTask.FromResult(inbox.Snapshot())
            : ValueTask.FromResult<IReadOnlyList<WorkflowSignalEntry>>([]);
    }

    private sealed class InstanceInbox
    {
        private readonly System.Threading.Lock _gate = new();
        private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);
        private readonly List<WorkflowSignalEntry> _entries = [];
        private long _sequence;

        public bool TryAdd(string signalId, string signalName, string? payloadJson)
        {
            lock (_gate)
            {
                if (!_seenIds.Add(signalId))
                {
                    return false;
                }

                _entries.Add(new WorkflowSignalEntry
                {
                    Sequence = ++_sequence,
                    SignalId = signalId,
                    SignalName = signalName,
                    PayloadJson = payloadJson,
                });
                return true;
            }
        }

        public IReadOnlyList<WorkflowSignalEntry> Snapshot()
        {
            lock (_gate)
            {
                // Already in ascending sequence order (append-only); copy so callers never see later mutation.
                return _entries.ToArray();
            }
        }
    }
}
