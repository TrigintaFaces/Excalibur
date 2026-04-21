// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Analyzers;

/// <summary>
/// Analyzes interface method declarations and reports DISP103 when a
/// <see cref="CancellationToken"/> parameter has a default value.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenDefaultAnalyzer : DiagnosticAnalyzer
{
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.CancellationTokenOptionalInInterface);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Only analyze interface method declarations
        if (method.ContainingType?.TypeKind != TypeKind.Interface)
        {
            return;
        }

        // Only analyze within Excalibur namespaces
        var namespaceName = method.ContainingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!namespaceName.StartsWith("Excalibur", StringComparison.Ordinal) && !namespaceName.StartsWith("Dispatch", StringComparison.Ordinal))
        {
            return;
        }

        var cancellationTokenType = context.Compilation.GetTypeByMetadataName(CancellationTokenTypeName);
        if (cancellationTokenType == null)
        {
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType) && parameter.HasExplicitDefaultValue)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        AnalyzerDiagnosticDescriptors.CancellationTokenOptionalInInterface,
                        parameter.Locations[0],
                        parameter.Name,
                        method.ContainingType.Name,
                        method.Name));
            }
        }
    }
}
