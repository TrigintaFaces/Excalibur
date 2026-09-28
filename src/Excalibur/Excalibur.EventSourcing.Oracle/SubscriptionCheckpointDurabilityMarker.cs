// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>
/// Asserts that this host's subscription checkpoints survive a restart. Registered only by the
/// registration that wires the Oracle checkpoint store, so it cannot be present without it.
/// </summary>
/// <remarks>
/// Carries no state and is never resolved for behavior; its presence in the container is the whole
/// signal.
/// </remarks>
internal sealed class SubscriptionCheckpointDurabilityMarker : ISubscriptionCheckpointDurability;
