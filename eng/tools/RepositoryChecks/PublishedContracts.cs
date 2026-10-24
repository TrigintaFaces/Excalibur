// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Anchors contract exemptions to the repository project rather than an assembly name.</summary>
internal static class PublishedContracts
{
    /// <summary>Finds the published contract compilation among evaluated source projects.</summary>
    /// <exception cref="InvalidOperationException">A required source compilation is unavailable.</exception>
    internal static async Task<IAssemblySymbol?> FindAsync(Project project, string root, CancellationToken cancellationToken)
    {
        var expected = Path.GetFullPath(Path.Combine(root, "src", "Excalibur", "Excalibur.Testing.Conformance", "Excalibur.Testing.Conformance.csproj"));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var published = project.Solution.Projects.SingleOrDefault(candidate => candidate.FilePath is not null &&
            string.Equals(Path.GetFullPath(candidate.FilePath), expected, comparison));
        if (published is null)
        {
            return null;
        }

        var compilation = await published.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var consumer = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null || consumer is null)
        {
            throw new InvalidOperationException("The published contract or consumer compilation is missing.");
        }

        return ResolveReference(consumer, compilation.Assembly);
    }

    /// <summary>Uses the consumer's retargeted assembly symbol while anchoring the source compilation.</summary>
    internal static IAssemblySymbol? ResolveReference(Compilation consumer, IAssemblySymbol published)
    {
        if (SymbolEqualityComparer.Default.Equals(consumer.Assembly, published))
        {
            return consumer.Assembly;
        }

        return consumer.References.OfType<CompilationReference>()
            .Where(reference => SymbolEqualityComparer.Default.Equals(reference.Compilation.Assembly, published))
            .Select(reference => consumer.GetAssemblyOrModuleSymbol(reference)).OfType<IAssemblySymbol>().SingleOrDefault();
    }
}
