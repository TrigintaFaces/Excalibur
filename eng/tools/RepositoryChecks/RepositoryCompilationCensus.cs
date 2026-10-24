// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Builds complete, independently enumerated source and test populations for provider policies.</summary>
internal sealed record RepositoryCompilationCensus(RepositoryCompilation[] Projects, string[] ExcludedSource, string[] ExcludedTests)
{
    /// <summary>Loads source projects independently of test references and reconciles excluded and conditional source.</summary>
    /// <exception cref="InvalidOperationException">Compiler evidence or project populations are incomplete.</exception>
    internal static async Task<RepositoryCompilationCensus> ReadAsync(MSBuildWorkspace workspace, string root, CancellationToken cancellationToken)
    {
        var projects = new List<RepositoryCompilation>();
        var validated = new HashSet<ProjectId>();
        var sourceCoverage = new CompilationSourceCoverage();
        var testCoverage = new CompilationSourceCoverage();
        foreach (var shipping in new[] { true, false })
        {
            foreach (var path in Paths(root, shipping ? "src" : "tests"))
            {
                var project = await OpenAsync(workspace, path, cancellationToken).ConfigureAwait(false);
                await CompilationValidation.ValidateProjectClosureAsync(project, cancellationToken, validated).ConfigureAwait(false);
                await CompilationValidation.ValidateWorkspaceAsync(workspace).ConfigureAwait(false);
                var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("No compilation for " + path);
                (shipping ? sourceCoverage : testCoverage).Add(compilation);
                projects.Add(new RepositoryCompilation(path, shipping, compilation));
                await Console.Out.WriteLineAsync("Examined provider coverage: " + Path.GetRelativePath(root, path)).ConfigureAwait(false);
            }
        }

        return new RepositoryCompilationCensus(projects.ToArray(), sourceCoverage.Verify(root, "src"), testCoverage.Verify(root, "tests"));
    }

    /// <summary>Anchors a trusted assembly to its independently inventoried source project.</summary>
    internal IAssemblySymbol Assembly(string root, string projectPath)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, projectPath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Projects.Single(project => string.Equals(System.IO.Path.GetFullPath(project.Path), path, comparison)).Compilation.Assembly;
    }

    private static string[] Paths(string root, string directory)
    {
        var paths = Directory.EnumerateFiles(System.IO.Path.Combine(root, directory), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).Any(segment =>
                string.Equals(segment, "bin", StringComparison.Ordinal) || string.Equals(segment, "obj", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal).ToArray();
        return paths.Length > 0 ? paths : throw new InvalidOperationException("No project population under " + directory);
    }

    private static Task<Project> OpenAsync(MSBuildWorkspace workspace, string path, CancellationToken cancellationToken)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var existing = workspace.CurrentSolution.Projects.SingleOrDefault(project => project.FilePath is not null &&
            string.Equals(System.IO.Path.GetFullPath(project.FilePath), System.IO.Path.GetFullPath(path), comparison));
        return existing is null ? workspace.OpenProjectAsync(path, cancellationToken: cancellationToken) : Task.FromResult(existing);
    }
}
