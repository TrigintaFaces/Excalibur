// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Compares a complete measured population with a bounded, exact debt ledger in both directions.</summary>
internal static class CoverageLedger
{
    /// <summary>Fails new gaps and stale ledger entries independently.</summary>
    /// <exception cref="InvalidOperationException">The ledger is duplicated, malformed, or exceeds its reviewed ceiling.</exception>
    internal static ConformanceVerdict Evaluate(IEnumerable<string> measured, IEnumerable<string> lines, int ceiling)
    {
        var entries = lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray();
        var ledger = entries.ToHashSet(StringComparer.Ordinal);
        if (entries.Length != ledger.Count || entries.Length > ceiling || entries.Any(entry => !entry.Contains('|', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Coverage debt must have unique qualified keys and cannot exceed its reviewed ceiling.");
        }

        var live = measured.ToHashSet(StringComparer.Ordinal);
        return new ConformanceVerdict(
            live.Except(ledger, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ledger.Except(live, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}
