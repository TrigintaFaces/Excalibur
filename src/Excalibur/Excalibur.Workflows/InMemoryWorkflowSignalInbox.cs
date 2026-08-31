// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

namespace Excalibur.Workflows;

/// <summary>
/// In-process default <see cref="IWorkflowSignalInbox"/>: a dedup-keyed, append-ordered signal mailbox held
/// in memory.
/// </summary>
/// <remarks>
/// <para>
/// Admission dedups on <c>(tenant, instanceId, signalId)</c> via a conditional add — redelivery of the same
/// identifier returns <see langword="false"/> and does not append. Entries carry a per-instance monotonic
/// sequence so drain order is deterministic. In a single-tenant deployment the tenant term is the reserved
/// untenanted marker, so the key shape does not vary by deployment.
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
    private readonly ConcurrentDictionary<(string TenantId, string InstanceId), InstanceInbox> _byInstance = new();
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWorkflowSignalInbox"/> class.
    /// </summary>
    /// <param name="tenantContext">
    /// Resolves the tenant each operation addresses. Consulted per call rather than captured, because one
    /// registered inbox serves every caller and the tenant belongs to the operation. Its resolved value
    /// becomes part of the mailbox key, so two tenants signalling distinct instances under one instance id
    /// do not collide.
    /// <para>
    /// Required, not optional. A caller that deliberately runs untenanted passes
    /// <see cref="UntenantedContext.Instance"/>, which names the reserved untenanted partition explicitly.
    /// Were the dependency omissible, "this host runs untenanted" and "the context was forgotten" would
    /// reach the inbox as the same state, and the two name different partitions — so a signal admitted
    /// under one would stop being drainable under the other with nothing raised.
    /// </para>
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="tenantContext"/> is <see langword="null"/>.</exception>
    public InMemoryWorkflowSignalInbox(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        _tenantContext = tenantContext;
    }

    /// <summary>
    /// The tenant partition the current call addresses, re-resolved per call.
    /// </summary>
    /// <remarks>
    /// Re-read rather than captured because this inbox is registered once and serves every caller: the
    /// tenant is a property of the operation, not of the instance. <see cref="TenantScope.FromContext"/>
    /// fails closed when multi-tenancy is active but no tenant is resolved, so the inbox cannot reach a key
    /// with no tenant term in it, and yields the reserved untenanted marker in a single-tenant deployment.
    /// </remarks>
    private TenantScope CurrentTenantScope => TenantScope.FromContext(_tenantContext);

    /// <summary>
    /// Composes the mailbox key for the instance as addressed by the calling tenant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tenant term is part of the key because a signal's identity in an inbox includes the tenant it
    /// belongs to. Both <c>instanceId</c> and <c>signalId</c> are producer-supplied strings, unique only
    /// within the system that issued them, so two tenants routinely present the same pair. Keyed without
    /// the tenant, both resolve to one mailbox, and the admission half leaves nothing behind: the second
    /// tenant's signal fails the deduplication check, so <see cref="TryEnqueueAsync"/> reports "not newly
    /// admitted" and discards it — not stored, not logged, not errored. That workflow then waits forever
    /// for a signal the system received and threw away. The drain half is a disclosure on the same key: a
    /// read on the instance id alone returns another tenant's producer-authored payloads.
    /// </para>
    /// <para>
    /// The key is a tuple, not a delimited string, so it is injective by construction: an instance id
    /// containing the character a string form would join on cannot shift a term across the tuple boundary
    /// and collide with another tenant's mailbox.
    /// </para>
    /// <para>
    /// Widening the key does not weaken deduplication — a redelivery <em>within</em> one tenant still
    /// collides and is still refused; it stops one tenant's signal from being mistaken for another's.
    /// </para>
    /// </remarks>
    private (string TenantId, string InstanceId) GetKey(string instanceId)
        => (CurrentTenantScope.TenantId, instanceId);

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

        var inbox = _byInstance.GetOrAdd(GetKey(instanceId), static _ => new InstanceInbox());
        return ValueTask.FromResult(inbox.TryAdd(signalId, signalName, payloadJson));
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WorkflowSignalEntry>> DrainAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        return _byInstance.TryGetValue(GetKey(instanceId), out var inbox)
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
