// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Applies exact contract identities and a shrink-only exception ledger.</summary>
internal static class ConformancePolicy
{
    /// <summary>Returns new private contracts and stale exceptions as independently failing sets.</summary>
    /// <exception cref="InvalidOperationException">The baseline contains duplicate or unqualified identities.</exception>
    internal static ConformanceVerdict Evaluate(IEnumerable<ConformanceFork> forks, IEnumerable<string> lines)
    {
        var entries = lines.Select(line => line.Trim()).Where(line => line.Length != 0 && !line.StartsWith('#')).ToArray();
        var baseline = entries.ToHashSet(StringComparer.Ordinal);
        if (entries.Any(entry => !entry.Contains(':', StringComparison.Ordinal)) || baseline.Count != entries.Length)
        {
            throw new InvalidOperationException("Conformance exceptions must be unique assembly-qualified source identities.");
        }

        var live = forks.Select(fork => fork.BaseIdentity).ToHashSet(StringComparer.Ordinal);
        return new ConformanceVerdict(
            live.Except(baseline, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            baseline.Except(live, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}
