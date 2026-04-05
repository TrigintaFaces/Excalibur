// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Analyzers;

/// <summary>
/// Analyzes static extension classes and reports DISP102 when their name uses an
/// interface-style 'I' prefix (e.g., <c>IDispatcherExtensions</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionClassIPrefixAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.ExtensionClassIPrefixNaming);

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

        // Only analyze static classes (extension method containers)
        if (!namedType.IsStatic)
        {
            return;
        }

        // Check if it has any extension methods
        var hasExtensionMethods = false;
        foreach (var member in namedType.GetMembers())
        {
            if (member is IMethodSymbol { IsExtensionMethod: true })
            {
                hasExtensionMethods = true;
                break;
            }
        }

        if (!hasExtensionMethods)
        {
            return;
        }

        // Check for 'I' prefix followed by uppercase letter
        var name = namedType.Name;
        if (name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]))
        {
            var suggestedName = name.Substring(1);
            context.ReportDiagnostic(
                Diagnostic.Create(
                    AnalyzerDiagnosticDescriptors.ExtensionClassIPrefixNaming,
                    namedType.Locations[0],
                    name,
                    suggestedName));
        }
    }
}
