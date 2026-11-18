// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Uses an actual MSBuild consumer graph rather than merging private project dependencies.</summary>
internal static class DocumentationReferences
{
    /// <summary>Builds the consumer before any workspace can lock its source generator assemblies.</summary>
    /// <exception cref="InvalidOperationException">Consumer preparation failed or produced no evidence.</exception>
    internal static Task<DocumentationConsumerContext> PrepareAsync(string root, CancellationToken cancellationToken) =>
        PrepareCoreAsync(root, profileProject: null, [], diagnosticPolicy: null, cancellationToken);

    /// <summary>Uses canonical or SDK-unified runtime references with a fixed application's actual compiler context.</summary>
    internal static Task<DocumentationConsumerContext> PrepareProfileAsync(string root, string profileProject, MetadataReference[] canonicalReferences, CompilationOptions? diagnosticPolicy, CancellationToken cancellationToken) =>
        PrepareCoreAsync(root, profileProject, canonicalReferences, diagnosticPolicy, cancellationToken);

    /// <summary>The Native guide borrows sample generators/options/imports, without importing its runtime graph.</summary>
    internal static bool IsGeneratorOnlyProfile(string? profileProject) => string.Equals(
        profileProject,
        "samples/10-aot/Excalibur.Dispatch.Aot.Sample/Excalibur.Dispatch.Aot.Sample.csproj",
        StringComparison.Ordinal);

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

    /// <summary>Preserves canonical APIs while adding one profile; conflicting assembly identities or bytes refuse.</summary>
    /// <exception cref="InvalidOperationException">A reference has no physical identity, or two bindings disagree.</exception>
    internal static MetadataReference[] Merge(IEnumerable<MetadataReference> canonical, IEnumerable<MetadataReference> profile)
    {
        var selected = new Dictionary<string, (string Identity, string Hash, MetadataReference Reference)>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in canonical.Concat(profile))
        {
            var path = reference.Display;
            if (path is null || !Path.IsPathFullyQualified(path) || !File.Exists(path))
            {
                throw new InvalidOperationException("Documentation references must have actual physical assembly bindings.");
            }

            var identity = AssemblyName.GetAssemblyName(path);
            var name = identity.Name ?? throw new InvalidOperationException("Documentation assembly has no name.");
            var fullName = identity.FullName ?? throw new InvalidOperationException("Documentation assembly has no full identity.");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (selected.TryGetValue(name, out var prior))
            {
                if (!string.Equals(prior.Identity, fullName, StringComparison.Ordinal) || !string.Equals(prior.Hash, hash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Conflicting documentation assembly bindings: " + name);
                }
            }
            else
            {
                selected.Add(name, (fullName, hash, reference));
            }
        }

        return selected.Values.Select(value => value.Reference).ToArray();
    }

    private static async Task<DocumentationConsumerContext> PrepareCoreAsync(string root, string? profileProject, MetadataReference[] canonicalReferences, CompilationOptions? diagnosticPolicy, CancellationToken cancellationToken)
    {
        var inputs = await PrepareInputsAsync(root, profileProject, cancellationToken).ConfigureAwait(false);
        var references = IsGeneratorOnlyProfile(profileProject) ? canonicalReferences : inputs.References;
        return await DocumentationConsumerContext.OpenAsync(
            profileProject is null ? references : Merge([], references),
            root, inputs.ProjectPath, inputs.Globals, diagnosticPolicy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finishes temporary-owner cleanup before any workspace can be allocated for return.</summary>
    /// <exception cref="InvalidOperationException">Preparation could not start, completed unsuccessfully, or produced incomplete references.</exception>
    private static async Task<(MetadataReference[] References, string ProjectPath, string? Globals)> PrepareInputsAsync(string root, string? profileProject, CancellationToken cancellationToken)
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

        if (profileProject is not null)
        {
            start.ArgumentList.Add("-ProfileProject");
            start.ArgumentList.Add(profileProject);
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

        await Task.WhenAll(output, error).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Consumer build failed: " + await output.ConfigureAwait(false) + await error.ConfigureAwait(false));
        }

        await Console.Out.WriteLineAsync("Documentation consumer evidence: " + directory).ConfigureAwait(false);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "consumer.json"), cancellationToken).ConfigureAwait(false));
        var references = document.RootElement.GetProperty("references").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        var projectPath = profileProject is null ? Path.Combine(directory, "DocumentationConsumer.csproj") : Path.Combine(root, profileProject);
        var globals = document.RootElement.TryGetProperty("globalUsingsFile", out var value) ? value.GetString() : null;
        return (IsGeneratorOnlyProfile(profileProject) ? [] : Create(references), projectPath, globals);
    }
}
