// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Finds concrete suites that inherit a private conformance contract instead of a published kit.</summary>
internal static class ConformanceForkRule
{
    /// <summary>The assembly that owns the published contracts.</summary>
    internal const string PublishedAssembly = "Excalibur.Testing.Conformance";

    /// <summary>Finds private abstract conformance ancestors of concrete source suites.</summary>
    /// <exception cref="InvalidOperationException">Required semantic inputs cannot be resolved.</exception>
    internal static IReadOnlyList<ConformanceFork> Analyze(Compilation compilation, IAssemblySymbol? publishedAssembly = null, string? testRoot = null)
    {
        var found = new HashSet<ConformanceFork>();
        foreach (var type in Types(compilation.Assembly.GlobalNamespace).Where(type => type.TypeKind == TypeKind.Class && !type.IsAbstract))
        {
            for (var parent = type.BaseType; parent is not null; parent = parent.BaseType)
            {
                if (parent.TypeKind == TypeKind.Error)
                {
                    throw new InvalidOperationException($"Unresolved base type for {Identity(type)}.");
                }

                if (parent.IsAbstract && parent.Name.Contains("Conformance", StringComparison.Ordinal) &&
                    (testRoot is null || DeclaredUnder(parent, testRoot)) &&
                    !SymbolEqualityComparer.Default.Equals(parent.ContainingAssembly, publishedAssembly))
                {
                    found.Add(new ConformanceFork(parent.Name, Identity(parent.OriginalDefinition), Identity(type)));
                }
            }
        }

        return found.OrderBy(fork => fork.BaseIdentity, StringComparer.Ordinal)
            .ThenBy(fork => fork.SuiteIdentity, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Enumerates namespace and nested types using symbol identities.</summary>
    internal static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in Types(type))
                {
                    yield return nested;
                }
            }
            else if (member is INamespaceSymbol space)
            {
                foreach (var nested in Types(space))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool DeclaredUnder(INamedTypeSymbol type, string root)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return type.Locations.Any(location => location.IsInSource && !string.IsNullOrEmpty(location.SourceTree?.FilePath) &&
            Path.GetFullPath(location.SourceTree.FilePath).StartsWith(prefix, comparison));
    }

    private static string Identity(INamedTypeSymbol type) =>
        type.ContainingAssembly.Identity.Name + ":" + type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
}
