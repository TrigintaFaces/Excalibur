// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using RepositoryChecks;

namespace RepositoryAnalyzers;

/// <summary>Enforces emulator connection wiring during integration-project compilation.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CosmosFixtureAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Wiring = new(
        "REPO001",
        "Cosmos emulator client wiring must match its container",
        "{0}",
        "RepositoryArchitecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each Cosmos emulator client must use the enrolled container's connection string and HTTP factory.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Wiring);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        if (!context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.RepositoryCosmosFixturePolicy", out var enabled) ||
            !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        foreach (var violation in CosmosFixtureRule.Analyze(context.Compilation, context.CancellationToken).Violations)
        {
            context.ReportDiagnostic(Diagnostic.Create(Wiring, violation.Location, violation.Message));
        }
    }
}
