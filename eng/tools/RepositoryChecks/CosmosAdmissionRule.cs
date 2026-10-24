// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Finds emulator-dependent test types using resolved source dependencies and xUnit collections.</summary>
internal static class CosmosAdmissionRule
{
    /// <summary>Returns qualified test names independently of filenames and test traits.</summary>
    internal static (int Fixtures, string[] Tests) Analyze(IEnumerable<Compilation> compilations)
    {
        var types = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var seeds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var compilation in compilations)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type)
                    {
                        continue;
                    }

                    var key = Key(type);
                    types[key] = type;
                    if (!edges.TryGetValue(key, out var references))
                    {
                        edges[key] = references = new HashSet<string>(StringComparer.Ordinal);
                    }

                    foreach (var name in declaration.DescendantNodes(node => ReferenceEquals(node, declaration) || node is not TypeDeclarationSyntax).OfType<SimpleNameSyntax>())
                    {
                        var symbol = model.GetSymbolInfo(name).Symbol;
                        var dependency = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
                        if (dependency is not null)
                        {
                            references.Add(Key(dependency));
                            if (IsContainer(dependency))
                            {
                                seeds.Add(key);
                            }
                        }
                    }
                }
            }
        }

        BindCollections(types, edges);
        var affected = new HashSet<string>(seeds, StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var (owner, references) in edges)
            {
                if (references.Overlaps(affected) && affected.Add(owner))
                {
                    changed = true;
                }
            }
        }
        while (changed);

        return (seeds.Count, affected.Where(key => IsTest(types[key])).Select(key => types[key].ToDisplayString()).Order(StringComparer.Ordinal).ToArray());
    }

    private static string Key(INamedTypeSymbol type) => type.ContainingAssembly.Identity.Name + ":" + type.OriginalDefinition.ToDisplayString();

    private static bool IsContainer(INamedTypeSymbol type) =>
        string.Equals(type.ContainingAssembly.Identity.Name, "Testcontainers.CosmosDb", StringComparison.Ordinal) &&
        string.Equals(type.ContainingNamespace.ToDisplayString(), "Testcontainers.CosmosDb", StringComparison.Ordinal) &&
        (string.Equals(type.Name, "CosmosDbBuilder", StringComparison.Ordinal) || string.Equals(type.Name, "CosmosDbContainer", StringComparison.Ordinal));

    private static bool IsTest(INamedTypeSymbol type) => !type.IsAbstract && !type.IsGenericType &&
        ConformanceArmWiring.EffectiveMethods(type).Any(ConformanceArmWiring.IsTest);

    private static void BindCollections(Dictionary<string, INamedTypeSymbol> types, Dictionary<string, HashSet<string>> edges)
    {
        var definitions = types.Where(pair => CollectionName(pair.Value, "Xunit.CollectionDefinitionAttribute") is not null)
            .GroupBy(pair => pair.Value.ContainingAssembly.Identity.Name + ":" + CollectionName(pair.Value, "Xunit.CollectionDefinitionAttribute"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).ToArray(), StringComparer.Ordinal);
        foreach (var (key, type) in types)
        {
            if (CollectionName(type, "Xunit.CollectionAttribute") is { } name && definitions.TryGetValue(type.ContainingAssembly.Identity.Name + ":" + name, out var owners))
            {
                edges[key].UnionWith(owners);
            }
        }
    }

    private static string? CollectionName(INamedTypeSymbol type, string attributeName) =>
        ConformanceArmWiring.Ancestors(type, includeSelf: true).SelectMany(parent => parent.GetAttributes())
            .FirstOrDefault(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), attributeName, StringComparison.Ordinal))?
            .ConstructorArguments.FirstOrDefault().Value as string;
}
