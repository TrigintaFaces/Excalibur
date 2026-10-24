// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Checks tenant mutation inventory and discovered behavioral test bindings.</summary>
internal static class TenantMutationCensus
{
	private static readonly string[] RangeNames = ["DeleteSnapshotsRequest", "DeleteSnapshotsOlderThanRequest", "EraseEventsRequest", "TombstoneArchivedEventsRequest", "PurgeCompletedSagasRequest"];
	private static readonly string[] KeyedNames = ["SaveSnapshotRequest", "SaveSagaRequest"];
	private static readonly string[] Counters =
	[
		"Excalibur.EventSourcing.SqlServer:Excalibur.EventSourcing.SqlServer.Requests.AllocateAndInsertEventsRequest",
		"Excalibur.EventSourcing.Postgres:Excalibur.EventSourcing.Postgres.Requests.AllocateAndInsertEventsRequest",
		"Excalibur.EventSourcing.Oracle:Excalibur.EventSourcing.Oracle.Requests.AllocateGlobalPositionsRequest",
	];

	private static readonly string[] Areas = new[] { "EventSourcing", "Saga" };

	private static readonly string[] Backends = ["SqlServer", "Postgres", "Oracle"];

	/// <summary>Requires complete compiler evidence before reporting inventory or test coverage.</summary>
	/// <exception cref="InvalidOperationException">The compiler census excluded executable source.</exception>
	internal static async Task<int> WriteAsync(MSBuildWorkspace workspace, string root, string reportPath, CancellationToken cancellationToken, RepositoryCompilationCensus? evaluated = null)
	{
		var census = evaluated ?? await RepositoryCompilationCensus.ReadAsync(workspace, root, cancellationToken).ConfigureAwait(false);
		foreach (var path in census.ExcludedSource.Concat(census.ExcludedTests))
		{
			var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
			if (ExcludedSourceContainsEvidence(text, path))
			{
				throw new InvalidOperationException("Unevaluated mutation or test source: " + path);
			}
		}

		var tenant = census.Assembly(root, "src/Dispatch/Excalibur.Dispatch.Abstractions/Excalibur.Dispatch.Abstractions.csproj");
		var mutations = census.Projects.Where(project => project.Shipping)
			.SelectMany(project => TenantMutationRule.Find(project.Compilation, tenant, root))
			.DistinctBy(mutation => mutation.Provider.Identity).ToArray();
		var requests = mutations.Select(mutation => mutation.Provider).ToArray();
		var bindings = census.Projects.Where(project => !project.Shipping)
			.SelectMany(project => TenantMutationRule.Bindings(project.Compilation, requests)).Distinct(StringComparer.Ordinal).ToArray();
		var failures = Evaluate(mutations, bindings);
		await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
		{
			status = failures.Length == 0 ? "passed" : "failed",
			sourceProjects = census.Projects.Count(project => project.Shipping),
			testProjects = census.Projects.Count(project => !project.Shipping),
			mutations = mutations.Select(mutation => new { identity = mutation.Provider.Identity, mutation.Path, mutation.Scoped }),
			discoveredTestBindings = bindings.Order(StringComparer.Ordinal),
			failures,
		}), cancellationToken).ConfigureAwait(false);
		foreach (var failure in failures)
		{
			await Console.Error.WriteLineAsync(failure).ConfigureAwait(false);
		}

		return failures.Length == 0 ? 0 : 1;
	}

	/// <summary>Rejects excluded calls or SQL-bearing declarations instead of silently dropping them.</summary>
	internal static bool ExcludedSourceContainsEvidence(string text, string path) => CSharpConfigurations.Parse(text)
		.Any(tree => tree.GetRoot().DescendantNodes().Any(node => node is
			Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax or
			Microsoft.CodeAnalysis.CSharp.Syntax.BaseObjectCreationExpressionSyntax or
			Microsoft.CodeAnalysis.CSharp.Syntax.InterpolatedStringExpressionSyntax ||
			(node is Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax literal && literal.Token.Value is string) ||
			(node is Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax && path.Replace('\\', '/').Contains("/Requests/", StringComparison.Ordinal))));

	/// <summary>Preserves curation, scope backstops, exact counter exceptions, and nonvacuity.</summary>
	internal static string[] Evaluate(IReadOnlyList<TenantMutation> mutations, IReadOnlyCollection<string> bindings)
	{
		var failures = new List<string>();
		foreach (var name in RangeNames.Concat(KeyedNames))
		{
			if (!mutations.Any(mutation => string.Equals(mutation.Provider.Type.Name, name, StringComparison.Ordinal)))
			{
				failures.Add("Missing curated mutation: " + name);
			}
		}

		foreach (var mutation in mutations)
		{
			var provider = mutation.Provider;
			if (Counters.Contains(provider.Identity, StringComparer.Ordinal))
			{
				continue;
			}

			var tenantStore = Areas.Any(area => Backends
				.Any(backend => mutation.Path.Contains("/Excalibur." + area + "." + backend + "/", StringComparison.Ordinal)));
			if (tenantStore && !mutation.Scoped)
			{
				failures.Add("Missing tenant seam: " + provider.Identity);
			}

			if (!mutation.Scoped)
			{
				continue;
			}

			if (RangeNames.Contains(provider.Type.Name, StringComparer.Ordinal))
			{
				if (!bindings.Contains(provider.Identity, StringComparer.Ordinal))
				{
					failures.Add("Missing behavioral test binding: " + provider.Identity);
				}
			}
			else if (!KeyedNames.Contains(provider.Type.Name, StringComparer.Ordinal))
			{
				failures.Add("Uncurated tenant mutation: " + provider.Identity);
			}
		}

		return failures.Order(StringComparer.Ordinal).ToArray();
	}
}
