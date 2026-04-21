// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Analyzers;

/// <summary>
/// Analyzes namespace declarations and reports DISP104 when a namespace contains a
/// <c>.Core.</c> segment, which violates ADR-075 naming conventions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CoreNamespaceSegmentAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.NamespaceContainsCoreSegment);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            AnalyzeNamespaceDeclaration,
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.FileScopedNamespaceDeclaration);
    }

    private static void AnalyzeNamespaceDeclaration(SyntaxNodeAnalysisContext context)
    {
        string namespaceName;
        Location location;

        switch (context.Node)
        {
            case NamespaceDeclarationSyntax ns:
                namespaceName = ns.Name.ToString();
                location = ns.Name.GetLocation();
                break;
            case FileScopedNamespaceDeclarationSyntax ns:
                namespaceName = ns.Name.ToString();
                location = ns.Name.GetLocation();
                break;
            default:
                return;
        }

        // Only check Excalibur/Dispatch namespaces
        if (!namespaceName.StartsWith("Excalibur", StringComparison.Ordinal) && !namespaceName.StartsWith("Dispatch", StringComparison.Ordinal))
        {
            return;
        }

        // Check for .Core. segment
        if (namespaceName.IndexOf(".Core.", StringComparison.Ordinal) >= 0 || namespaceName.EndsWith(".Core", StringComparison.Ordinal))
        {
            var suggestedNamespace = namespaceName
                .Replace(".Core.", ".")
                .Replace(".Core", string.Empty);

            context.ReportDiagnostic(
                Diagnostic.Create(
                    AnalyzerDiagnosticDescriptors.NamespaceContainsCoreSegment,
                    location,
                    namespaceName,
                    suggestedNamespace));
        }
    }
}
