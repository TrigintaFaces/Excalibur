// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Inventories resolved skip metadata and guards the runtime capability-validation wiring.</summary>
internal static class CapabilityCensus
{
    /// <summary>Requires measured skip sites, checked lookups and real runnable suites for every skip-bearing kit.</summary>
    /// <exception cref="InvalidOperationException">Published symbols or nonempty policy populations cannot be resolved.</exception>
    internal static async Task<int> WriteAsync(RepositoryCompilationCensus census, string root, string reportPath, CancellationToken cancellationToken)
    {
        var published = census.Assembly(root, "src/Excalibur/Excalibur.Testing.Conformance/Excalibur.Testing.Conformance.csproj");
        var project = census.Projects.Single(item => item.Shipping && SymbolEqualityComparer.Default.Equals(item.Compilation.Assembly, published));
        var compilation = project.Compilation;
        var (sites, lookups, failures) = Measure(compilation, cancellationToken);

        var suites = census.Projects.Where(item => !item.Shipping).SelectMany(item =>
            ConformanceArmWiring.Analyze(item.Compilation, PublishedContracts.ResolveReference(item.Compilation, compilation.Assembly)))
            .Where(suite => !suite.IsHelper && suite.EntryPoints > 0).ToArray();
        foreach (var kit in sites.Select(site => site.Kit).Distinct(StringComparer.Ordinal))
        {
            if (!suites.Any(suite => string.Equals(suite.Kit, kit, StringComparison.Ordinal)))
            {
                failures.Add("Skip-bearing kit has no runnable suite: " + kit);
            }
        }

        if (sites.Count == 0 || lookups == 0 || suites.Length == 0)
        {
            throw new InvalidOperationException("Capability policy requires nonempty skip, lookup and runnable-suite populations.");
        }

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            status = failures.Count == 0 ? "passed" : "failed",
            evidence = "Runtime enforcement and static wiring; execution is verified by the conformance test jobs.",
            sites = sites.Select(site => new { site.Kit, site.Arm, site.Capability }),
            checkedLookups = lookups,
            runnableSuites = suites.Length,
            failures,
        }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"EXAMINED: {sites.Count} skip sites; {lookups} capability lookups; {failures.Count} wiring failures.")).ConfigureAwait(false);
        return failures.Count == 0 ? 0 : 1;
    }

    private static (List<(string Kit, string Arm, string? Capability)> Sites, int Lookups, List<string> Failures) Measure(Compilation compilation, CancellationToken cancellationToken)
    {
        var kitBase = compilation.GetTypeByMetadataName("Excalibur.Testing.Conformance.ConformanceTestKit")
            ?? throw new InvalidOperationException("Published conformance base is missing.");
        var skip = kitBase.GetMembers("SkipArm").OfType<IMethodSymbol>().Single();
        var validator = kitBase.GetMembers("ValidateCapability").OfType<IMethodSymbol>().Single();
        var failures = new List<string>();
        var sites = new List<(string Kit, string Arm, string? Capability)>();
        var lookups = 0;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot(cancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetOperation(node, cancellationToken) is not IInvocationOperation call)
                {
                    continue;
                }

                var owner = model.GetEnclosingSymbol(node.SpanStart, cancellationToken)?.ContainingType;
                if (owner is null)
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(call.TargetMethod.OriginalDefinition, skip))
                {
                    var arm = call.Arguments.Single(item => item.Parameter?.Ordinal == 0).Value;
                    var capability = call.Arguments.Single(item => item.Parameter?.Ordinal == 1).Value;
                    var type = capability as ITypeOfOperation;
                    if (type is null && capability.ConstantValue is not { HasValue: true, Value: null })
                    {
                        failures.Add("Unresolved skip capability: " + node.ToFullString());
                    }

                    sites.Add((owner.ContainingAssembly.Identity.Name + ":" + owner.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), arm.ConstantValue is { HasValue: true, Value: string name } ? name : "dynamic:" + arm.Syntax.ToFullString(), type?.TypeOperand.ToDisplayString()));
                }

                if (!string.Equals(owner.Name, "OutboxStoreConformanceTestKit", StringComparison.Ordinal) &&
                    !string.Equals(owner.Name, "DeadLetterStoreConformanceTestKit", StringComparison.Ordinal))
                {
                    continue;
                }

                var resolver = string.Equals(call.TargetMethod.Name, "ResolveAdminFacet", StringComparison.Ordinal);
                if (!resolver && !string.Equals(call.TargetMethod.Name, "GetService", StringComparison.Ordinal))
                {
                    continue;
                }

                lookups++;
                if (!CapabilityLookupRule.IsChecked(call, validator, resolver))
                {
                    failures.Add("Unchecked capability lookup: " + owner.ToDisplayString() + ": " + node.ToFullString());
                }
            }
        }

        return (sites, lookups, failures);
    }
}
