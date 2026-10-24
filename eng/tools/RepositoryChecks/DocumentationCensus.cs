// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Applies compiler validation to independently selected Markdown fences.</summary>
internal static class DocumentationCensus
{
    /// <summary>Builds the source consumer before evaluating standalone documentation.</summary>
    internal static async Task<int> PrepareAndWriteAsync(string root, string reportPath, CancellationToken cancellationToken)
    {
        var references = await DocumentationReferences.PrepareAsync(root, cancellationToken).ConfigureAwait(false);
        return await WriteAsync(references, root, reportPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks selected fragments without attributing unchanged-context diagnostics to changed lines.</summary>
    /// <exception cref="InvalidOperationException">Fence extraction or the source population is incomplete.</exception>
    internal static async Task<int> WriteAsync(MetadataReference[] references, string root, string reportPath, CancellationToken cancellationToken)
    {
        var blocks = await ExtractAsync(root, cancellationToken).ConfigureAwait(false);
        if (blocks.Length == 0)
        {
            throw new InvalidOperationException("Documentation validation found no C# fences in the repository.");
        }

        var selected = blocks.Where(block => block.Selected && !string.Equals(block.Tier, "ignore", StringComparison.Ordinal)).ToArray();
        var results = selected.Select(block =>
        {
            var result = DocumentationApiRule.Analyze(block, blocks, references);
            var diagnostics = result.Selected.Concat(result.Context).ToArray();
            return new
            {
                block.File,
                block.StartLine,
                block.Tier,
                status = diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning) ? "unbound-or-invalid" : "bound",
                selectedDiagnostics = result.Selected.Select(diagnostic => diagnostic.ToString()).ToArray(),
                contextualDiagnostics = result.Context.Select(diagnostic => diagnostic.ToString()).ToArray(),
            };
        }).ToArray();
        var failures = results.Where(result => !string.Equals(result.status, "bound", StringComparison.Ordinal)).ToArray();
        foreach (var result in failures)
        {
            foreach (var diagnostic in result.selectedDiagnostics.Concat(result.contextualDiagnostics))
            {
                await Console.Error.WriteLineAsync(diagnostic).ConfigureAwait(false);
            }
        }

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            status = failures.Length == 0 ? "passed" : "failed",
            fences = blocks.Length,
            selected = selected.Length,
            ignored = blocks.Count(block => block.Selected && string.Equals(block.Tier, "ignore", StringComparison.Ordinal)),
            results,
        }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"EXAMINED: {blocks.Length} documentation fences; {selected.Length} selected; {failures.Length} unbound/invalid snippets.")).ConfigureAwait(false);
        return failures.Length == 0 ? 0 : 1;
    }

    private static async Task<DocumentationBlock[]> ExtractAsync(string root, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "eng/ci/docs-csharp-extract.py", "--request", "--repo", root, "--base", Environment.GetEnvironmentVariable("DOCS_GATE_BASE_REF") ?? "HEAD" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Markdown extraction.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Markdown extraction failed: " + await error.ConfigureAwait(false));
        }

        return JsonSerializer.Deserialize<DocumentationBlock[]>(await output.ConfigureAwait(false), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Markdown extraction returned no records.");
    }
}
