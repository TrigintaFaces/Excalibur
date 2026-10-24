// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Text.Json;

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Uses an actual MSBuild consumer graph rather than merging private project dependencies.</summary>
internal static class DocumentationReferences
{
    /// <summary>Builds the consumer before any workspace can lock its source generator assemblies.</summary>
    /// <exception cref="InvalidOperationException">Consumer preparation failed or produced no evidence.</exception>
    internal static async Task<MetadataReference[]> PrepareAsync(string root, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(root, "artifacts", "repository-checks", "documentation-consumer", Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-NoProfile", "-File", "eng/tools/RepositoryChecks/prepare-documentation-consumer.ps1", "-RepositoryRoot", root, "-OutputDirectory", directory })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not prepare documentation consumer.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Consumer build failed: " + await output.ConfigureAwait(false) + await error.ConfigureAwait(false));
        }

        await Console.Out.WriteLineAsync("Documentation consumer evidence: " + directory).ConfigureAwait(false);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "consumer.json"), cancellationToken).ConfigureAwait(false));
        return Create(document.RootElement.GetProperty("references").EnumerateArray().Select(value => value.GetString() ?? string.Empty));
    }

    /// <summary>Reads only the concrete reference roster emitted by ResolveReferences.</summary>
    /// <exception cref="InvalidOperationException">The reference population is empty or missing a file.</exception>
    internal static MetadataReference[] Create(IEnumerable<string> paths)
    {
        var files = paths.ToArray();
        if (files.Length == 0 || files.Any(path => !Path.IsPathFullyQualified(path) || !File.Exists(path)))
        {
            throw new InvalidOperationException("Incomplete MSBuild consumer reference population.");
        }

        return files.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }
}
