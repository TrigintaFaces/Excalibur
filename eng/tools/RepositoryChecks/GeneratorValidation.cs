// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RepositoryChecks;

/// <summary>Checks generator load and execution diagnostics independently of ordinary compiler diagnostics.</summary>
internal static class GeneratorValidation
{
    /// <summary>Runs the evaluated generators against original source and refuses failures.</summary>
    /// <exception cref="InvalidOperationException">A generator cannot load or reports a warning or error.</exception>
    internal static async Task ValidateAsync(Project project, Compilation compilation, CancellationToken cancellationToken)
    {
        var generators = Discover(project);
        var generatedTrees = new List<SyntaxTree>();
        foreach (var document in await project.GetSourceGeneratedDocumentsAsync(cancellationToken).ConfigureAwait(false))
        {
            generatedTrees.Add(await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Missing generated syntax tree for {document.Name}."));
        }

        if (generators.Count == 0 && generatedTrees.Count == 0)
        {
            return;
        }

        var driver = CSharpGeneratorDriver.Create(
            generators,
            project.AnalyzerOptions.AdditionalFiles,
            (CSharpParseOptions?)project.ParseOptions,
            project.AnalyzerOptions.AnalyzerConfigOptionsProvider);
        var result = driver.RunGenerators(compilation.RemoveSyntaxTrees(generatedTrees), cancellationToken).GetRunResult();
        var diagnostics = result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ToArray();
        if (diagnostics.Length != 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        var originalOutputs = generatedTrees.Select(tree => tree.GetText(cancellationToken).ToString()).Order(StringComparer.Ordinal);
        var verifiedOutputs = result.GeneratedTrees.Select(tree => tree.GetText(cancellationToken).ToString()).Order(StringComparer.Ordinal);
        if (!originalOutputs.SequenceEqual(verifiedOutputs, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Generator validation produced different source from the compilation being audited.");
        }
    }

    /// <summary>Loads generator instances without trusting cached discovery results.</summary>
    /// <exception cref="InvalidOperationException">A generator reference cannot be loaded.</exception>
    private static List<ISourceGenerator> Discover(Project project)
    {
        var generators = new List<ISourceGenerator>();
        foreach (var reference in project.AnalyzerReferences)
        {
            if (reference is UnresolvedAnalyzerReference)
            {
                throw new InvalidOperationException($"Unresolved analyzer reference: {reference.FullPath}.");
            }

            var failures = new List<string>();
            void OnLoadFailed(object? sender, AnalyzerLoadFailureEventArgs args) => failures.Add(args.Message);
            var freshReference = reference is AnalyzerFileReference original
                ? new AnalyzerFileReference(original.FullPath, new ExistingAnalyzerAssemblyLoader(original))
                : reference;
            var file = freshReference as AnalyzerFileReference;
            if (file is not null)
            {
                file.AnalyzerLoadFailed += OnLoadFailed;
            }

            try
            {
                generators.AddRange(freshReference.GetGenerators(project.Language));
            }
            finally
            {
                if (file is not null)
                {
                    file.AnalyzerLoadFailed -= OnLoadFailed;
                }
            }

            if (failures.Count != 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
            }
        }

        return generators;
    }
}
