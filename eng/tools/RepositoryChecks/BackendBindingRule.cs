// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Resolves static backend references and unambiguous registration closures for a trusted kit.</summary>
/// <remarks>This is static binding evidence, not proof that a suite executes against a constructed instance.</remarks>
internal static class BackendBindingRule
{
    /// <summary>Collects backend bindings without borrowing names from another provider's registration.</summary>
    internal static string[] Bindings(Compilation compilation, INamedTypeSymbol kit, IReadOnlyList<BackendProvider> providers)
    {
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suite in ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace).Where(type => !type.IsAbstract &&
            ConformanceArmWiring.Ancestors(type).Contains(kit, SymbolEqualityComparer.Default)))
        {
            foreach (var type in ConformanceArmWiring.Ancestors(suite, includeSelf: true).Where(type =>
                !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, kit.ContainingAssembly)))
            {
                foreach (var declaration in type.DeclaringSyntaxReferences)
                {
                    if (declaration.GetSyntax() is not TypeDeclarationSyntax syntax)
                    {
                        continue;
                    }

                    var model = ConformanceArmWiring.ModelFor(compilation, syntax.SyntaxTree);
                    foreach (var member in syntax.Members.Where(member => member is not BaseTypeDeclarationSyntax))
                    {
                        foreach (var node in member.DescendantNodesAndSelf())
                        {
                            var operation = model.GetOperation(node);
                            AddDirect(operation, compilation, model.Compilation, providers, bound, TypeSubstitutions(type));
                            if (operation is IInvocationOperation invocation && IsConcreteCall(invocation.TargetMethod) &&
                                TrustedAssembly(model.Compilation, invocation.TargetMethod.ContainingAssembly, providers) is not null)
                            {
                                var candidates = RegistrationClosure(compilation, model.Compilation, invocation.TargetMethod, providers);
                                if (candidates.Count == 1)
                                {
                                    bound.UnionWith(candidates);
                                }
                            }
                        }
                    }
                }
            }
        }

        return bound.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Collects the complete same-assembly closure before applying the uniqueness requirement.</summary>
    /// <exception cref="InvalidOperationException">The registration is not defined by a trusted provider source assembly.</exception>
    internal static HashSet<string> RegistrationClosure(Compilation compilation, Compilation owner, IMethodSymbol entryPoint, IReadOnlyList<BackendProvider> providers)
    {
        var trustedAssembly = TrustedAssembly(owner, entryPoint.ContainingAssembly, providers)
            ?? throw new InvalidOperationException("Registration has no trusted provider source assembly.");
        var ownProviders = providers.Where(provider => SymbolEqualityComparer.Default.Equals(provider.Type.ContainingAssembly, trustedAssembly)).ToArray();
        var found = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<(IMethodSymbol Method, Dictionary<string, ITypeSymbol> Substitutions)>();
        queue.Enqueue((entryPoint, Substitutions(entryPoint, new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal))));
        while (queue.TryDequeue(out var item))
        {
            var visit = item.Method.OriginalDefinition.ToDisplayString() + "|" + string.Join('|', item.Substitutions.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value.ToDisplayString()));
            if (!visited.Add(visit))
            {
                continue;
            }

            foreach (var declaration in item.Method.DeclaringSyntaxReferences)
            {
                var syntax = declaration.GetSyntax();
                var model = ConformanceArmWiring.ModelFor(compilation, syntax.SyntaxTree);
                foreach (var node in syntax.DescendantNodesAndSelf())
                {
                    var operation = model.GetOperation(node);
                    AddDirect(operation, compilation, model.Compilation, ownProviders, found, item.Substitutions);
                    if (operation is IInvocationOperation invocation && IsConcreteCall(invocation.TargetMethod) &&
                        SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingAssembly, PublishedContracts.ResolveReference(model.Compilation, trustedAssembly)))
                    {
                        queue.Enqueue((invocation.TargetMethod, Substitutions(invocation.TargetMethod, item.Substitutions)));
                    }
                }
            }
        }

        return found;
    }

    private static bool IsConcreteCall(IMethodSymbol method) => method.IsStatic ||
        (method.ContainingType.TypeKind == TypeKind.Class && !method.IsAbstract && !method.IsVirtual && !method.IsOverride);

    private static void AddDirect(IOperation? operation, Compilation root, Compilation owner, IReadOnlyList<BackendProvider> providers, HashSet<string> found, IReadOnlyDictionary<string, ITypeSymbol> substitutions)
    {
        switch (operation)
        {
            case IObjectCreationOperation creation:
                AddType(creation.Type, root, owner, providers, found, substitutions);
                break;
            case ITypeParameterObjectCreationOperation creation:
                AddType(creation.Type, root, owner, providers, found, substitutions);
                break;
            case ITypeOfOperation typeOf:
                AddType(typeOf.TypeOperand, root, owner, providers, found, substitutions);
                break;
            case IInvocationOperation invocation when string.Equals(invocation.TargetMethod.ContainingNamespace.ToDisplayString(), "Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal):
                foreach (var argument in invocation.TargetMethod.TypeArguments)
                {
                    AddType(argument, root, owner, providers, found, substitutions);
                }

                break;
        }
    }

    private static void AddType(ITypeSymbol? type, Compilation root, Compilation owner, IReadOnlyList<BackendProvider> providers, HashSet<string> found, IReadOnlyDictionary<string, ITypeSymbol> substitutions)
    {
        var resolved = Resolve(type, substitutions);
        if (resolved is INamedTypeSymbol named)
        {
            foreach (var provider in providers)
            {
                if (provider.Matches(named, root, owner))
                {
                    found.Add(provider.Identity);
                }
            }
        }
    }

    private static Dictionary<string, ITypeSymbol> Substitutions(IMethodSymbol method, IReadOnlyDictionary<string, ITypeSymbol> parent)
    {
        var result = TypeSubstitutions(method.ContainingType, parent);
        for (var index = 0; index < method.TypeParameters.Length; index++)
        {
            result[ParameterKey(method.OriginalDefinition.TypeParameters[index])] = Resolve(method.TypeArguments[index], parent)!;
        }

        return result;
    }

    private static Dictionary<string, ITypeSymbol> TypeSubstitutions(INamedTypeSymbol type, IReadOnlyDictionary<string, ITypeSymbol>? parent = null)
    {
        var result = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.ContainingType)
        {
            for (var index = 0; index < current.TypeParameters.Length; index++)
            {
                var argument = current.TypeArguments[index];
                result[ParameterKey(current.OriginalDefinition.TypeParameters[index])] = parent is null ? argument : Resolve(argument, parent)!;
            }
        }

        return result;
    }

    private static IAssemblySymbol? TrustedAssembly(Compilation owner, IAssemblySymbol apparent, IEnumerable<BackendProvider> providers) =>
        providers.Select(provider => provider.Type.ContainingAssembly).FirstOrDefault(trusted =>
            SymbolEqualityComparer.Default.Equals(apparent, PublishedContracts.ResolveReference(owner, trusted)));

    private static ITypeSymbol? Resolve(ITypeSymbol? type, IReadOnlyDictionary<string, ITypeSymbol> substitutions) =>
        type is ITypeParameterSymbol parameter && substitutions.TryGetValue(ParameterKey(parameter), out var resolved) ? resolved : type;

    private static string ParameterKey(ITypeParameterSymbol parameter) => parameter.ContainingSymbol.OriginalDefinition.ToDisplayString() + ":" + parameter.Name;
}
