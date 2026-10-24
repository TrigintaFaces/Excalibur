// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Measures generator API references reachable from actual test methods in property suites.</summary>
/// <remarks>Static reference evidence does not prove input diversity or shrinking; behavioral tests remain required.</remarks>
internal static class GeneratedInputRule
{
    /// <summary>Returns source paths of test entry points reaching a framework random source or a property generator API.</summary>
    internal static IEnumerable<string> Find(Compilation compilation)
    {
        foreach (var type in ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace).Where(type => !type.IsAbstract && !type.IsGenericType))
        {
            foreach (var method in ConformanceArmWiring.EffectiveMethods(type).Where(method => ConformanceArmWiring.IsTest(method) || HasPropertyAttribute(method)))
            {
                if (HasPropertyAttribute(method) || ReachesGenerator(compilation, method))
                {
                    foreach (var declaration in method.DeclaringSyntaxReferences)
                    {
                        yield return declaration.SyntaxTree.FilePath;
                    }
                }
            }
        }
    }

    private static bool ReachesGenerator(Compilation compilation, IMethodSymbol entry)
    {
        var random = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly.GetTypeByMetadataName("System.Random");
        var queue = new Queue<ISymbol>();
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        queue.Enqueue(entry);
        foreach (var data in TenantMutationRule.DataMembers([entry]))
        {
            queue.Enqueue(data);
        }

        while (queue.TryDequeue(out var method))
        {
            if (!visited.Add(method.OriginalDefinition))
            {
                continue;
            }

            foreach (var declaration in method.DeclaringSyntaxReferences)
            {
                var syntax = declaration.GetSyntax();
                var model = ConformanceArmWiring.ModelFor(compilation, syntax.SyntaxTree);
                foreach (var node in syntax.DescendantNodes(descendIntoChildren: node => node == syntax || node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
                {
                    var operation = model.GetOperation(node);
                    if (operation is IObjectCreationOperation creation && SymbolEqualityComparer.Default.Equals(creation.Type, random))
                    {
                        return true;
                    }

                    if (operation is not IInvocationOperation call)
                    {
                        continue;
                    }

                    var assembly = call.TargetMethod.ContainingAssembly.Identity.Name;
                    if (string.Equals(assembly, "FsCheck", StringComparison.Ordinal) || string.Equals(assembly, "CsCheck", StringComparison.Ordinal) ||
                        SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, random))
                    {
                        return true;
                    }

                    if (SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingAssembly, compilation.Assembly) &&
                        (call.TargetMethod.IsStatic || (!call.TargetMethod.IsAbstract && !call.TargetMethod.IsVirtual && !call.TargetMethod.IsOverride)))
                    {
                        queue.Enqueue(call.TargetMethod);
                    }
                }
            }
        }

        return false;
    }

    private static bool HasPropertyAttribute(IMethodSymbol method) => method.GetAttributes().Any(attribute =>
        string.Equals(attribute.AttributeClass?.ToDisplayString(), "FsCheck.Xunit.PropertyAttribute", StringComparison.Ordinal) &&
        string.Equals(attribute.AttributeClass.ContainingAssembly.Identity.Name, "FsCheck.Xunit", StringComparison.Ordinal));
}
