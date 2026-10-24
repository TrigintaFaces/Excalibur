// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Shares one complete compiler population across the repository policies.</summary>
internal static class RepositoryPolicyCensus
{
    /// <summary>Produces separate policy verdicts without repeatedly loading every project.</summary>
    internal static async Task<int> WriteAsync(MSBuildWorkspace workspace, string root, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var documentationReferences = await DocumentationReferences.PrepareAsync(root, cancellationToken).ConfigureAwait(false);
        var census = await RepositoryCompilationCensus.ReadAsync(workspace, root, cancellationToken).ConfigureAwait(false);
        var results = new List<int>
        {
            await ConformanceCensus.WriteAsync(workspace, root, Path.Combine(directory, "conformance.json"), cancellationToken).ConfigureAwait(false),
            await PersistenceHealthCensus.WriteAsync(workspace, root, Path.Combine(directory, "persistence-health.json"), cancellationToken, census).ConfigureAwait(false),
            await BackendCoverageCensus.WriteAsync(workspace, root, Path.Combine(directory, "backend-coverage.json"), cancellationToken, census).ConfigureAwait(false),
            await TestTimingCensus.WriteAsync(workspace, root, Path.Combine(directory, "test-timing.json"), cancellationToken, census).ConfigureAwait(false),
            await TenantMutationCensus.WriteAsync(workspace, root, Path.Combine(directory, "tenant-mutations.json"), cancellationToken, census).ConfigureAwait(false),
            await SqlPredicateCensus.WriteAsync(census, root, Path.Combine(directory, "sql-predicates.json"), cancellationToken).ConfigureAwait(false),
            await TestReliabilityCensus.WriteAsync(census, root, Path.Combine(directory, "test-reliability.json"), cancellationToken).ConfigureAwait(false),
            await RigorEvidenceCensus.WriteAsync(census, root, Path.Combine(directory, "rigor-evidence.json"), cancellationToken).ConfigureAwait(false),
            await CapabilityCensus.WriteAsync(census, root, Path.Combine(directory, "capability-wiring.json"), cancellationToken).ConfigureAwait(false),
            await CosmosAdmissionCensus.WriteInventoryAsync(census, Path.Combine(directory, "cosmos-admission.json"), cancellationToken).ConfigureAwait(false),
            await DocumentationCensus.WriteAsync(documentationReferences, root, Path.Combine(directory, "documentation-api.json"), cancellationToken).ConfigureAwait(false),
        };
        return results.Exists(code => code != 0) ? 1 : 0;
    }
}
