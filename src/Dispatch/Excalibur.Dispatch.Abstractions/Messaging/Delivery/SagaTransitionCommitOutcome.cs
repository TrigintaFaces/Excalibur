// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Describes authoritative outcomes of a store-owned durable transition.</summary>
public enum SagaTransitionCommitOutcome
{
    /// <summary>No server outcome was established; caller must resolve the same immutable identity.</summary>
    Unknown = 0,
    /// <summary>The server transaction committed the exact batch.</summary>
    Committed = 1,
    /// <summary>An existing receipt proves that this exact immutable batch already committed.</summary>
    AlreadyCommitted = 2,
    /// <summary>A different state version won; no writes from this batch committed.</summary>
    VersionConflict = 3,
    /// <summary>The same exact transition identifier names a different immutable digest; delivery replay has its distinct outcome.</summary>
    IdentityConflict = 4,
    /// <summary>The requested incarnation or scheduling generation is no longer active.</summary>
    IncarnationClosed = 5,
    /// <summary>The original delivery committed another winning attempt; discard speculative state and reload it.</summary>
    DeliveryAlreadyCommitted = 6,
    /// <summary>An authoritative synchronized lookup found no committed receipt.</summary>
    NotFound = 7,
}
