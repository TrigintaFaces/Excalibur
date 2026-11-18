// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Storage;

internal sealed partial class InMemorySagaTimeoutStore : ISagaTimeoutFailureStore
{
    // Every access is inside the SAME _dueLock as payloads, claims and receiver authority.
    // One latest disposition per opaque ID; replacement discards the prior lifetime's receipt.
    private readonly Dictionary<string, FailureSource> _failureSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failureTokens = new(StringComparer.Ordinal);
    private readonly string _failureTarget = Guid.NewGuid().ToString("N");

    bool ISagaTimeoutFailureStore.HoldsSurviveProcessRestart => false;

    ValueTask ISagaTimeoutFailureStore.ValidateFailureStorageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    ValueTask<SagaTimeoutHoldAttempt> ISagaTimeoutFailureStore.TryHoldFailureAsync(
        SagaTimeoutFailureObservation observation, ClaimedSagaTimeout claim, Guid operationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(claim.Timeout);
        ArgumentException.ThrowIfNullOrEmpty(claim.ClaimToken);
        if (operationId == Guid.Empty) { throw new ArgumentException("A hold operation ID must be nonempty.", nameof(operationId)); }
        cancellationToken.ThrowIfCancellationRequested();
        var original = observation.OriginalClaim;
        var current = SagaTimeoutFailureObservation.CopyClaim(claim);
        var partition = CurrentPartition();
        lock (_dueLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Establish same selected target AND raw identity before absence can prove anything.
            if (!OwnedBy(original.Timeout, partition) || !OwnedBy(current.Timeout, partition)
                || !ValidFailureEpoch(original) || !ValidFailureEpoch(current)
                || !SameFailureAddress(original.Timeout, current.Timeout))
            { return HoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
            _failureSources.TryGetValue(original.Timeout.TimeoutId, out var source);
            // Same operation's conflicting inputs are rejected BEFORE observing another current hold.
            if (source is not null && source.SourceEpoch == original.SourceEpoch && source.HoldOperation == operationId)
            {
                if (source.Observation is null || !SameObservation(source.Observation, observation)
                    || source.HoldClaim is null || !SameFailureClaim(source.HoldClaim, current))
                { return HoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
                return source.State == SagaTimeoutHoldState.Held
                    ? HoldAttempt(SagaTimeoutHoldOutcome.Held, FailureReceipt(source))
                    : HoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded, FailureReceipt(source));
            }
            if (source is null || source.SourceEpoch != original.SourceEpoch || source.DeliveryEpoch != original.DeliveryEpoch)
            { return HoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded); }
            if (!SameFailureTimeout(original.Timeout, source.Timeout)
                || !SameFailureTimeout(current.Timeout, source.Timeout)
                || current.SourceEpoch != source.SourceEpoch || current.DeliveryEpoch != source.DeliveryEpoch)
            { return HoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
            if (!_timeouts.TryGetValue(source.Timeout.TimeoutId, out var timeout))
            { return HoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded); }
            var owned = ClaimWithRecoveryAuthority(timeout, current.ClaimToken).OwnedDelivery;
            if (!SameFailureTimeout(source.Timeout, timeout)
                || !SameFailureAuthority(original.OwnedDelivery, owned) || !SameFailureAuthority(current.OwnedDelivery, owned))
            { return HoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
            if (!FailureReceiverCurrent(timeout)) { return HoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded); }
            // Another operation may already hold the SAME source/cycle. This observes real exclusion,
            // not proof that this caller's operation committed; no input/lease is mutated.
            if (source.State == SagaTimeoutHoldState.Held)
            { return HoldAttempt(SagaTimeoutHoldOutcome.Held, FailureReceipt(source)); }
            if (!_claims.TryGetValue(timeout.TimeoutId, out var owner)
                || !string.Equals(owner.Owner, current.ClaimToken, StringComparison.Ordinal))
            { return HoldAttempt(SagaTimeoutHoldOutcome.RetryWithFreshClaim); }
            var nextVersion = checked(source.Version + 1);
            var token = Guid.NewGuid().ToString("N");
            var captured = new SagaTimeoutFailureObservation(original, observation.Kind, observation.Reason);
            var receipt = new SagaTimeoutHoldReceipt(new(token), SagaTimeoutHoldState.Held,
                source.Timeout, observation.Kind, observation.Reason);
            if (source.Token is not null) { _ = _failureTokens.Remove(source.Token); }
            source.Token = token; source.Version = nextVersion; source.State = SagaTimeoutHoldState.Held;
            source.HoldOperation = operationId; source.Observation = captured; source.HoldClaim = current;
            source.ResumeCommand = null;
            _failureTokens[token] = source.Timeout.TimeoutId;
            _ = _claims.Remove(source.Timeout.TimeoutId);
            return HoldAttempt(SagaTimeoutHoldOutcome.Held, receipt);
        }
    }

    private static ValueTask<SagaTimeoutHoldAttempt> HoldAttempt(SagaTimeoutHoldOutcome outcome, SagaTimeoutHoldReceipt? receipt = null) =>
        ValueTask.FromResult(new SagaTimeoutHoldAttempt(outcome, receipt));

