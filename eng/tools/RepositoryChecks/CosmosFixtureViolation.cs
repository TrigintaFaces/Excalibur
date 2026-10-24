// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Identifies an invalid client construction at its source location.</summary>
/// <param name="message">The failed requirement.</param>
/// <param name="location">The source location.</param>
internal sealed class CosmosFixtureViolation(string message, Location location)
{
    /// <summary>Gets the failed requirement.</summary>
    public string Message { get; } = message;

    /// <summary>Gets the source location.</summary>
    public Location Location { get; } = location;
}
