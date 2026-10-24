// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Records fixture enrollment and client configuration violations.</summary>
/// <param name="fixtures">The number of enrolled fixture types.</param>
/// <param name="clients">The number of inspected SDK client construction sites.</param>
/// <param name="violations">The sites whose configuration could not be proved.</param>
internal sealed class CosmosFixtureResult(int fixtures, int clients, IReadOnlyList<CosmosFixtureViolation> violations)
{
    /// <summary>Gets the enrolled fixture count.</summary>
    public int Fixtures { get; } = fixtures;

    /// <summary>Gets the inspected client count.</summary>
    public int Clients { get; } = clients;

    /// <summary>Gets the unresolved construction sites.</summary>
    public IReadOnlyList<CosmosFixtureViolation> Violations { get; } = violations;
}
