// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

namespace Excalibur.Dispatch;

/// <summary>Owns an immutable provider-encoded outbox write prepared before a transaction begins.</summary>
/// <remarks>
/// Preparation, including configured payload transforms and metadata encoding, has already completed.
/// Implementations must preserve the same row identity and exact encoded values across staging retries.
/// This handle is process-local; persist its canonical data, never this object or a delegate.
/// </remarks>
public interface IPreparedTransactionalOutboxWrite
{
    /// <summary>Gets the owning provider's immutable outbox row identifier.</summary>
    string MessageId { get; }

    /// <summary>Gets the immutable message tenant provenance, independent of later ambient context.</summary>
    string? TenantId { get; }

    /// <summary>Returns a caller-owned copy of the complete, versioned canonical persisted wire representation.</summary>
    /// <returns>A fresh byte array covering the exact payload, encoded headers/metadata, routing and transport values, and the provider/table ownership binding.</returns>
    /// <remarks>
    /// The representation must be unambiguous and stable. Mutating the returned array cannot affect this
    /// handle. Staging consumes precisely the encoded values represented here, without reserializing caller objects.
    /// </remarks>
    byte[] GetCanonicalWireBytes();

    /// <summary>Inserts this already-prepared write on the supplied transaction without committing or rolling it back.</summary>
    /// <param name="transaction">The exclusive transaction owned by the coordinating store.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The asynchronous staging operation.</returns>
    /// <remarks>
    /// Implementations validate their supported concrete transaction and use its exact connection. They do not
    /// open a second connection, run a payload transform, regenerate identity, re-encode metadata or commit.
    /// A successful insert is provisional until the transaction owner commits. Duplicate identity is not
    /// automatically idempotent acceptance; the owner must first resolve its authoritative transition receipt.
    /// </remarks>
    ValueTask StageAsync(IDbTransaction transaction, CancellationToken cancellationToken);
}
