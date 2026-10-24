// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

namespace RepositoryChecks;

/// <summary>Checks SQL-fragment placement over the independently evaluated shipping population.</summary>
internal static class SqlPredicateCensus
{
    /// <summary>Requires a nonempty population and reports every unplaced fragment.</summary>
    /// <exception cref="InvalidOperationException">No fragments were measured or excluded source could contain SQL.</exception>
    internal static async Task<int> WriteAsync(RepositoryCompilationCensus census, string root, string reportPath, CancellationToken cancellationToken)
    {
        foreach (var path in census.ExcludedSource)
        {
            if (TenantMutationCensus.ExcludedSourceContainsEvidence(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path))
            {
                throw new InvalidOperationException("Unevaluated SQL source: " + path);
            }
        }

        var fragments = census.Projects.Where(project => project.Shipping).SelectMany(project => SqlPredicatePlacement.Find(project.Compilation)).ToArray();
        if (fragments.Length == 0)
        {
            throw new InvalidOperationException("No SQL predicate fragments were examined.");
        }

        var missing = fragments.Where(fragment => !fragment.Placed).ToArray();
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            status = missing.Length == 0 ? "passed" : "failed",
            fragments = fragments.Select(fragment => fragment with { Path = Path.GetRelativePath(root, fragment.Path).Replace('\\', '/') }),
            missing,
        }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"EXAMINED: {fragments.Length} SQL predicate fragments; {missing.Length} unplaced.").ConfigureAwait(false);
        return missing.Length == 0 ? 0 : 1;
    }
}
