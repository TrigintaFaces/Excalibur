// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Owns the original scheduled timeout authority carried through claim and delivery.</summary>
public sealed class SagaTimeoutDeliveryIdentity
{
    /// <summary>Initializes immutable original timeout authority without generating a new identity.</summary>
    /// <param name="scope">The original exact tenant, persisted state type and saga identity.</param>
    /// <param name="timeoutId">The original nonblank opaque timeout identifier.</param>
    /// <param name="incarnationId">The original nonempty scheduling incarnation.</param>
    /// <param name="generation">The original nonnegative scheduling generation.</param>
    public SagaTimeoutDeliveryIdentity(SagaScopeIdentity scope, string timeoutId, Guid incarnationId, long generation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutId.Length, 450, nameof(timeoutId));
        if (incarnationId == Guid.Empty)
        {
            throw new ArgumentException("Timeout incarnation must be nonempty.", nameof(incarnationId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        Scope = scope;
        TimeoutId = timeoutId;
        IncarnationId = incarnationId;
        Generation = generation;
    }

    /// <summary>Gets the original storage authority.</summary>
    public SagaScopeIdentity Scope { get; }

    /// <summary>Gets the original opaque timeout identifier.</summary>
    public string TimeoutId { get; }

    /// <summary>Gets the original scheduling incarnation.</summary>
    public Guid IncarnationId { get; }

    /// <summary>Gets the original scheduling generation.</summary>
    public long Generation { get; }
}
