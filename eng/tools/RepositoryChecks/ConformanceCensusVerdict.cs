// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Combines independent contract, arm-reference, and reflection-guard policies.</summary>
internal sealed record ConformanceCensusVerdict(ConformanceVerdict Contracts, string[] ArmFailures, ConformanceGuardResult Guards)
{
    /// <summary>Gets a value indicating whether every measured policy passed.</summary>
    internal bool Passed => Contracts.Passed && ArmFailures.Length == 0 && Guards.Failures.Length == 0;

    /// <summary>Refuses missing guard evidence and preserves each policy's failures.</summary>
    /// <exception cref="InvalidOperationException">The published guard population was not evaluated.</exception>
    internal static ConformanceCensusVerdict Evaluate(ConformanceEvidence evidence, IEnumerable<string> baseline) => new(
        ConformancePolicy.Evaluate(evidence.Forks, baseline),
        ConformanceArmWiring.Evaluate(evidence.ArmSuites),
        evidence.Guards ?? throw new InvalidOperationException("Published conformance guards were not evaluated."));
}
