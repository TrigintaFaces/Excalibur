// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace Boundary.Tests;

/// <summary>Readable source and Roslyn parse configurations for repository architecture checks.</summary>
internal static class CSharpSourceInventory
{
    internal static IReadOnlyList<SourceFile> ReadProductionSources() => ReadSources(
        Directory.EnumerateFiles(Path.Combine(TestHelpers.GetRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsProductionSource));

    internal static bool IsProductionSource(string path) => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "bin" or "obj");

    internal static IReadOnlyList<SourceFile> ReadSources(IEnumerable<string> paths)
    {
        var files = paths.Select(path => new SourceFile(path, File.ReadAllText(path))).ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException("C# policy validation requires a nonempty, readable source population.");
        }

        return files;
    }

    internal static IEnumerable<SyntaxTree> ParseConfigurations(string source) => RepositoryChecks.CSharpConfigurations.Parse(source);
}
