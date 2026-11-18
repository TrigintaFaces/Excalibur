// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Binary;
using System.Data;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Messaging;

/// <summary>Owns the exact encoded state and ordered effect inputs of one durable transition attempt.</summary>
public sealed class SagaPreparedTransition
{
    private readonly byte[] _state;
    private readonly byte[] _digest;
    private readonly ISagaAtomicTimeoutParticipant? _timeoutParticipant;
    private readonly byte[]? _timeoutBinding;
    private readonly ReadOnlyCollection<SagaPreparedTimerMutation> _timers;
    private readonly ReadOnlyCollection<IPreparedTransactionalOutboxWrite> _writes;

    /// <summary>Initializes a prepared transition, copying state and hashing exact immutable prepared wire values.</summary>
    /// <param name="identity">The stable transition identity.</param>
    /// <param name="expectedVersion">The state CAS precondition, included in the digest but not receipt uniqueness.</param>
    /// <param name="generation">The current scheduling generation.</param>
    /// <param name="firstSequence">The first per-saga effect sequence from the loaded scope counter; not a global delivery-order promise.</param>
    /// <param name="completed">Whether this transition seals its incarnation.</param>
    /// <param name="encodedState">The already-serialized UTF-8 state including its processed marker.</param>
    /// <param name="timers">The ordered timer mutations.</param>
    /// <param name="writes">The ordered owning-provider prepared command/event writes.</param>
    /// <param name="completedAt">The original completion timestamp, preserved independently of the encoded state.</param>
    /// <param name="timeoutParticipant">The selected timer participant, required for timer mutations or owned timeout delivery; absent only with no selected timer store.</param>
    /// <param name="ownedTimeout">The original owned timeout authority, revalidated for every NEW commit.</param>
    /// <param name="isManualTimeout">Whether explicit manual-timeout admission must be revalidated at commit.</param>
    public SagaPreparedTransition(SagaTransitionIdentity identity, long expectedVersion, long generation,
        long firstSequence, bool completed, byte[] encodedState, IReadOnlyList<SagaPreparedTimerMutation> timers,
        IReadOnlyList<IPreparedTransactionalOutboxWrite> writes, DateTimeOffset? completedAt = null, ISagaAtomicTimeoutParticipant? timeoutParticipant = null, bool isManualTimeout = false, SagaTimeoutDeliveryIdentity? ownedTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (isManualTimeout && identity.DeliveryId is null)
        {
            throw new ArgumentException("Explicit manual timeout requires its original nonempty delivery identity.", nameof(identity));
        }
        if (ownedTimeout is not null && (isManualTimeout
            || !string.Equals(ownedTimeout.Scope.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(ownedTimeout.Scope.StateType, identity.SagaType, StringComparison.Ordinal)
            || ownedTimeout.Scope.SagaId != identity.SagaId || ownedTimeout.IncarnationId != identity.IncarnationId
            || ownedTimeout.Generation != generation || !string.Equals(ownedTimeout.TimeoutId, identity.DeliveryId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Owned timeout authority must exactly match its prepared transition and cannot be manual.", nameof(ownedTimeout));
        }
        OwnedTimeout = ownedTimeout;
        ArgumentNullException.ThrowIfNull(encodedState);
        ArgumentNullException.ThrowIfNull(timers);
        ArgumentNullException.ThrowIfNull(writes);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentOutOfRangeException.ThrowIfNegative(firstSequence);
        _ = checked(expectedVersion + 1);
        _ = checked(firstSequence + writes.Count);
        if (encodedState.Length == 0) { throw new ArgumentException("Encoded saga state cannot be empty.", nameof(encodedState)); }
        // Reject malformed UTF-8 without guessing or replacing bytes that the provider will persist.
        _ = new UTF8Encoding(false, true).GetCharCount(encodedState);
        Identity = identity;
        IsManualTimeout = isManualTimeout;
        ExpectedVersion = expectedVersion;
        Generation = generation;
        FirstSequence = firstSequence;
        Completed = completed;
        CompletedAt = completedAt;
        _timeoutParticipant = timeoutParticipant;
        _timeoutBinding = timeoutParticipant is null ? null : (byte[])timeoutParticipant.GetCanonicalBindingBytes().Clone();
        if (_timeoutBinding is { Length: 0 }) { throw new ArgumentException("Timer binding cannot be empty.", nameof(timeoutParticipant)); }
        if ((timers.Count != 0 || ownedTimeout is not null) && timeoutParticipant is null)
        {
            throw new ArgumentException("A timer mutation or owned timeout delivery requires its selected timer participant.", nameof(timeoutParticipant));
        }
        _state = (byte[])encodedState.Clone();
        _timers = Array.AsReadOnly(timers.ToArray());
        _writes = Array.AsReadOnly(writes.Select(static write => (IPreparedTransactionalOutboxWrite)new FrozenWrite(write)).ToArray());
        foreach (var timer in _timers)
        {
            ArgumentNullException.ThrowIfNull(timer);
        }
        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var write in _writes)
        {
            ArgumentNullException.ThrowIfNull(write);
            ArgumentException.ThrowIfNullOrWhiteSpace(write.MessageId);
            if (!messageIds.Add(write.MessageId))
            {
                throw new ArgumentException("A transition cannot stage the same message identifier twice.", nameof(writes));
            }
            var partition = write.TenantId ?? KeyedTenantPartition.Untenanted.TenantId;
            if (!string.Equals(partition, identity.TenantId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Prepared outbox tenant provenance differs from the transition authority.", nameof(writes));
            }
        }
        _digest = ComputeDigest();
    }

    /// <summary>Gets the immutable transition identity.</summary>
    public SagaTransitionIdentity Identity { get; }
    /// <summary>Gets the state CAS precondition.</summary>
    public long ExpectedVersion { get; }
    /// <summary>Gets the current scheduling generation.</summary>
    public long Generation { get; }
    /// <summary>Gets the first reserved effect sequence.</summary>
    public long FirstSequence { get; }
    /// <summary>Gets whether the transition seals the incarnation.</summary>
    public bool Completed { get; }
    /// <summary>Gets the original exact completion timestamp, or null.</summary>
    public DateTimeOffset? CompletedAt { get; }
    /// <summary>Gets whether this attempt captured a selected timer participant.</summary>
    /// <remarks>The provider must refuse omission when its configured profile requires a timer participant.</remarks>
    public bool HasTimerParticipant => _timeoutParticipant is not null;
    /// <summary>Gets immutable ordered timer mutations.</summary>
    public IReadOnlyList<SagaPreparedTimerMutation> Timers => _timers;
    /// <summary>Gets immutable owning-provider command/event handles.</summary>
    public IReadOnlyList<IPreparedTransactionalOutboxWrite> Writes => _writes;
    /// <summary>Returns caller-owned encoded state bytes.</summary>
    /// <returns>The exact copied state bytes.</returns>
    public byte[] GetEncodedState() => (byte[])_state.Clone();
    /// <summary>Returns a caller-owned SHA-256 digest of the versioned exact transition representation.</summary>
    /// <returns>The transition digest.</returns>
    public byte[] GetDigest() => (byte[])_digest.Clone();

    /// <summary>Validates selected timer ownership against the actual transaction writer before any writes.</summary>
    /// <param name="transaction">The actual open provider-owned transaction.</param>
    /// <param name="actualWriterBinding">The trusted kernel encoding of its actual backend and table.</param>
    public void ValidateTimerParticipant(IDbTransaction transaction, byte[] actualWriterBinding)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(actualWriterBinding);
        if (_timeoutParticipant is null) { return; }
        if (!_timeoutBinding!.AsSpan().SequenceEqual(actualWriterBinding)
            || !_timeoutBinding.AsSpan().SequenceEqual(_timeoutParticipant.GetCanonicalBindingBytes()))
        {
            throw new InvalidOperationException("Selected timer backend/table does not match the actual atomic writer.");
        }

        _timeoutParticipant.ValidateTransaction(transaction, Identity.TenantId);
        if (!_timeoutBinding.AsSpan().SequenceEqual(_timeoutParticipant.GetCanonicalBindingBytes()))
        {
            throw new InvalidOperationException("Selected timer binding changed during transaction validation.");
        }
    }

    /// <summary>Gets whether explicit manual-timeout identity admission must be checked again under the commit scope lock.</summary>
    public bool IsManualTimeout { get; }

    /// <summary>Gets the immutable original owned timeout authority, or null for nonowned delivery.</summary>
    public SagaTimeoutDeliveryIdentity? OwnedTimeout { get; }

    private byte[] ComputeDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AddString(hash, "excalibur-saga-transition-v6");
        AddNumber(hash, IsManualTimeout ? 1 : 0);
        AddNumber(hash, OwnedTimeout is null ? 0 : 1);
        if (OwnedTimeout is { } owned)
        {
            AddString(hash, owned.Scope.TenantId);
            AddString(hash, owned.Scope.StateType);
            AddString(hash, owned.Scope.SagaId.ToString("N"));
            AddString(hash, owned.TimeoutId);
            AddString(hash, owned.IncarnationId.ToString("N"));
            AddNumber(hash, owned.Generation);
        }
        AddBytes(hash, _timeoutBinding);
        AddString(hash, Identity.TenantId);
        AddString(hash, Identity.SagaType);
        AddString(hash, Identity.SagaId.ToString("N"));
        AddString(hash, Identity.IncarnationId.ToString("N"));
        AddString(hash, Identity.TransitionId.ToString("N"));
        AddString(hash, Identity.DeliveryId);
        AddNumber(hash, ExpectedVersion);
        AddNumber(hash, Generation);
        AddNumber(hash, FirstSequence);
        AddNumber(hash, Completed ? 1 : 0);
        AddInstant(hash, CompletedAt);
        AddBytes(hash, _state);
        AddNumber(hash, _timers.Count);
        foreach (var timer in _timers)
        {
            AddNumber(hash, (long)timer.Action);
            AddString(hash, timer.TimeoutId);
            AddNumber(hash, timer.Generation);
            AddString(hash, timer.TimeoutType);
            AddString(hash, timer.SagaType);
            AddBytes(hash, timer.GetData());
            AddInstant(hash, timer.DueAt);
            AddInstant(hash, timer.ScheduledAt);
        }
        AddNumber(hash, _writes.Count);
        foreach (var write in _writes)
        {
            AddString(hash, write.MessageId);
            AddString(hash, write.TenantId);
            AddBytes(hash, write.GetCanonicalWireBytes());
        }
        return hash.GetHashAndReset();
    }

    private static void AddInstant(IncrementalHash hash, DateTimeOffset? value)
    {
        AddNumber(hash, value.HasValue ? 1 : 0);
        if (value.HasValue)
        {
            AddNumber(hash, value.Value.Ticks);
            AddNumber(hash, value.Value.Offset.Ticks);
        }
    }

    private static void AddNumber(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AddBytes(IncrementalHash hash, byte[]? bytes)
    {
        AddNumber(hash, bytes?.LongLength ?? -1);
        if (bytes is not null) { hash.AppendData(bytes); }
    }

    private static void AddString(IncrementalHash hash, string? value)
    {
        AddNumber(hash, value?.Length ?? -1);
        if (value is null) { return; }
        // Exact UTF-16 code units preserve ordinal opaque identity, including trailing spaces and surrogates.
        Span<byte> bytes = stackalloc byte[sizeof(char)];
        foreach (var character in value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, character);
            hash.AppendData(bytes);
        }
    }
    private sealed class FrozenWrite : IPreparedTransactionalOutboxWrite
    {
        private readonly IPreparedTransactionalOutboxWrite _inner;
        private readonly byte[] _wire;

        internal FrozenWrite(IPreparedTransactionalOutboxWrite inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _inner = inner;
            MessageId = inner.MessageId;
            TenantId = inner.TenantId;
            _wire = inner.GetCanonicalWireBytes() ?? throw new InvalidOperationException("Prepared provider returned no canonical wire.");
            if (_wire.Length == 0) { throw new InvalidOperationException("Prepared canonical wire must contain its versioned persisted values."); }
            _wire = (byte[])_wire.Clone();
            Verify();
        }
        public string MessageId { get; }
        public string? TenantId { get; }
        public byte[] GetCanonicalWireBytes() => (byte[])_wire.Clone();
        public async ValueTask StageAsync(System.Data.IDbTransaction transaction, CancellationToken cancellationToken)
        {
            Verify();
            await _inner.StageAsync(transaction, cancellationToken).ConfigureAwait(false);
            Verify();
        }
        private void Verify()
        {
            if (!string.Equals(MessageId, _inner.MessageId, StringComparison.Ordinal)
                || !string.Equals(TenantId, _inner.TenantId, StringComparison.Ordinal)
                || !_wire.AsSpan().SequenceEqual(_inner.GetCanonicalWireBytes()))
            {
                throw new InvalidOperationException("Prepared provider identity or canonical wire changed after preparation.");
            }
        }
    }

}
