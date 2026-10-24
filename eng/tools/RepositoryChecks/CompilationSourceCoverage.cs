// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Reconciles independently discovered source files with evaluated compiler input.</summary>
/// <remarks>Conditional changes to active tokens require evaluated configuration support; unevaluated branches refuse certification.</remarks>
internal sealed class CompilationSourceCoverage
{
    private readonly HashSet<string> _paths = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _evaluated = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Records source variants per compilation without borrowing another project's configuration.</summary>
    /// <exception cref="InvalidOperationException">A conditional source variant was not compiled.</exception>
    internal void Add(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees.Where(tree => File.Exists(tree.FilePath)))
        {
            var path = Path.GetFullPath(tree.FilePath);
            var identity = SourceOwnership.SyntaxIdentity(tree);
            var source = tree.GetText().ToString();
            if (source.Contains('#', StringComparison.Ordinal) && CSharpConfigurations.Parse(source).Any(variant =>
                !string.Equals(SourceOwnership.SyntaxIdentity(variant), identity, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Unevaluated source variant in " + compilation.AssemblyName + ": " + path);
            }

            _paths.Add(path);
            if (!_evaluated.TryGetValue(path, out var identities))
            {
                identities = [];
                _evaluated.Add(path, identities);
            }

            identities.Add(identity);
        }
    }

    /// <summary>Refuses unowned inheritance and reports excluded files that cannot contribute it.</summary>
    internal string[] Verify(string root, string directory) => SourceOwnership.Verify(root, _paths, _evaluated, directory);
}
