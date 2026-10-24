// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text.Json;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Enforces nested-deadline, image-pinning and unconditional-skip policies over compiled test sources.</summary>
internal static class TestReliabilityCensus
{
    /// <summary>Reports complete denominators and refuses unresolved container arguments.</summary>
    /// <exception cref="InvalidOperationException">Source coverage, image population or debt configuration is incomplete.</exception>
    internal static async Task<int> WriteAsync(RepositoryCompilationCensus census, string root, string reportPath, CancellationToken cancellationToken)
    {
        await ValidateExcludedAsync(census, cancellationToken).ConfigureAwait(false);

        var files = new HashSet<string>(StringComparer.Ordinal);
        var deadlines = new HashSet<string>(StringComparer.Ordinal);
        var images = new HashSet<ContainerImageFact>();
        var skips = new HashSet<(string Path, int Line)>();
        foreach (var project in census.Projects.Where(project => !project.Shipping))
        {
            foreach (var tree in project.Compilation.SyntaxTrees)
            {
                var path = Path.GetRelativePath(root, tree.FilePath).Replace('\\', '/');
                if (!path.StartsWith("tests/", StringComparison.Ordinal) || path.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                files.Add(path);
                deadlines.UnionWith(NestedDeadlineRule.Find(project.Compilation, tree, path));
                images.UnionWith(ContainerImageRule.Find(project.Compilation, tree, path));
                skips.UnionWith(UnconditionalSkipRule.Find(project.Compilation, tree).Select(line => (path[6..], line)));
            }
        }

        if (files.Count == 0 || images.Count == 0)
        {
            throw new InvalidOperationException("Reliability policies require nonempty test files and resolved Testcontainers calls.");
        }

        var baseline = await ReadSkipBaselineAsync(root, cancellationToken).ConfigureAwait(false);

        var observed = skips.GroupBy(skip => skip.Path, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var growth = observed.Where(pair => pair.Value > baseline.GetValueOrDefault(pair.Key)).ToArray();
        var imageBaseline = (await File.ReadAllTextAsync(Path.Combine(root, "eng/ci/container-image-pinning-baseline.txt"), cancellationToken).ConfigureAwait(false)).Trim();
        if (!string.Equals(imageBaseline, "0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Container image debt must remain zero.");
        }

        var unsafeImages = images.Where(image => string.Equals(image.Classification, "floating", StringComparison.Ordinal) ||
            string.Equals(image.Classification, "unresolved", StringComparison.Ordinal)).ToArray();
        var passed = deadlines.Count == 0 && growth.Length == 0 && unsafeImages.Length == 0;
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            status = passed ? "passed" : "failed",
            files = files.Count,
            nestedDeadlines = deadlines.Order(StringComparer.Ordinal),
            images = images.OrderBy(image => image.Path, StringComparer.Ordinal).ThenBy(image => image.Line),
            unsafeImages,
            unconditionalSkips = observed,
            skipGrowth = growth,
        }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"EXAMINED: {files.Count} test files; {images.Count} image calls; {skips.Count} unconditional skips; {deadlines.Count} nested deadlines.").ConfigureAwait(false);
        return passed ? 0 : 1;
    }

    private static async Task ValidateExcludedAsync(RepositoryCompilationCensus census, CancellationToken cancellationToken)
    {
        foreach (var excluded in census.ExcludedTests)
        {
            if (CSharpConfigurations.Parse(await File.ReadAllTextAsync(excluded, cancellationToken).ConfigureAwait(false))
                .Any(tree => tree.GetRoot(cancellationToken).DescendantNodes().Any(node => node is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax)))
            {
                throw new InvalidOperationException("Unevaluated test calls cannot escape reliability coverage: " + excluded);
            }
        }
    }

    private static async Task<Dictionary<string, int>> ReadSkipBaselineAsync(string root, CancellationToken cancellationToken)
    {
        var baseline = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in await File.ReadAllLinesAsync(Path.Combine(root, "eng/ci/unconditional-skip-baseline.txt"), cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0 || !baseline.TryAdd(fields[1], count))
            {
                throw new InvalidOperationException("Invalid unconditional-skip baseline row: " + line);
            }
        }

        if (baseline.Values.Sum() > 0)
        {
            throw new InvalidOperationException("The unconditional-skip debt ceiling cannot grow.");
        }

        return baseline;
    }
}
