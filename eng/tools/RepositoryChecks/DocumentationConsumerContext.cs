// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Keeps actual consumer generators/options separate from compile references.</summary>
internal sealed class DocumentationConsumerContext : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private Project? _project;

    private DocumentationConsumerContext(MetadataReference[] references, string root)
    {
        References = references;
        _workspace = MSBuildWorkspace.Create(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Configuration"] = "Release",
            ["BuildExamplesAndTests"] = "true",
            ["SolutionDir"] = root + Path.DirectorySeparatorChar,
            ["SolutionName"] = "Excalibur",
            ["BuildProjectReferences"] = "false",
        });
    }

    /// <summary>Gets the exact prepared consumer runtime reference roster.</summary>
    internal MetadataReference[] References { get; }

    /// <summary>Gets the actual evaluated generator/options context after successful preparation.</summary>
    /// <exception cref="InvalidOperationException">The context has not completed initialization.</exception>
    internal Project Project => _project ?? throw new InvalidOperationException("Documentation context is not initialized.");

    /// <inheritdoc />
    public void Dispose()
    {
        _workspace.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Uses only the real SDK-generated global-using document, never consumer business declarations.</summary>
    internal static SyntaxTree[] GlobalUsings(Project? project)
    {
        if (project is null)
        {
            return [];
        }

        var name = (project.FilePath is null ? project.AssemblyName ?? project.Name : Path.GetFileNameWithoutExtension(project.FilePath)) + ".GlobalUsings.g.cs";
        return project.Documents.Where(document => string.Equals(Path.GetFileName(document.FilePath), name, StringComparison.Ordinal))
            .Select(document => document.TryGetSyntaxTree(out var tree) && tree is not null ? tree : throw new InvalidOperationException("SDK global usings were not loaded."))
            .ToArray();
    }

    /// <summary>Owns workspace creation and cleans up if project evaluation fails or is cancelled.</summary>
    internal static async Task<DocumentationConsumerContext> OpenAsync(MetadataReference[] references, string root, string projectPath, string? expectedGlobalUsingsPath, CompilationOptions? diagnosticPolicy, CancellationToken cancellationToken)
    {
        var context = new DocumentationConsumerContext(references, root);
        try
        {
            // The consumer has just been built; its analyzer/options context remains
            // separate from the explicit runtime ReferencePath roster.
            context._workspace.LoadMetadataForReferencedProjects = true;
            context._project = await context._workspace.OpenProjectAsync(
                projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            await CompilationValidation.ValidateWorkspaceAsync(context._workspace).ConfigureAwait(false);
            if (diagnosticPolicy is not null && context._project.CompilationOptions is { } options)
            {
                // Optional dependencies never import a sample's warning suppressions into documentation.
                context._project = context._project.WithCompilationOptions(options
                    .WithSpecificDiagnosticOptions(diagnosticPolicy.SpecificDiagnosticOptions)
                    .WithGeneralDiagnosticOption(diagnosticPolicy.GeneralDiagnosticOption));
            }

            var globals = context._project.Documents.Where(document => string.Equals(Path.GetFileName(document.FilePath), Path.GetFileNameWithoutExtension(projectPath) + ".GlobalUsings.g.cs", StringComparison.Ordinal)).ToArray();
            if (globals.Length != 1)
            {
                throw new InvalidOperationException("Expected the actual SDK global-usings document in the consumer project.");
            }

            if (expectedGlobalUsingsPath is not null && !string.Equals(
                Path.GetFullPath(globals[0].FilePath ?? string.Empty), Path.GetFullPath(expectedGlobalUsingsPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Application-profile global usings do not match the actual SDK path.");
            }

            _ = await globals[0].GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The actual SDK global-usings syntax could not be loaded.");
            return context;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }
}
