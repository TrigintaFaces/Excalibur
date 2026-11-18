// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Owns an authoritative durable scope and optional encoded state loaded before handler admission.</summary>
public sealed class SagaDurableSnapshot
{
    private readonly byte[]? _state;
    /// <summary>Initializes an immutable durable scope snapshot.</summary>
    /// <param name="incarnationId">The authoritative incarnation.</param>
    /// <param name="version">The committed state version.</param>
    /// <param name="generation">The scheduling generation.</param>
    /// <param name="nextEffectSequence">The next per-saga effect sequence; this is not a global delivery cursor.</param>
    /// <param name="closed">Whether the incarnation is sealed.</param>
    /// <param name="encodedState">The owned encoded state, or null when retained closure has outlived the payload.</param>
    /// <param name="completedAt">The original completion timestamp.</param>
    public SagaDurableSnapshot(Guid incarnationId,long version,long generation,long nextEffectSequence,bool closed,
        byte[]? encodedState,DateTimeOffset? completedAt)
    {
        if(incarnationId==Guid.Empty){throw new ArgumentException("Incarnation identifier must be nonempty.",nameof(incarnationId));}
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentOutOfRangeException.ThrowIfNegative(nextEffectSequence);
        if(encodedState is null&&!closed){throw new ArgumentException("An active committed incarnation cannot lack its state payload.",nameof(encodedState));}
        IncarnationId=incarnationId;Version=version;Generation=generation;NextEffectSequence=nextEffectSequence;
        Closed=closed;CompletedAt=completedAt;_state=encodedState is null?null:(byte[])encodedState.Clone();
    }
    /// <summary>Gets the authoritative incarnation.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Gets the committed version.</summary>
    public long Version { get; }
    /// <summary>Gets the scheduling generation.</summary>
    public long Generation { get; }
    /// <summary>Gets the next per-saga effect sequence, without a global delivery-order guarantee.</summary>
    public long NextEffectSequence { get; }
    /// <summary>Gets whether this incarnation is sealed.</summary>
    public bool Closed { get; }
    /// <summary>Gets the original completion timestamp.</summary>
    public DateTimeOffset? CompletedAt { get; }
    /// <summary>Returns a caller-owned state payload copy, or null for retained closure.</summary>
    /// <returns>The original encoded state bytes.</returns>
    public byte[]? GetEncodedState()=>_state is null?null:(byte[])_state.Clone();
}
