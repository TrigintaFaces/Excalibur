// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;

namespace Excalibur.Dispatch.Messaging;

/// <summary>Owns the original encoded timer intents persisted with ordinary saga state and dedup.</summary>
/// <remarks>
/// EncodedIntents is a versioned strict frame containing ordered action, original timeout ID, type,
/// saga routing type, owned payload bytes, due/scheduled UTC instants. No delegates/runtime objects.
/// The coordinator and receiver must validate the full frame/count/length/UTF-16 identity guards before
/// Save/apply respectively. Providers impose the same reviewed configured payload limits; a caller's
/// claimed limit never grants authority. No silent truncation or partial batch acceptance.
/// </remarks>
public sealed class SagaTimerRecoveryJournal
{
    private readonly byte[] _encodedIntents;
    /// <summary>Restores or creates an owned original intent journal.</summary>
    /// <param name="scope">Exact scope of the committed state.</param>
    /// <param name="incarnationId">Nonempty identity persisted across that state's lifetime.</param>
    /// <param name="sequence">Strictly positive next journal sequence for the incarnation.</param>
    /// <param name="batchId">Stable nonempty batch identity, never replaced on ambiguous retry.</param>
    /// <param name="expectedFence">Receiver observation obtained before authoritative state load.</param>
    /// <param name="observedRevision">Receiver revision captured with the expected fence before state load.</param>
    /// <param name="encodingVersion">Exact supported intent frame version.</param>
    /// <param name="encodedIntents">Complete primitive intent frame, copied on input and output.</param>
    public SagaTimerRecoveryJournal(SagaScopeIdentity scope, Guid incarnationId, long sequence,
        Guid batchId, Guid expectedFence, long observedRevision, int encodingVersion, byte[] encodedIntents)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(encodedIntents);
        if (incarnationId == Guid.Empty || batchId == Guid.Empty || expectedFence == Guid.Empty)
        {
            throw new ArgumentException("Incarnation, batch and expected fence identities must be nonempty.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(observedRevision);
        ArgumentOutOfRangeException.ThrowIfNotEqual(encodingVersion, 1);
        if (encodedIntents.Length == 0) { throw new ArgumentException("A timer intent frame cannot be empty.", nameof(encodedIntents)); }
        Scope = scope; IncarnationId = incarnationId; Sequence = sequence; BatchId = batchId;
        ExpectedFence = expectedFence; ObservedRevision = observedRevision; EncodingVersion = encodingVersion;
        _encodedIntents = (byte[])encodedIntents.Clone();
    }
    /// <summary>Gets the exact authoritative state scope.</summary>
    public SagaScopeIdentity Scope { get; }
    /// <summary>Gets the persisted state incarnation.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Gets the original monotone batch sequence.</summary>
    public long Sequence { get; }
    /// <summary>Gets the original immutable batch identity.</summary>
    public Guid BatchId { get; }
    /// <summary>Gets the expected receiver token; authority rebindinging requires a new guarded state CAS.</summary>
    public Guid ExpectedFence { get; }
    /// <summary>Gets the captured cancellation/retirement revision; never advanced during retry.</summary>
    public long ObservedRevision { get; }
    /// <summary>Returns the timer authority domain without introducing a state-type partition.</summary>
    public SagaTimerRecoveryDomain GetDomain() => new(Scope.TenantId, Scope.SagaId);
    /// <summary>Gets the supported primitive frame version.</summary>
    public int EncodingVersion { get; }
    /// <summary>Gets a fresh copy of the complete original timer intents.</summary>
    public byte[] EncodedIntents => (byte[])_encodedIntents.Clone();
    /// <summary>Gets a fresh payload digest; receiver receipt additionally binds all immutable header fields.</summary>
    public byte[] GetIntentDigest() => SHA256.HashData(_encodedIntents);
    /// <summary>Gets an owned digest of the exact immutable journal header and intent frame for state CAS.</summary>
    /// <remarks>Versioned length framing preserves ordinal UTF-16 identities, including code units;
    /// this is journal equality metadata, not a signature or a replacement for provider authority.</remarks>
    public byte[] GetAuthorityDigest()
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(1);
            WriteText(writer, Scope.TenantId);
            WriteText(writer, Scope.StateType);
            writer.Write(Scope.SagaId.ToByteArray());
            writer.Write(IncarnationId.ToByteArray());
            writer.Write(Sequence);
            writer.Write(BatchId.ToByteArray());
            writer.Write(ExpectedFence.ToByteArray());
            writer.Write(ObservedRevision);
            writer.Write(EncodingVersion);
            writer.Write(_encodedIntents.Length);
            writer.Write(_encodedIntents);
        }
        return SHA256.HashData(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));

        static void WriteText(BinaryWriter writer, string value)
        {
            writer.Write(value.Length);
            foreach (var character in value) { writer.Write((ushort)character); }
        }
    }
}
