// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Compares independently inventoried providers with resolved suite factory construction references.</summary>
/// <remarks>Static construction references are not proof of runtime capability assertions.</remarks>
internal static class PersistenceHealthCoverage
{
    /// <summary>Enumerates concrete implementations, including inherited interface implementations.</summary>
    internal static string[] Providers(Compilation compilation, INamedTypeSymbol contract) =>
        ProviderTypes(compilation, contract).Select(Identity).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Retains defining source symbols for binding checks across consumer compilations.</summary>
    internal static IEnumerable<INamedTypeSymbol> ProviderTypes(Compilation compilation, INamedTypeSymbol contract) =>
        ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace)
            .Where(type => !type.IsAbstract && type.TypeKind is TypeKind.Class or TypeKind.Struct &&
                type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default));

    /// <summary>Resolves constructions in the effective override of the kit's provider factory.</summary>
    internal static string[] Bindings(Compilation compilation, INamedTypeSymbol kit, IReadOnlyList<BackendProvider> providers)
    {
        var factories = kit.GetMembers("CreateProvider").OfType<IMethodSymbol>().Where(method => method.IsAbstract).ToArray();
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suite in ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace).Where(type => !type.IsAbstract &&
            ConformanceArmWiring.Ancestors(type).Contains(kit, SymbolEqualityComparer.Default)))
        {
            foreach (var factory in factories)
            {
                var effective = ConformanceArmWiring.Ancestors(suite, includeSelf: true)
                    .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>()).FirstOrDefault(method => Overrides(method, factory));
                if (effective is null)
                {
                    continue;
                }

                foreach (var declaration in effective.DeclaringSyntaxReferences)
                {
                    var syntax = declaration.GetSyntax();
                    var model = ConformanceArmWiring.ModelFor(compilation, syntax.SyntaxTree);
                    foreach (var node in syntax.DescendantNodes())
                    {
                        if (model.GetOperation(node) is IObjectCreationOperation { Type: INamedTypeSymbol created })
                        {
                            foreach (var provider in providers.Where(provider => provider.Matches(created, compilation, model.Compilation)))
                            {
                                bound.Add(provider.Identity);
                            }
                        }
                    }
                }
            }
        }

        return bound.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Refuses an empty inventory and returns every unbound provider without exemptions.</summary>
    /// <exception cref="InvalidOperationException">No shipping provider was inventoried.</exception>
    internal static string[] Evaluate(IEnumerable<string> providers, IEnumerable<string> bindings)
    {
        var measured = providers.ToHashSet(StringComparer.Ordinal);
        if (measured.Count == 0)
        {
            throw new InvalidOperationException("No shipping persistence health providers were measured.");
        }

        return measured.Except(bindings, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool Overrides(IMethodSymbol method, IMethodSymbol factory)
    {
        for (var parent = method.OverriddenMethod; parent is not null; parent = parent.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(parent.OriginalDefinition, factory.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static string Identity(INamedTypeSymbol type) => type.ContainingAssembly.Identity.Name + ":" +
        type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
}
