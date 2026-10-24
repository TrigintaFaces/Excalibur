// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RepositoryChecks;

/// <summary>Prevents incomplete compilations from producing successful policy evidence.</summary>
internal static class CompilationValidation
{
    /// <summary>Refuses compiler diagnostics in every source project required by the root.</summary>
    /// <exception cref="InvalidOperationException">A required compilation is missing or has diagnostics.</exception>
    internal static async Task ValidateProjectClosureAsync(Project root, CancellationToken cancellationToken, ISet<ProjectId>? validated = null)
    {
        var solution = root.Solution;
        foreach (var id in solution.GetProjectDependencyGraph().GetProjectsThatThisProjectTransitivelyDependsOn(root.Id).Append(root.Id))
        {
            if (validated?.Contains(id) == true)
            {
                continue;
            }

            var project = solution.GetProject(id) ?? throw new InvalidOperationException("A required source project is missing.");
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"No compilation for {project.Name}.");
            await GeneratorValidation.ValidateAsync(project, compilation, cancellationToken).ConfigureAwait(false);
            var diagnostics = compilation.GetDiagnostics(cancellationToken)
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
            foreach (var diagnostic in diagnostics)
            {
                await Console.Error.WriteLineAsync(diagnostic.ToString()).ConfigureAwait(false);
            }

            if (diagnostics.Length != 0)
            {
                throw new InvalidOperationException($"Diagnostic-bearing compilation {project.Name} cannot certify repository policies.");
            }

            validated?.Add(id);
        }
    }

    /// <summary>Refuses incomplete workspace evaluation.</summary>
    /// <exception cref="InvalidOperationException">The workspace has diagnostics.</exception>
    internal static async Task ValidateWorkspaceAsync(MSBuildWorkspace workspace)
    {
        foreach (var diagnostic in workspace.Diagnostics)
        {
            await Console.Error.WriteLineAsync(diagnostic.ToString()).ConfigureAwait(false);
        }

        if (!workspace.Diagnostics.IsEmpty)
        {
            throw new InvalidOperationException("Incomplete workspace cannot certify repository policies.");
        }
    }
}
