// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RepositoryChecks;

/// <summary>Reuses an evaluated reference's loader while permitting fresh generator discovery.</summary>
internal sealed class ExistingAnalyzerAssemblyLoader(AnalyzerFileReference reference) : IAnalyzerAssemblyLoader
{
    /// <inheritdoc />
    public Assembly LoadFromPath(string fullPath)
    {
        if (!string.Equals(fullPath, reference.FullPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An analyzer loader cannot load a different reference.");
        }

        return reference.GetAssembly();
    }

    /// <inheritdoc />
    public void AddDependencyLocation(string fullPath)
    {
        // The original evaluated reference owns dependency registration and assembly resolution.
    }
}
