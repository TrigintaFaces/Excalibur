// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Routes source-artifact extraction commands that do not require an MSBuild workspace.</summary>
internal static class ArtifactCommands
{
    /// <summary>Returns null when the request belongs to a compiled project policy.</summary>
    internal static async Task<int?> TryRunAsync(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--ddl-facts", StringComparison.Ordinal))
        {
            return await CSharpDdlFacts.RunAsync().ConfigureAwait(false);
        }

        if (args.Length == 2 && string.Equals(args[0], "--image-strings", StringComparison.Ordinal))
        {
            return await CSharpImageReferences.RunAsync(args[1]).ConfigureAwait(false);
        }

        if (args.Length == 2 && string.Equals(args[0], "--sql-text", StringComparison.Ordinal))
        {
            return await CSharpSqlStrings.RunAsync(args[1]).ConfigureAwait(false);
        }

        return null;
    }
}
