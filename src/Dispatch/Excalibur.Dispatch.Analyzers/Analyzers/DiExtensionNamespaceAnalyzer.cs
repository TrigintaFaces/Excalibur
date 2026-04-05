// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Analyzers;

/// <summary>
/// Analyzes DI extension classes and reports DISP101 when they are not in the
/// <c>Microsoft.Extensions.DependencyInjection</c> namespace.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiExtensionNamespaceAnalyzer : DiagnosticAnalyzer
{
    private const string ExpectedNamespace = "Microsoft.Extensions.DependencyInjection";
    private const string ServiceCollectionTypeName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.DiExtensionWrongNamespace);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Only analyze static classes
        if (!namedType.IsStatic)
        {
            return;
        }

        // Check if any method is an extension method with IServiceCollection as the first parameter
        var hasServiceCollectionExtension = false;
        var serviceCollectionType = context.Compilation.GetTypeByMetadataName(ServiceCollectionTypeName);
        if (serviceCollectionType == null)
        {
            return;
        }

        foreach (var member in namedType.GetMembers())
        {
            if (member is IMethodSymbol method && method.IsExtensionMethod && method.Parameters.Length >= 1)
            {
                if (SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serviceCollectionType))
                {
                    hasServiceCollectionExtension = true;
                    break;
                }
            }
        }

        if (!hasServiceCollectionExtension)
        {
            return;
        }

        // Check namespace
        var containingNamespace = namedType.ContainingNamespace?.ToDisplayString();
        if (containingNamespace is not null and not "Microsoft.Extensions.DependencyInjection")
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    AnalyzerDiagnosticDescriptors.DiExtensionWrongNamespace,
                    namedType.Locations[0],
                    namedType.Name,
                    containingNamespace));
        }
    }
}
