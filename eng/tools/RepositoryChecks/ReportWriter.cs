// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text.Json;

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Writes semantic facts without interpreting them as a repository-wide policy verdict.</summary>
internal static class ReportWriter
{
    /// <summary>Writes aggregate facts; policy acceptance is a separate operation.</summary>
    internal static Task WriteCensusAsync(string root, string reportPath, string[] paths,
        ConformanceEvidence evidence, string[] unowned, ConformanceCensusVerdict verdict, CancellationToken cancellationToken)
    {
        var report = new
        {
            configuration = "Release",
            status = verdict.Passed ? "passed" : "failed",
            verdict.Contracts.NewContracts,
            verdict.Contracts.StaleExceptions,
            projects = paths.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray(),
            sources = evidence.Sources,
            sourcePaths = evidence.SourcePaths.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray(),
            excludedWithoutInheritance = unowned.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray(),
            concreteTypes = evidence.ConcreteTypes,
            publishedDerivations = evidence.PublishedDerivations,
            armSuites = evidence.ArmSuites.OrderBy(suite => suite.Identity, StringComparer.Ordinal).ToArray(),
            armWiringFailures = verdict.ArmFailures,
            conformanceGuards = verdict.Guards,
            publishedKits = evidence.PublishedKits.Order(StringComparer.Ordinal).ToArray(),
            forks = evidence.Forks.OrderBy(fork => fork.BaseIdentity, StringComparer.Ordinal).ThenBy(fork => fork.SuiteIdentity, StringComparer.Ordinal).ToArray(),
        };
        return File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report), cancellationToken);
    }

    /// <summary>Emits facts for one evaluated project.</summary>
    internal static async Task WriteAsync(string root, Project project, string reportPath, string expectedSdk,
        Compilation compilation, IAssemblySymbol? publishedAssembly, CancellationToken cancellationToken)
    {
        var cosmos = CosmosFixtureRule.Analyze(compilation, cancellationToken);
        var facts = ConformanceForkRule.Analyze(compilation, publishedAssembly, Path.Combine(root, "tests"));
        var report = new
        {
            project = Path.GetRelativePath(root, project.FilePath!).Replace('\\', '/'),
            sdk = expectedSdk,
            configuration = "Release",
            sources = compilation.SyntaxTrees.Count(),
            types = ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace).Count(),
            forks = facts,
            cosmos = new { cosmos.Fixtures, cosmos.Clients, Violations = cosmos.Violations.Select(item => item.Location.GetLineSpan() + ": " + item.Message).ToArray() },
        };
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Examined {report.project}: {report.sources} syntax trees, {report.types} types, {facts.Count} private contract bindings.")).ConfigureAwait(false);
    }
}
