// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Names one exact compact receiver state; it is an observation, not ownership of saga state.</summary>
public sealed class SagaTimerRecoveryFence
{
    /// <summary>Creates an observed receiver fence.</summary>
    /// <param name="token">Nonempty opaque token, replaced only on state-incarnation advancement; batch sequence and mutation revision are separate.</param>
    /// <param name="incarnationId">Current state incarnation, or empty only before the first batch.</param>
    /// <param name="sequence">Last atomically applied batch sequence within that incarnation.</param>
    /// <param name="mutationRevision">Monotone receiver revision including public cancellations/retirement.</param>
    public SagaTimerRecoveryFence(Guid token, Guid incarnationId, long sequence, long mutationRevision)
    {
        if (token == Guid.Empty) { throw new ArgumentException("A receiver fence token must be nonempty.", nameof(token)); }
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfNegative(mutationRevision);
        if (incarnationId == Guid.Empty != (sequence == 0))
        {
            throw new ArgumentException("Only an uninitialized receiver may have zero sequence.", nameof(sequence));
        }
        Token = token; IncarnationId = incarnationId; Sequence = sequence; MutationRevision = mutationRevision;
    }
    /// <summary>Gets the exact opaque receiver token.</summary>
    public Guid Token { get; }
    /// <summary>Gets the currently applied incarnation.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Gets the last applied sequence.</summary>
    public long Sequence { get; }
    /// <summary>Gets the provider-serialized mutation revision used to fence captured intents.</summary>
    public long MutationRevision { get; }
}
