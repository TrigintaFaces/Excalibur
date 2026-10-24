// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Records policy failures without confusing an empty fork population with missing evidence.</summary>
/// <param name="NewContracts">Private contract identities absent from the baseline.</param>
/// <param name="StaleExceptions">Baseline identities absent from the measured population.</param>
internal sealed record ConformanceVerdict(string[] NewContracts, string[] StaleExceptions)
{
    /// <summary>Gets a value indicating whether both policy comparisons passed.</summary>
    internal bool Passed => NewContracts.Length == 0 && StaleExceptions.Length == 0;
}
