// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Services;

// Per-worker unresolved disposition observations, not a durable outcome or a retry ownership scope.
// The single delivery pump serializes access. Committed provider state remains the authority.
internal sealed class SagaTimeoutFailureDelivery(
    ISagaTimeoutStore store, IMessageFailureClassifier classifier,
    ILogger<SagaTimeoutDeliveryService> logger, int maxPending, TimeProvider? timeProvider = null)
{
    private static readonly Action<ILogger, string, string, Exception?> PendingLog = LoggerMessage.Define<string, string>(
        LogLevel.Warning, new EventId(0, "SagaTimeoutHoldUnresolved"),
        "Timeout {TimeoutId} for saga {SagaId} has an unresolved hold; source retained, no delivery acknowledged");
    private readonly Dictionary<FailureKey, PendingHold> _pending = [];
    private readonly int _capacity = maxPending > 0 ? maxPending : throw new ArgumentOutOfRangeException(nameof(maxPending));
    internal bool HasCapacity => _pending.Count < _capacity;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private ISagaTimeoutFailureStore? _failureStore;

    internal async Task ValidateAsync(CancellationToken cancellationToken)
    {
        _failureStore = store.GetService(typeof(ISagaTimeoutFailureStore)) as ISagaTimeoutFailureStore
            ?? throw new InvalidOperationException("Hosted saga timeout delivery requires ISagaTimeoutFailureStore on the selected timeout store. Configure a preserving store/decorator and its schema before starting the pump; manual timeout APIs remain available.");
        await _failureStore.ValidateFailureStorageAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task RetryPendingAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in _pending.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var tenant = TenantContextHolder.BeginScope(entry.Value.Observation.OriginalClaim.Timeout.TenantId);
            await TryPersistAsync(entry.Key, entry.Value, null, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<bool> SuppressPendingAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken)
    {
        var key = Key(claim);
        // Keys bind opaque source/cycle authority. A different source at the same text address
        // does not prove that an old selected backend's observation ended. Only provider outcome does.
        if (!_pending.TryGetValue(key, out var pending)) { return false; }
        if (!SameSource(pending.Observation.OriginalClaim, claim))
        { throw new InvalidOperationException("A reclaimed timeout changed payload/authority without changing its source or delivery epoch."); }
        await TryPersistAsync(key, pending, claim, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task RecordFaultAsync(ClaimedSagaTimeout claim, Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) { return; }
        MessageFailureKind kind;
        try { kind = classifier.Classify(exception); }
        catch (Exception classifierError)
        {
            // No trustworthy retry classification: retain as Unknown, without claiming classifier success.
            PendingLog(logger, claim.Timeout.TimeoutId, claim.Timeout.SagaId, classifierError);
            kind = MessageFailureKind.Unknown;
        }
        if (kind is MessageFailureKind.Transient or MessageFailureKind.Uncertain) { return; }
        if (kind is not (MessageFailureKind.Permanent or MessageFailureKind.Poison or MessageFailureKind.Unknown))
        { kind = MessageFailureKind.Unknown; }
        await RecordFailureAsync(claim, kind, exception.Message, cancellationToken).ConfigureAwait(false);
    }

    internal Task RecordUntypedResultAsync(ClaimedSagaTimeout claim, string? reason, CancellationToken cancellationToken) =>
        RecordFailureAsync(claim, MessageFailureKind.Unknown, reason ?? "Dispatch returned an unsuccessful untyped result.", cancellationToken);

    private async Task RecordFailureAsync(ClaimedSagaTimeout claim, MessageFailureKind kind, string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasCapacity) { throw new InvalidOperationException("Timeout dispatch exceeded its configured pending-observation capacity."); }
        var observation = new SagaTimeoutFailureObservation(claim, kind, reason.Length <= 1024 ? reason : reason[..1024]);
        var pending = new PendingHold(observation);
        var key = Key(claim);
        _pending.Add(key, pending);
        await TryPersistAsync(key, pending, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryPersistAsync(FailureKey key, PendingHold pending, ClaimedSagaTimeout? freshClaim, CancellationToken cancellationToken)
    {
        // The real poll reconciles pending operations BEFORE acquiring fresh claims. Preserve a
        // validated fresh observation even if that first reconciliation just advanced the backoff.
        if (freshClaim is not null) { pending.ReclaimedClaim = SagaTimeoutFailureObservation.CopyClaim(freshClaim); }
        if (_clock.GetUtcNow() < pending.NextAttempt) { return; }
        try
        {
            var failureStore = _failureStore ?? throw new InvalidOperationException("Timeout hold storage was not validated before delivery.");
            // Always reconcile the ORIGINAL operation inputs first. A thrown/unknown acknowledgment
            // never permits silently swapping its claim token under the same operation ID.
            var attempt = await failureStore.TryHoldFailureAsync(pending.Observation, pending.OperationClaim,
                pending.OperationId, cancellationToken).ConfigureAwait(false);
            var rebind = pending.ReclaimedClaim;
            if (attempt.Outcome == SagaTimeoutHoldOutcome.RetryWithFreshClaim && rebind is not null && rebind.ClaimToken != pending.OperationClaim.ClaimToken)
            {
                if (!SameSource(pending.Observation.OriginalClaim, rebind))
                { throw new InvalidOperationException("A hold rebind does not match the original failed source and cycle."); }
                pending.OperationClaim = SagaTimeoutFailureObservation.CopyClaim(rebind);
                pending.OperationId = Guid.NewGuid();
                attempt = await failureStore.TryHoldFailureAsync(pending.Observation, pending.OperationClaim,
                    pending.OperationId, cancellationToken).ConfigureAwait(false);
            }
            if (attempt.Outcome is SagaTimeoutHoldOutcome.Held or SagaTimeoutHoldOutcome.ObservationEnded)
            {
                // Same bound provider certifies hold or definitive original-source end. Release the
                // complete observation/payload even when cancellation produces no future claim.
                // Neither result acknowledges delivery. Rejected never proves source absence.
                _pending.Remove(key);
                return;
            }
            if (attempt.Outcome == SagaTimeoutHoldOutcome.Rejected)
            { PendingLog(logger, key.TimeoutId, key.SagaId, new InvalidOperationException("The selected timeout hold authority rejected the bound target/tenant/operation inputs; repair configuration or operation binding.")); }
            else { PendingLog(logger, key.TimeoutId, key.SagaId, null); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { PendingLog(logger, key.TimeoutId, key.SagaId, error); }
        pending.Attempts = Math.Min(pending.Attempts + 1, 6);
        pending.NextAttempt = _clock.GetUtcNow().AddSeconds(Math.Min(60, 1 << (pending.Attempts - 1)));
    }

    private static FailureKey Key(ClaimedSagaTimeout claim)
    {
        if (string.IsNullOrEmpty(claim.SourceEpoch) || string.IsNullOrEmpty(claim.DeliveryEpoch))
        { throw new InvalidOperationException("The selected timeout claim did not project both validated source and delivery epochs."); }
        return new(claim.Timeout.TenantId, claim.Timeout.SagaId, claim.Timeout.TimeoutId, claim.SourceEpoch, claim.DeliveryEpoch);
    }

    private static bool SameSource(ClaimedSagaTimeout original, ClaimedSagaTimeout current) =>
        Key(original) == Key(current) && SameOwnedIdentity(original.OwnedDelivery, current.OwnedDelivery)
        && original.Timeout.SagaType == current.Timeout.SagaType && original.Timeout.TimeoutType == current.Timeout.TimeoutType
        && original.Timeout.DueAt == current.Timeout.DueAt && original.Timeout.ScheduledAt == current.Timeout.ScheduledAt
        && ((original.Timeout.TimeoutData is null && current.Timeout.TimeoutData is null)
            || (original.Timeout.TimeoutData is not null && current.Timeout.TimeoutData is not null
                && original.Timeout.TimeoutData.AsSpan().SequenceEqual(current.Timeout.TimeoutData)));

    private static bool SameOwnedIdentity(SagaTimeoutDeliveryIdentity? left, SagaTimeoutDeliveryIdentity? right) =>
        left is null ? right is null : right is not null
        && left.TimeoutId == right.TimeoutId && left.IncarnationId == right.IncarnationId && left.Generation == right.Generation
        && left.Scope.TenantId == right.Scope.TenantId && left.Scope.SagaId == right.Scope.SagaId
        && left.Scope.StateType == right.Scope.StateType;

    private readonly record struct FailureKey(string TenantId, string SagaId, string TimeoutId, string SourceEpoch, string DeliveryEpoch);

    private sealed class PendingHold(SagaTimeoutFailureObservation observation)
    {
        internal SagaTimeoutFailureObservation Observation { get; } = observation;
        internal ClaimedSagaTimeout OperationClaim { get; set; } = observation.OriginalClaim;
        internal Guid OperationId { get; set; } = Guid.NewGuid();
        internal ClaimedSagaTimeout? ReclaimedClaim { get; set; }
        internal int Attempts { get; set; }
        internal DateTimeOffset NextAttempt { get; set; }
    }
}
