// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Identifies the roster and member lifetimes covered by a projection freshness observation.</summary>
/// <param name="SubscriptionName">The subscription whose roster was observed.</param>
/// <param name="RosterIdentity">The identity of that roster snapshot.</param>
/// <param name="ThroughPosition">The contiguous stream position covered.</param>
/// <param name="CoveredMembers">Projection keys and binding identifiers whose progress was confirmed.</param>
/// <remarks>Freshness is an observation, not a promise that the roster will never change. Readers validate this receipt after reading data.</remarks>
public sealed record ProjectionAttestation(string SubscriptionName, string RosterIdentity,
	long ThroughPosition, IReadOnlyDictionary<string, Guid> CoveredMembers);

/// <summary>The result of completing projections after a committed save.</summary>
/// <param name="Freshness">The bounded completion outcome.</param>
/// <param name="Attestation">The roster receipt, present when built-in processing confirms freshness.</param>
public readonly record struct ProjectionCompletion(ProjectionFreshness Freshness, ProjectionAttestation? Attestation = null);
