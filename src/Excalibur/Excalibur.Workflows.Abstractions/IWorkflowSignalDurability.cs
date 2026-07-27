// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// Registration-time capability marker declaring that the wired <see cref="IWorkflowSignalInbox"/> is backed
/// by durable storage: admitted signals and their deduplication keys survive a process restart, so a
/// producer's redelivery of the same <c>(instanceId, signalId)</c> after a restart is still deduplicated.
/// </summary>
/// <remarks>
/// <para>
/// A durable provider registers this marker <em>from the same registration that wires its durable store</em>
/// — the marker is never separately registerable, so its presence in the service collection is an
/// authoritative signal that the durable inbox was actually wired, not merely advertised. A provider whose
/// inbox holds signals in process memory (the default <c>InMemoryWorkflowSignalInbox</c>) does not register
/// it.
/// </para>
/// <para>
/// The marker exists so a consumer that requires durable signals can fail closed at startup (via
/// <c>RequireDurableSignalInbox()</c>) when only the in-memory inbox is present, rather than silently losing
/// admitted-but-undrained signals on restart. Presence is consumed as a registration-time signal only; the
/// marker is never resolved for behavior.
/// </para>
/// </remarks>
public interface IWorkflowSignalDurability;
