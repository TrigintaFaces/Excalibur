// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Prevents excluded source from silently escaping the semantic inheritance census.</summary>
internal static class SourceOwnership
{
    /// <summary>Returns uncompiled files only when their syntax cannot declare inheritance.</summary>
    /// <exception cref="InvalidOperationException">An uncompiled source file may contribute inheritance.</exception>
    internal static string[] Verify(string root, ISet<string> owned, IReadOnlyDictionary<string, HashSet<string>> evaluated,
        string directory = "tests")
    {
        var paths = Directory.EnumerateFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment =>
                string.Equals(segment, "bin", StringComparison.Ordinal) || string.Equals(segment, "obj", StringComparison.Ordinal)))
            .Select(Path.GetFullPath).Order(StringComparer.Ordinal).ToArray();
        foreach (var path in paths)
        {
            var source = File.ReadAllText(path);
            if (owned.Contains(path) && !source.Contains('#', StringComparison.Ordinal))
            {
                continue;
            }

            var covered = evaluated.TryGetValue(path, out var positions) ? positions : [];
            if (owned.Contains(path) ? HasUncoveredSyntax(source, covered) : HasInheritance(source) || HasGlobalUsing(source))
            {
                throw new InvalidOperationException($"Source syntax lacks an evaluated compilation and cannot be omitted: {path}");
            }
        }

        return paths.Where(path => !owned.Contains(path)).ToArray();
    }

    /// <summary>Checks real declaration syntax, including inactive conditional branches.</summary>
    internal static bool HasInheritance(string source) => CSharpConfigurations.Parse(source)
        .Any(tree => tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Any(type =>
            type.BaseList is not null || type.Modifiers.Any(token => string.Equals(token.ValueText, "partial", StringComparison.Ordinal))));

    /// <summary>Refuses unevaluated syntax, including aliases outside files declaring inheritance.</summary>
    internal static bool HasUncoveredSyntax(string source, ISet<string> covered) => CSharpConfigurations.Parse(source)
        .Any(tree => !covered.Contains(SyntaxIdentity(tree)));

    /// <summary>Identifies active tokens without comments, directives, or disabled source text.</summary>
    internal static string SyntaxIdentity(SyntaxTree tree) => string.Concat(tree.GetRoot().DescendantTokens()
        .Select(token => string.Create(CultureInfo.InvariantCulture, $"{token.RawKind}:{token.Text.Length}:{token.Text}")));

    private static bool HasGlobalUsing(string source) => CSharpConfigurations.Parse(source)
        .Any(tree => tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().Any(directive => directive.GlobalKeyword.RawKind != 0));
}
