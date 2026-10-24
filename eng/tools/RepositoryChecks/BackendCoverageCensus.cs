// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Enforces semantic backend bindings against the exact existing debt ledger.</summary>
internal static class BackendCoverageCensus
{
    /// <summary>Independently inventories source and test projects and enforces new and stale gaps.</summary>
    /// <exception cref="InvalidOperationException">A trusted contract, kit, or provider population is missing.</exception>
    internal static async Task<int> WriteAsync(MSBuildWorkspace workspace, string root, string reportPath, CancellationToken cancellationToken, RepositoryCompilationCensus? evaluated = null)
    {
        var census = evaluated ?? await RepositoryCompilationCensus.ReadAsync(workspace, root, cancellationToken).ConfigureAwait(false);
        var published = census.Assembly(root, "src/Excalibur/Excalibur.Testing.Conformance/Excalibur.Testing.Conformance.csproj");
        var facts = BackendContract.All.Select(pair => Measure(census, root, published, pair)).ToArray();
        var gaps = facts.SelectMany(fact => fact.Missing.Select(provider => fact.Contract + "|" + provider)).ToArray();
        var baseline = await File.ReadAllLinesAsync(Path.Combine(root, "eng/ci/conformance-backend-coverage-baseline.txt"), cancellationToken).ConfigureAwait(false);
        var verdict = CoverageLedger.Evaluate(gaps, baseline, 14);
        var report = new
        {
            status = verdict.Passed ? "passed-with-existing-debt" : "failed",
            verdict,
            existingDebtCount = gaps.Length,
            sourceProjects = census.Projects.Count(project => project.Shipping),
            testProjects = census.Projects.Count(project => !project.Shipping),
            census.ExcludedSource,
            census.ExcludedTests,
            contracts = facts,
        };
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report), cancellationToken).ConfigureAwait(false);
        foreach (var gap in verdict.NewContracts)
        {
            await Console.Error.WriteLineAsync("NEW backend gap: " + gap).ConfigureAwait(false);
        }

        foreach (var stale in verdict.StaleExceptions)
        {
            await Console.Error.WriteLineAsync("STALE backend debt: " + stale).ConfigureAwait(false);
        }

        return verdict.Passed ? 0 : 1;
    }

    private static BackendCoverageFact Measure(RepositoryCompilationCensus census, string root, IAssemblySymbol published, BackendContract pair)
    {
        var trusted = census.Assembly(root, pair.Project);
        var contract = trusted.GetTypeByMetadataName(pair.MetadataName)
            ?? throw new InvalidOperationException("Missing trusted interface " + pair.MetadataName);
        var kitName = "Excalibur.Testing.Conformance." + pair.Kit;
        _ = published.GetTypeByMetadataName(kitName) ?? throw new InvalidOperationException("Missing published kit " + kitName);
        var providers = new Dictionary<string, BackendProvider>(StringComparer.Ordinal);
        foreach (var project in census.Projects.Where(project => project.Shipping))
        {
            var visible = PublishedContracts.ResolveReference(project.Compilation, trusted)?.GetTypeByMetadataName(pair.MetadataName);
            if (visible is not null)
            {
                foreach (var type in PersistenceHealthCoverage.ProviderTypes(project.Compilation, visible))
                {
                    var provider = new BackendProvider(type);
                    providers.Add(provider.Identity, provider);
                }
            }
        }

        var bindings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in census.Projects.Where(project => !project.Shipping))
        {
            var kit = PublishedContracts.ResolveReference(project.Compilation, published)?.GetTypeByMetadataName(kitName);
            if (kit is not null)
            {
                bindings.UnionWith(BackendBindingRule.Bindings(project.Compilation, kit, providers.Values.ToArray()));
            }
        }

        if (bindings.Count == 0)
        {
            throw new InvalidOperationException("No suite binding evidence for " + pair.MetadataName);
        }

        var missing = PersistenceHealthCoverage.Evaluate(providers.Keys, bindings);
        return new BackendCoverageFact(trusted.Identity.Name + ":" + pair.MetadataName, contract.Name, kitName,
            providers.Keys.Order(StringComparer.Ordinal).ToArray(), bindings.Order(StringComparer.Ordinal).ToArray(), missing);
    }
}
