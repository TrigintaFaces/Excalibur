// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Inventories shipping providers independently from the projects containing their tests.</summary>
internal static class PersistenceHealthCensus
{
    /// <summary>Checks every source project and test project, refusing incomplete compiler evidence.</summary>
    /// <exception cref="InvalidOperationException">A required source contract or project population is missing.</exception>
    internal static async Task<int> WriteAsync(MSBuildWorkspace workspace, string root, string reportPath, CancellationToken cancellationToken, RepositoryCompilationCensus? evaluated = null)
    {
        var census = evaluated ?? await RepositoryCompilationCensus.ReadAsync(workspace, root, cancellationToken).ConfigureAwait(false);
        var sourcePaths = census.Projects.Where(project => project.Shipping).Select(project => project.Path).ToArray();
        var testPaths = census.Projects.Where(project => !project.Shipping).Select(project => project.Path).ToArray();
        var contractAssembly = census.Assembly(root, "src/Excalibur/Excalibur.Data.Abstractions/Excalibur.Data.Abstractions.csproj");
        var kitAssembly = census.Assembly(root, "src/Excalibur/Excalibur.Testing.Conformance/Excalibur.Testing.Conformance.csproj");
        var providers = new Dictionary<string, BackendProvider>(StringComparer.Ordinal);
        foreach (var project in census.Projects.Where(project => project.Shipping))
        {
            var contract = PublishedContracts.ResolveReference(project.Compilation, contractAssembly)?.GetTypeByMetadataName("Excalibur.Data.Persistence.IPersistenceProviderHealth");
            if (contract is not null)
            {
                foreach (var type in PersistenceHealthCoverage.ProviderTypes(project.Compilation, contract))
                {
                    var provider = new BackendProvider(type);
                    providers.Add(provider.Identity, provider);
                }
            }
        }

        var bindings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in census.Projects.Where(project => !project.Shipping))
        {
            var kit = PublishedContracts.ResolveReference(project.Compilation, kitAssembly)?.GetTypeByMetadataName("Excalibur.Testing.Conformance.PersistenceProviderConformanceTestKit");
            if (kit is not null)
            {
                bindings.UnionWith(PersistenceHealthCoverage.Bindings(project.Compilation, kit, providers.Values.ToArray()));
            }
        }

        var missing = PersistenceHealthCoverage.Evaluate(providers.Keys, bindings);
        var report = new
        {
            configuration = "Release",
            status = missing.Length == 0 ? "passed" : "failed",
            sourceProjects = sourcePaths.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray(),
            testProjects = testPaths.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray(),
            providers = providers.Keys.Order(StringComparer.Ordinal).ToArray(),
            factoryConstructionBindings = bindings.Order(StringComparer.Ordinal).ToArray(),
            excludedSourceWithoutInheritance = census.ExcludedSource,
            excludedTestsWithoutInheritance = census.ExcludedTests,
            missing,
        };
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report), cancellationToken).ConfigureAwait(false);
        foreach (var provider in missing)
        {
            await Console.Error.WriteLineAsync("UNBOUND persistence health provider: " + provider).ConfigureAwait(false);
        }

        return missing.Length == 0 ? 0 : 1;
    }
}
