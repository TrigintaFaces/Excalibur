// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;

namespace Excalibur.Saga.Storage;

internal sealed partial class InMemorySagaTimeoutStore : ISagaTimerRecoveryStore
{
    // Same lock as all physical timer/claim operations. Retain compact authority after payload deletion.
    private readonly Dictionary<(KeyedTenantPartition Tenant, Guid SagaId), RecoveryScope> _recoveryScopes = [];
    private readonly Dictionary<string, long> _timerRevisions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _trackedTimerIds = new(StringComparer.Ordinal);

    public ValueTask<SagaTimerRecoveryFence> ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        cancellationToken.ThrowIfCancellationRequested();
        var partition = CurrentPartition();
        RequireTenant(domain.TenantId, partition);
        lock (_dueLock)
        {
            var key = (partition, domain.SagaId);
            if (!_recoveryScopes.TryGetValue(key, out var scope))
            {
                scope = new RecoveryScope();
                _recoveryScopes.Add(key, scope);
            }
            return ValueTask.FromResult(scope.Fence());
        }
    }

    public ValueTask<SagaTimerRecoveryApplyResult> ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(journal);
        cancellationToken.ThrowIfCancellationRequested();
        var partition = CurrentPartition();
        RequireTenant(journal.Scope.TenantId, partition);
        var intents = SagaTimerRecoveryCodec.Decode(journal);
        var digest = journal.GetIntentDigest();
        lock (_dueLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scopeKey = (partition, journal.Scope.SagaId);
            if (!_recoveryScopes.TryGetValue(scopeKey, out var scope))
            { throw new InvalidOperationException("A persisted timer journal has no retained receiver authority."); }
            if (scope.Incarnation == journal.IncarnationId && scope.Sequence == journal.Sequence)
            {
                if (scope.BatchId != journal.BatchId || scope.ExpectedFence != journal.ExpectedFence
                    || scope.ObservedRevision != journal.ObservedRevision || scope.StateType != journal.Scope.StateType
                    || !scope.Digest.AsSpan().SequenceEqual(digest))
                { throw new InvalidOperationException("Timer journal identity conflicts with the retained exact receipt."); }
                return ValueTask.FromResult(new SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome.AlreadyApplied,
                    scope.Fence(), scope.Results));
            }
            if (scope.Token != journal.ExpectedFence)
            {
                if (!scope.RetiredTokens.Contains(journal.ExpectedFence))
                { throw new InvalidOperationException("The original timer journal fence is unknown to this receiver domain."); }
                return ValueTask.FromResult(new SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome.Superseded, scope.Fence(), []));
            }
            if (scope.Incarnation == journal.IncarnationId && journal.Sequence < scope.Sequence)
            {
                return ValueTask.FromResult(new SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome.Superseded, scope.Fence(), []));
            }
            if (scope.Incarnation == journal.IncarnationId)
            {
                if (scope.StateType != journal.Scope.StateType || journal.Sequence != checked(scope.Sequence + 1))
                { throw new InvalidOperationException("Timer journal state type or sequence is not the next original batch."); }
            }
            else if (journal.Sequence != 1)
            { throw new InvalidOperationException("A fresh state incarnation starts with its first original timer batch."); }

            return ValueTask.FromResult(ApplyNewRecoveryBatch(journal, partition, intents, digest, scope));
        }
    }

    // Called only while ApplyAsync holds _dueLock. Planning, fallible precomputation and
    // publication remain in their original order inside that SAME uninterrupted lock interval.
    private SagaTimerRecoveryApplyResult ApplyNewRecoveryBatch(SagaTimerRecoveryJournal journal,
        KeyedTenantPartition partition, SagaTimerIntent[] intents, byte[] digest, RecoveryScope scope)
    {
        if (journal.ObservedRevision > scope.Revision)
        { throw new InvalidDataException("A journal cannot claim a future receiver observation."); }
        var revision = checked(scope.Revision + 1);
        var rows = new Dictionary<string, SagaTimeout?>(StringComparer.Ordinal);
        var known = new Dictionary<string, KnownTimer>(StringComparer.Ordinal);
        var cancellations = new Dictionary<string, CancellationFence>(StringComparer.Ordinal);
        var results = new List<SagaTimerRecoveryIntentResult>(intents.Length);
        var scheduledIds = new HashSet<string>(StringComparer.Ordinal);
        var cancelAll = scope.CancelAll;
        var sagaId = journal.Scope.SagaId.ToString();
        for (var ordinal = 0; ordinal < intents.Length; ordinal++)
        {
            var intent = intents[ordinal];
            var outcome = SagaTimerRecoveryIntentOutcome.Applied;
            var authorityRevision = revision;
            switch (intent.Action)
            {
                case SagaTimerAction.Schedule:
                    if (!scheduledIds.Add(intent.TimeoutId)) { throw new InvalidDataException("A batch cannot allocate one timer ID twice."); }
                    if (_trackedTimerIds.Contains(intent.TimeoutId) || _timeouts.ContainsKey(intent.TimeoutId))
                    { throw new InvalidOperationException("A fresh timer intent cannot overwrite an existing or retired timer identity."); }
                    var blocked = scope.CancelAll;
                    if (scope.Cancellations.TryGetValue(intent.TimeoutId, out var one) && one.Through > blocked.Through) { blocked = one; }
                    if (journal.ObservedRevision <= blocked.Through)
                    {
                        outcome = SagaTimerRecoveryIntentOutcome.Cancelled; authorityRevision = blocked.Revision;
                        known[intent.TimeoutId] = new KnownTimer(journal.IncarnationId, journal.Sequence,
                            journal.Scope.StateType, revision, Cancelled: true);
                        break;
                    }
                    var timeout = intent.Timeout! with { TenantId = partition.TenantId };
                    rows[intent.TimeoutId] = timeout;
                    known[intent.TimeoutId] = new KnownTimer(journal.IncarnationId, journal.Sequence,
                        journal.Scope.StateType, revision, Cancelled: false);
                    break;
                case SagaTimerAction.Cancel:
                    if (rows.ContainsKey(intent.TimeoutId))
                    {
                        rows[intent.TimeoutId] = null;
                        if (known.TryGetValue(intent.TimeoutId, out var own)) { known[intent.TimeoutId] = own with { Cancelled = true }; }
                    }
                    else if (_timeouts.TryGetValue(intent.TimeoutId, out var target) && MatchesDomain(target, partition, journal.Scope.SagaId))
                    {
                        if (RevisionOf(intent.TimeoutId) > journal.ObservedRevision)
                        { outcome = SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer; }
                        else { rows[intent.TimeoutId] = null; }
                    }
                    cancellations[intent.TimeoutId] = LaterCancellation(scope.Cancellations.GetValueOrDefault(intent.TimeoutId),
                        new CancellationFence(journal.ObservedRevision, revision));
                    break;
                case SagaTimerAction.CancelAll:
                    foreach (var ownId in rows.Keys.ToArray())
                    {
                        if (rows[ownId] is not null)
                        {
                            rows[ownId] = null;
                            if (known.TryGetValue(ownId, out var own)) { known[ownId] = own with { Cancelled = true }; }
                        }
                    }
                    foreach (var pair in _timeouts)
                    {
                        if (!MatchesDomain(pair.Value, partition, journal.Scope.SagaId) || rows.ContainsKey(pair.Key)) { continue; }
                        if (RevisionOf(pair.Key) > journal.ObservedRevision)
                        { outcome = SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer; }
                        else { rows[pair.Key] = null; }
                    }
                    cancelAll = LaterCancellation(cancelAll, new CancellationFence(journal.ObservedRevision, revision));
                    break;
                default: throw new InvalidDataException("Unknown timer action.");
            }
            var action = intent.Action switch
            {
                SagaTimerAction.Schedule => SagaPreparedTimerAction.Schedule,
                SagaTimerAction.Cancel => SagaPreparedTimerAction.Cancel,
                _ => SagaPreparedTimerAction.CancelAll,
            };
            results.Add(new(ordinal, action, intent.TimeoutId, outcome, authorityRevision));
        }
        // Precompute all fallible data before publishing under the shared physical-store lock.
        var token = scope.Token;
        if (scope.Incarnation != journal.IncarnationId)
        {
            do { token = Guid.NewGuid(); }
            while (token == Guid.Empty || token == scope.Token || scope.RetiredTokens.Contains(token));
        }
        if (token != scope.Token) { _ = scope.RetiredTokens.EnsureCapacity(checked(scope.RetiredTokens.Count + 1)); }
        var fence = new SagaTimerRecoveryFence(token, journal.IncarnationId, journal.Sequence, revision);
        var result = new SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome.Applied, fence, results);
        _ = _trackedTimerIds.EnsureCapacity(checked(_trackedTimerIds.Count + known.Count));
        foreach (var pair in rows)
        {
            if (pair.Value is null) { EndFailureSource(pair.Key); _ = _timeouts.TryRemove(pair.Key, out _); _ = _claims.Remove(pair.Key); _ = _timerRevisions.Remove(pair.Key); }
            else { var retained = CopyFailureTimeout(pair.Value); StartFailureSource(retained); _timeouts[pair.Key] = retained; _timerRevisions[pair.Key] = revision; }
        }
        foreach (var pair in known) { scope.Known[pair.Key] = pair.Value; _ = _trackedTimerIds.Add(pair.Key); }
        foreach (var pair in cancellations) { scope.Cancellations[pair.Key] = pair.Value; }
        scope.CancelAll = cancelAll;
        if (token != scope.Token) { _ = scope.RetiredTokens.Add(scope.Token); }
        scope.Token = token; scope.Incarnation = journal.IncarnationId; scope.Sequence = journal.Sequence;
        scope.Revision = revision; scope.BatchId = journal.BatchId; scope.ExpectedFence = journal.ExpectedFence;
        scope.ObservedRevision = journal.ObservedRevision; scope.StateType = journal.Scope.StateType;
        scope.Digest = digest; scope.Results = result.IntentResults;
        return result;
    }

    public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery); cancellationToken.ThrowIfCancellationRequested();
        var partition = CurrentPartition(); RequireTenant(delivery.Scope.TenantId, partition);
        lock (_dueLock)
        {
            return ValueTask.FromResult(IsRecoveryDeliveryCurrent(delivery, partition));
        }
    }

    private bool IsRecoveryDeliveryCurrent(SagaTimeoutDeliveryIdentity delivery, KeyedTenantPartition partition)
    {
            if (!_recoveryScopes.TryGetValue((partition, delivery.Scope.SagaId), out var scope)
                || !scope.Known.TryGetValue(delivery.TimeoutId, out var timer))
            { throw new InvalidOperationException("Original tracked timer authority is missing."); }
            var cancelledThrough = scope.CancelAll.Through;
            if (scope.Cancellations.TryGetValue(delivery.TimeoutId, out var one)) { cancelledThrough = Math.Max(cancelledThrough, one.Through); }
            return scope.Incarnation == delivery.IncarnationId && timer.Incarnation == delivery.IncarnationId
                && timer.Sequence == delivery.Generation && timer.StateType == delivery.Scope.StateType
                && !timer.Cancelled && timer.CreatedRevision > cancelledThrough;
    }

    private void BeforePublicSchedule(SagaTimeout timeout)
    {
        if (!Guid.TryParse(timeout.SagaId, out var id)) { return; }
        var partition = CurrentPartition();
        var key = (partition, id);
        if (!_recoveryScopes.TryGetValue(key, out var scope)) { scope = new RecoveryScope(); _recoveryScopes.Add(key, scope); }
        if (_trackedTimerIds.Contains(timeout.TimeoutId) || scope.Cancellations.ContainsKey(timeout.TimeoutId))
        { throw new InvalidOperationException("Public Schedule cannot overwrite a recovery-tracked timer ID; use conditional recovery authority."); }
        scope.Revision = checked(scope.Revision + 1); _timerRevisions[timeout.TimeoutId] = scope.Revision;
    }

    private void RecordPublicCancellation(KeyedTenantPartition partition, string sagaId, string? timeoutId)
    {
        if (!Guid.TryParse(sagaId, out var id)) { return; }
        var key = (partition, id);
        if (!_recoveryScopes.TryGetValue(key, out var scope)) { scope = new RecoveryScope(); _recoveryScopes.Add(key, scope); }
        var cancellation = new CancellationFence(scope.Revision, checked(scope.Revision + 1));
        scope.Revision = cancellation.Revision;
        if (timeoutId is null) { scope.CancelAll = LaterCancellation(scope.CancelAll, cancellation); }
        else { scope.Cancellations[timeoutId] = LaterCancellation(scope.Cancellations.GetValueOrDefault(timeoutId), cancellation); }
    }

    private ClaimedSagaTimeout ClaimWithRecoveryAuthority(SagaTimeout timeout, string claimToken)
    {
        var partition = KeyedTenantPartition.FromStoredValue(timeout.TenantId);
        if (Guid.TryParse(timeout.SagaId, out var sagaId)
            && _recoveryScopes.TryGetValue((partition, sagaId), out var scope)
            && scope.Known.TryGetValue(timeout.TimeoutId, out var timer))
        {
            return new ClaimedSagaTimeout(timeout, claimToken, new SagaTimeoutDeliveryIdentity(
                new SagaScopeIdentity(partition.TenantId, timer.StateType, sagaId), timeout.TimeoutId,
                timer.Incarnation, timer.Sequence));
        }
        return new ClaimedSagaTimeout(timeout, claimToken);
    }

    private bool MatchesPublicSaga(SagaTimeout timeout, KeyedTenantPartition partition, string sagaId) =>
        Guid.TryParse(sagaId, out var id) && _recoveryScopes.ContainsKey((partition, id))
            ? MatchesDomain(timeout, partition, id)
            : string.Equals(timeout.SagaId, sagaId, StringComparison.Ordinal);

    private long RevisionOf(string timeoutId) => _timerRevisions.GetValueOrDefault(timeoutId);
    private static CancellationFence LaterCancellation(CancellationFence left, CancellationFence right) => left.Through > right.Through ? left : right;
    private static bool MatchesDomain(SagaTimeout timeout, KeyedTenantPartition partition, Guid sagaId) =>
        OwnedBy(timeout, partition) && Guid.TryParse(timeout.SagaId, out var actual) && actual == sagaId;
    private static void RequireTenant(string tenant, KeyedTenantPartition partition)
    {
        if (!string.Equals(tenant, partition.TenantId, StringComparison.Ordinal))
        { throw new InvalidOperationException("Timer recovery tenant differs from the current receiver authority."); }
    }
    private readonly record struct CancellationFence(long Through, long Revision);
    private sealed record KnownTimer(Guid Incarnation, long Sequence, string StateType, long CreatedRevision, bool Cancelled);
    private sealed class RecoveryScope
    {
        internal Guid Token { get; set; } = Guid.NewGuid();
        internal HashSet<Guid> RetiredTokens { get; } = [];
        internal Guid Incarnation { get; set; }
        internal long Sequence { get; set; }
        internal long Revision { get; set; }
        internal string? StateType { get; set; }
        internal Guid BatchId { get; set; }
        internal Guid ExpectedFence { get; set; }
        internal long ObservedRevision { get; set; }
        internal byte[] Digest { get; set; } = [];
        internal IReadOnlyList<SagaTimerRecoveryIntentResult> Results { get; set; } = [];
        internal CancellationFence CancelAll { get; set; } = new(-1, 0);
        internal Dictionary<string, CancellationFence> Cancellations { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, KnownTimer> Known { get; } = new(StringComparer.Ordinal);
        internal SagaTimerRecoveryFence Fence() => new(Token, Incarnation, Sequence, Revision);
    }
}
