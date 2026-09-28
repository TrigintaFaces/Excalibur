// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Registration-time marker asserting that this host's subscription checkpoints survive a restart.
/// </summary>
/// <remarks>
/// <para>
/// A provider registers this marker <em>from the same registration that wires its checkpoint store</em>,
/// so the marker is never separately registerable and its presence is an authoritative signal that a
/// durability decision was actually made — not merely advertised. Presence is consumed as a
/// registration-time signal only; the marker is never resolved for behavior.
/// </para>
/// <para>
/// <b>It asserts a decision, not a technology.</b> A provider whose event store is itself in-process
/// registers it alongside the in-memory checkpoint store, because for that host a process-lifetime
/// checkpoint is the coherent choice: the events do not outlive the process either. What the marker
/// distinguishes is a host that chose, from one that silently inherited the fallback.
/// </para>
/// <para>
/// <b>Why the distinction is worth a marker.</b> Replaying an async projection is not idempotent. The
/// apply path loads the stored projection, applies the event and writes it back, and the stored
/// projection records no last-applied position — so a handler that assigns survives a replay while one
/// that accumulates double-counts on every restart, silently and without bound. The framework cannot
/// tell those two handlers apart, which is why the choice is forced rather than defaulted.
/// </para>
/// </remarks>
public interface ISubscriptionCheckpointDurability;
