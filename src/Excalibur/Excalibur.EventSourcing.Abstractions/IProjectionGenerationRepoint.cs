// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
namespace Excalibur.EventSourcing;

/// <summary>Conditionally changes the destination resolved by projection readers.</summary>
/// <remarks>Canonical destination identities use the same format as IProjectionDestinationIdentity. Implementations preserve reader configuration and never retire member state or delete destinations.</remarks>
public interface IProjectionGenerationRepoint
{
    /// <summary>Changes an expected reader destination to a new destination, or observes an already completed transition.</summary>
    /// <param name="expectedDestination">The canonical destination expected before the transition.</param>
    /// <param name="destination">The canonical destination readers should resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task completing when the conditional update is acknowledged.</returns>
    /// <remarks>A transport failure after submission has an unknown outcome. Retry by reading authoritative state. Concurrent external reconfiguration outside this protocol requires separate coordination.</remarks>
    Task RepointAsync(string expectedDestination, string destination, CancellationToken cancellationToken);

    /// <summary>Reads the authoritative destination currently resolved by readers.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The canonical destination, or null when no single destination is configured. Read failures throw.</returns>
    Task<string?> GetActiveDestinationAsync(CancellationToken cancellationToken);
}