    ValueTask<SagaTimeoutHoldReceipt?> ISagaTimeoutFailureStore.TryResumeHeldFailureAsync(
        SagaTimeoutHoldToken token, Guid commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (commandId == Guid.Empty) { throw new ArgumentException("A resume command ID must be nonempty.", nameof(commandId)); }
        cancellationToken.ThrowIfCancellationRequested();
        var partition = CurrentPartition();
        lock (_dueLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_failureTokens.TryGetValue(token.Value, out var id) || !_failureSources.TryGetValue(id, out var source)
                || source.Token != token.Value || !OwnedBy(source.Timeout, partition))
            { return ValueTask.FromResult<SagaTimeoutHoldReceipt?>(null); }
            if (source.ResumeCommand == commandId)
            { return ValueTask.FromResult(FailureReceipt(source)); }
            if (source.State != SagaTimeoutHoldState.Held || !_timeouts.TryGetValue(id, out var timeout)
                || !SameFailureTimeout(source.Timeout, timeout) || !FailureReceiverCurrent(source.Timeout))
            { return ValueTask.FromResult<SagaTimeoutHoldReceipt?>(null); }
            var version = checked(source.Version + 1);
            var cycle = Guid.NewGuid().ToString("N");
            var result = new SagaTimeoutHoldReceipt(token, SagaTimeoutHoldState.Resumed,
                source.Timeout, source.Observation!.Kind, source.Observation.Reason);
            source.Version = version; source.DeliveryEpoch = cycle;
            source.State = SagaTimeoutHoldState.Resumed; source.ResumeCommand = commandId;
            return ValueTask.FromResult<SagaTimeoutHoldReceipt?>(result);
        }
    }

    ValueTask<SagaTimeoutHoldPage> ISagaTimeoutFailureStore.GetHeldFailuresAsync(
        string tenantId, string? sagaId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 256);
        cancellationToken.ThrowIfCancellationRequested();
        // An exact-partition filter is not an authorization grant; require the ambient partition too.
        if (!string.Equals(tenantId, CurrentPartition().TenantId, StringComparison.Ordinal))
        { throw new InvalidOperationException("Held timer query tenant differs from the current receiver authority."); }
        var prefix = string.Join('|', _failureTarget, FailureCursorPart(tenantId), sagaId is null ? "-" : FailureCursorPart(sagaId)) + "|";
        var after = ReadFailureCursor(cursor, prefix);
        lock (_dueLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = _failureSources.Values.Where(row => row.State == SagaTimeoutHoldState.Held
                && string.Equals(row.Timeout.TenantId, tenantId, StringComparison.Ordinal)
                && (sagaId is null || string.Equals(row.Timeout.SagaId, sagaId, StringComparison.Ordinal))
                && (after is null || string.CompareOrdinal(row.Timeout.TimeoutId, after) > 0))
                .OrderBy(row => row.Timeout.TimeoutId, StringComparer.Ordinal).Take(pageSize + 1).ToArray();
            var page = rows.Take(pageSize).Select(row => FailureReceipt(row)!).ToArray();
            var next = rows.Length > pageSize ? prefix + FailureCursorPart(page[^1].Timeout.TimeoutId) : null;
            return ValueTask.FromResult(new SagaTimeoutHoldPage(page, next));
        }
    }

    private bool FailureReceiverCurrent(SagaTimeout timeout)
    {
        var authority = ClaimWithRecoveryAuthority(timeout, "authority-observation").OwnedDelivery;
        return authority is null || IsRecoveryDeliveryCurrent(authority, KeyedTenantPartition.FromStoredValue(timeout.TenantId));
    }

    private static bool SameFailureAuthority(SagaTimeoutDeliveryIdentity? left, SagaTimeoutDeliveryIdentity? right) =>
        left is null ? right is null : right is not null && left.TimeoutId == right.TimeoutId
            && left.Scope.TenantId == right.Scope.TenantId && left.Scope.StateType == right.Scope.StateType
            && left.Scope.SagaId == right.Scope.SagaId && left.IncarnationId == right.IncarnationId && left.Generation == right.Generation;

    private static bool SameFailureAddress(SagaTimeout left, SagaTimeout right) =>
        left.TimeoutId == right.TimeoutId && left.TenantId == right.TenantId && left.SagaId == right.SagaId;

    private static bool SameFailureTimeout(SagaTimeout left, SagaTimeout right) =>
        left.TimeoutId == right.TimeoutId && left.TenantId == right.TenantId && left.SagaId == right.SagaId
        && left.SagaType == right.SagaType && left.TimeoutType == right.TimeoutType
        && left.DueAt.EqualsExact(right.DueAt) && left.ScheduledAt.EqualsExact(right.ScheduledAt)
        && (left.TimeoutData is null ? right.TimeoutData is null : right.TimeoutData is not null && left.TimeoutData.AsSpan().SequenceEqual(right.TimeoutData));

    private static bool SameFailureClaim(ClaimedSagaTimeout left, ClaimedSagaTimeout right) =>
        left.ClaimToken == right.ClaimToken && left.SourceEpoch == right.SourceEpoch && left.DeliveryEpoch == right.DeliveryEpoch
        && SameFailureTimeout(left.Timeout, right.Timeout) && SameFailureAuthority(left.OwnedDelivery, right.OwnedDelivery);

    private static bool SameObservation(SagaTimeoutFailureObservation left, SagaTimeoutFailureObservation right) =>
        left.Kind == right.Kind && left.Reason == right.Reason && SameFailureClaim(left.OriginalClaim, right.OriginalClaim);

    private static SagaTimeout CopyFailureTimeout(SagaTimeout timeout) => timeout with { TimeoutData = timeout.TimeoutData?.ToArray() };
    private static SagaTimeoutHoldReceipt? FailureReceipt(FailureSource source) =>
        source.Token is null || source.Observation is null ? null : new(new(source.Token), source.State,
            source.Timeout, source.Observation.Kind, source.Observation.Reason);

    private void StartFailureSource(SagaTimeout timeout)
    {
        if (_failureSources.TryGetValue(timeout.TimeoutId, out var previous) && previous.Token is not null)
        { _ = _failureTokens.Remove(previous.Token); }
        _failureSources[timeout.TimeoutId] = new FailureSource(CopyFailureTimeout(timeout), FailureSourceBinding(timeout));
        _ = _claims.Remove(timeout.TimeoutId);
    }

    private void EndFailureSource(string id)
    {
        if (_failureSources.Remove(id, out var source) && source.Token is not null)
        { _ = _failureTokens.Remove(source.Token); }
    }

    // Matches the existing process-local pending-count diagnostic used by store lifecycle controls.
    internal int GetFailureSourceCount()
    {
        lock (_dueLock) { return _failureSources.Count; }
    }

    private bool IsFailureHeld(string id) => _failureSources.TryGetValue(id, out var source) && source.State == SagaTimeoutHoldState.Held;
    private ClaimedSagaTimeout ProjectFailureEpochs(ClaimedSagaTimeout claim)
    {
        var source = _failureSources[claim.Timeout.TimeoutId];
        return claim with { Timeout = CopyFailureTimeout(claim.Timeout), SourceEpoch = source.SourceEpoch, DeliveryEpoch = source.DeliveryEpoch };
    }

    private string FailureSourceBinding(SagaTimeout timeout)
    {
        // Framed primitive identity, not authentication. The instance is this process-local backend.
        var framed = string.Join('|', _failureTarget, FailureCursorPart(timeout.TenantId),
            FailureCursorPart(timeout.SagaId), FailureCursorPart(timeout.TimeoutId));
        return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(framed.AsSpan())));
    }

    private bool ValidFailureEpoch(ClaimedSagaTimeout claim)
    {
        var prefix = FailureSourceBinding(claim.Timeout) + ":";
        return claim.SourceEpoch is { } epoch && epoch.Length == prefix.Length + 32
            && epoch.StartsWith(prefix, StringComparison.Ordinal)
            && IsCanonicalFailureGuid(epoch[prefix.Length..]) && IsCanonicalFailureGuid(claim.DeliveryEpoch);
    }

    private static bool IsCanonicalFailureGuid(string? value) =>
        Guid.TryParseExact(value, "N", out var parsed) && parsed != Guid.Empty
        && string.Equals(parsed.ToString("N"), value, StringComparison.Ordinal);

    private static string FailureCursorPart(string value) => Convert.ToBase64String(MemoryMarshal.AsBytes(value.AsSpan()));
    private static string? ReadFailureCursor(string? cursor, string prefix)
    {
        if (cursor is null) { return null; }
        if (!cursor.StartsWith(prefix, StringComparison.Ordinal)) { throw new ArgumentException("Cursor belongs to another target or exact partition/filter.", nameof(cursor)); }
        try
        {
            var bytes = Convert.FromBase64String(cursor[prefix.Length..]);
            if ((bytes.Length & 1) != 0) { throw new FormatException("Invalid cursor code-unit count."); }
            return new string(MemoryMarshal.Cast<byte, char>(bytes.AsSpan()));
        }
        catch (FormatException error) { throw new ArgumentException("Invalid held timer cursor.", nameof(cursor), error); }
    }

    private sealed class FailureSource(SagaTimeout timeout, string binding)
    {
        internal SagaTimeout Timeout { get; set; } = timeout;
        internal string SourceEpoch { get; } = binding + ":" + Guid.NewGuid().ToString("N");
        internal string DeliveryEpoch { get; set; } = Guid.NewGuid().ToString("N");
        internal long Version { get; set; }
        internal SagaTimeoutHoldState State { get; set; } = SagaTimeoutHoldState.Resumed;
        internal string? Token { get; set; }
        internal Guid HoldOperation { get; set; }
        internal SagaTimeoutFailureObservation? Observation { get; set; }
        internal ClaimedSagaTimeout? HoldClaim { get; set; }
        internal Guid? ResumeCommand { get; set; }
    }
}
