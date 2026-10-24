// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Collects conformance facts across every evaluated project in the test tree.</summary>
internal static class ConformanceCensus
{
    /// <summary>Writes a complete source-project census, refusing incomplete compilation evidence.</summary>
    /// <exception cref="InvalidOperationException">The project or compilation population is incomplete.</exception>
    internal static async Task<int> WriteAsync(MSBuildWorkspace workspace, string root, string reportPath, CancellationToken cancellationToken)
    {
        var paths = ProjectPaths(root);
        if (paths.Length == 0)
        {
            throw new InvalidOperationException("The conformance census requires a nonempty test-project population.");
        }

        var validated = new HashSet<ProjectId>();
        var evidence = new ConformanceEvidence();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var path in paths)
        {
            var project = workspace.CurrentSolution.Projects.SingleOrDefault(candidate => candidate.FilePath is not null &&
                string.Equals(Path.GetFullPath(candidate.FilePath), Path.GetFullPath(path), comparison))
                ?? await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (workspace.CurrentSolution.Projects.Where(candidate => candidate.FilePath is not null &&
                string.Equals(Path.GetFullPath(candidate.FilePath), Path.GetFullPath(path), comparison)).Take(2).Count() != 1)
            {
                throw new InvalidOperationException($"Multiple evaluated variants require explicit configuration coverage: {path}");
            }

            await CompilationValidation.ValidateProjectClosureAsync(project, cancellationToken, validated).ConfigureAwait(false);
            await CompilationValidation.ValidateWorkspaceAsync(workspace).ConfigureAwait(false);
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"No compilation for {path}.");
            var published = await PublishedContracts.FindAsync(project, root, cancellationToken).ConfigureAwait(false);
            var found = evidence.Add(compilation, published, Path.Combine(root, "tests"));
            await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Examined {Path.GetRelativePath(root, path)}: {found} private contract bindings.")).ConfigureAwait(false);
        }

        if (evidence.Sources == 0 || evidence.ConcreteTypes == 0 || evidence.PublishedDerivations == 0)
        {
            throw new InvalidOperationException("The conformance census requires sources, concrete types, and resolved published-kit derivations.");
        }

        var unowned = SourceOwnership.Verify(root, evidence.SourcePaths, evidence.EvaluatedInheritance);
        var baselinePath = Path.Combine(root, "eng", "ci", "conformance-fork-baseline.txt");
        var baseline = await File.ReadAllLinesAsync(baselinePath, cancellationToken).ConfigureAwait(false);
        var verdict = ConformanceCensusVerdict.Evaluate(evidence, baseline);
        await ReportWriter.WriteCensusAsync(root, reportPath, paths, evidence, unowned, verdict, cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"{(verdict.Passed ? "PASS" : "FAIL")}: {paths.Length} projects, {evidence.ConcreteTypes} concrete types, {evidence.PublishedDerivations} published bindings, {verdict.Contracts.NewContracts.Length} new contracts, {verdict.Contracts.StaleExceptions.Length} stale exceptions, {verdict.ArmFailures.Length} arm failures, {verdict.Guards.Failures.Length} guard failures.")).ConfigureAwait(false);
        foreach (var identity in verdict.Contracts.NewContracts)
        {
            await Console.Error.WriteLineAsync($"NEW private contract: {identity}").ConfigureAwait(false);
        }

        foreach (var identity in verdict.Contracts.StaleExceptions)
        {
            await Console.Error.WriteLineAsync($"STALE private contract exception: {identity}").ConfigureAwait(false);
        }

        foreach (var failure in verdict.ArmFailures.Concat(verdict.Guards.Failures))
        {
            await Console.Error.WriteLineAsync(failure).ConfigureAwait(false);
        }

        return verdict.Passed ? 0 : 1;
    }

    private static string[] ProjectPaths(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment =>
                string.Equals(segment, "bin", StringComparison.Ordinal) || string.Equals(segment, "obj", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal).ToArray();
}
