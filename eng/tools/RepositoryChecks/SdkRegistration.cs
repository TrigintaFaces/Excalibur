// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.Build.Locator;

namespace RepositoryChecks;

/// <summary>Loads the repository's pinned SDK before workspace types are used.</summary>
internal static class SdkRegistration
{
    /// <summary>Registers MSBuild and refuses an SDK version mismatch.</summary>
    /// <exception cref="InvalidOperationException">Required semantic inputs cannot be resolved.</exception>
    internal static async Task<string> RegisterAsync(string root, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "global.json"), cancellationToken).ConfigureAwait(false));
        var expected = document.RootElement.GetProperty("sdk").GetProperty("version").GetString()
            ?? throw new InvalidOperationException("The repository must pin an SDK version.");
        Directory.SetCurrentDirectory(root);
        var instance = MSBuildLocator.RegisterDefaults();
        if (!string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(instance.MSBuildPath)), expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Selected SDK {instance.MSBuildPath} differs from required {expected}.");
        }

        return expected;
    }
}
