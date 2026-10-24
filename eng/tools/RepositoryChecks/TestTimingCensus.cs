// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Enforces deterministic test timing against an exact, shrink-only debt ledger.</summary>
internal static class TestTimingCensus
{
	/// <summary>Reports every live timing hit and both sides of the existing debt comparison.</summary>
	/// <exception cref="InvalidOperationException">Excluded test syntax may contain an unevaluated call.</exception>
	internal static async Task<int> WriteAsync(MSBuildWorkspace workspace, string root, string reportPath, CancellationToken cancellationToken, RepositoryCompilationCensus? evaluated = null)
	{
		var census = evaluated ?? await RepositoryCompilationCensus.ReadAsync(workspace, root, cancellationToken).ConfigureAwait(false);
		foreach (var path in census.ExcludedTests)
		{
			var source = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
			if (CSharpConfigurations.Parse(source).Any(tree => tree.GetRoot(cancellationToken).DescendantNodes().Any(node =>
				node is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax)))
			{
				throw new InvalidOperationException("Unevaluated test calls cannot escape timing coverage: " + path);
			}
		}

		var hits = new HashSet<string>(StringComparer.Ordinal);
		var files = new HashSet<string>(StringComparer.Ordinal);
		foreach (var project in census.Projects.Where(project => !project.Shipping))
		{
			foreach (var tree in project.Compilation.SyntaxTrees)
			{
				var path = Path.GetRelativePath(root, tree.FilePath).Replace('\\', '/');
				if (path.StartsWith("tests/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
				{
					files.Add(path);
					hits.UnionWith(TestTimingRule.Find(project.Compilation, tree, path));
				}
			}
		}

		var baseline = await File.ReadAllLinesAsync(Path.Combine(root, "eng/ci/task-delay-syncwait.baseline.txt"), cancellationToken).ConfigureAwait(false);
		if (files.Count == 0)
		{
			throw new InvalidOperationException("No test source files were examined.");
		}

		var verdict = CoverageLedger.Evaluate(hits, baseline, 155);
		var passed = verdict.NewContracts.Length == 0 && verdict.StaleExceptions.Length == 0;
		await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
		{
			status = passed ? "passed-with-existing-debt" : "failed",
			files = files.Count,
			hits = hits.Order(StringComparer.Ordinal).ToArray(),
			verdict,
		}), cancellationToken).ConfigureAwait(false);
		await Console.Out.WriteLineAsync($"EXAMINED: {files.Count} test files; {hits.Count} timing debt entries.").ConfigureAwait(false);
		foreach (var violation in verdict.NewContracts.Concat(verdict.StaleExceptions))
		{
			await Console.Error.WriteLineAsync(violation).ConfigureAwait(false);
		}

		return passed ? 0 : 1;
	}
}
