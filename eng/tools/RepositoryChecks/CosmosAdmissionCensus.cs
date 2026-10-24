// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Loads the selected test projects and their shared fixture sources for admission checks.</summary>
internal static class CosmosAdmissionCensus
{
    /// <summary>Preserves the repository-wide nonempty behavior population independently of shard selection.</summary>
    /// <exception cref="InvalidOperationException">The full test population exposes no emulator dependencies.</exception>
    internal static async Task<int> WriteInventoryAsync(RepositoryCompilationCensus census, string reportPath, CancellationToken cancellationToken)
    {
        var result = CosmosAdmissionRule.Analyze(census.Projects.Where(project => !project.Shipping).Select(project => project.Compilation));
        if (result.Fixtures == 0 || result.Tests.Length == 0)
        {
            throw new InvalidOperationException("Repository Cosmos admission inventory is empty.");
        }

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { fixtures = result.Fixtures, tests = result.Tests }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"EXAMINED: {result.Fixtures} Cosmos fixture owners; {result.Tests.Length} emulator-dependent test classes.")).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Writes compiler-derived qualified test names for comparison with adapter discovery.</summary>
    /// <exception cref="InvalidOperationException">The project population is empty or cannot be compiled.</exception>
    internal static async Task<int> WriteAsync(MSBuildWorkspace workspace, string roster, string reportPath, CancellationToken cancellationToken)
    {
        var paths = (await File.ReadAllLinesAsync(roster, cancellationToken).ConfigureAwait(false)).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
        if (paths.Length == 0)
        {
            throw new InvalidOperationException("No test projects for Cosmos admission census.");
        }

        var validated = new HashSet<ProjectId>();
        foreach (var path in paths)
        {
            var project = workspace.CurrentSolution.Projects.FirstOrDefault(project => string.Equals(project.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                ?? await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
            await CompilationValidation.ValidateProjectClosureAsync(project, cancellationToken, validated).ConfigureAwait(false);
        }

        await CompilationValidation.ValidateWorkspaceAsync(workspace).ConfigureAwait(false);
        var compilations = new List<Compilation>();
        foreach (var project in workspace.CurrentSolution.Projects.Where(project => project.FilePath?.Replace('\\', '/').Contains("/tests/", StringComparison.OrdinalIgnoreCase) == true))
        {
            compilations.Add(await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("No test compilation: " + project.FilePath));
        }

        var result = CosmosAdmissionRule.Analyze(compilations);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { projects = paths.Length, fixtures = result.Fixtures, tests = result.Tests }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"EXAMINED: {paths.Length} test projects; {result.Fixtures} Cosmos fixture owners; {result.Tests.Length} emulator-dependent test classes.")).ConfigureAwait(false);
        return 0;
    }
}
