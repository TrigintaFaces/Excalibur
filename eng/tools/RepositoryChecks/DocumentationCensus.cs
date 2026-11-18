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
        using var references = await DocumentationReferences.PrepareAsync(root, cancellationToken).ConfigureAwait(false);
        return await WriteAsync(references, root, reportPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks selected fragments without attributing unchanged-context diagnostics to changed lines.</summary>
    /// <exception cref="InvalidOperationException">Fence extraction or the source population is incomplete.</exception>
    internal static async Task<int> WriteAsync(DocumentationConsumerContext references, string root, string reportPath, CancellationToken cancellationToken)
    {
        var blocks = await ExtractAsync(root, cancellationToken).ConfigureAwait(false);
        if (blocks.Length == 0)
        {
            throw new InvalidOperationException("Documentation validation found no C# fences in the repository.");
        }

        var selected = blocks.Where(block => block.Selected && !string.Equals(block.Tier, "ignore", StringComparison.Ordinal)).ToArray();
        var results = new List<object>();
        var failures = new List<string[]>();
        foreach (var group in selected.GroupBy(block => ProfileProject(block.File), StringComparer.Ordinal))
        {
            if (group.Key is null)
            {
                AnalyzeGroup(group, blocks, references, results, failures);
                continue;
            }

            using var profile = await DocumentationReferences.PrepareProfileAsync(
                root, group.Key, references.References, references.Project.CompilationOptions, cancellationToken).ConfigureAwait(false);
            AnalyzeGroup(group, blocks, profile, results, failures);
        }

        foreach (var diagnostics in failures)
        {
            foreach (var diagnostic in diagnostics)
            {
                await Console.Error.WriteLineAsync(diagnostic).ConfigureAwait(false);
            }
        }

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            status = failures.Count == 0 ? "passed" : "failed",
            fences = blocks.Length,
            selected = selected.Length,
            ignored = blocks.Count(block => block.Selected && string.Equals(block.Tier, "ignore", StringComparison.Ordinal)),
            results,
        }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"EXAMINED: {blocks.Length} documentation fences; {selected.Length} selected; {failures.Count} unbound/invalid snippets.")).ConfigureAwait(false);
        return failures.Count == 0 ? 0 : 1;
    }

    /// <summary>Selects only existing, explicitly supported optional application dependency profiles.</summary>
    internal static string? ProfileProject(string file) => file.Replace('\\', '/') switch
    {
        "docs-site/docs/deployment/aws-lambda.md" => "samples/05-serverless/AwsLambda/AwsLambda.csproj",
        "docs-site/docs/deployment/azure-functions.md" => "samples/05-serverless/AzureFunctions/AzureFunctions.csproj",
        "docs-site/docs/deployment/google-cloud-functions.md" => "samples/05-serverless/GoogleCloudFunctions/GoogleCloudFunctions.csproj",
        "docs-site/docs/advanced/native-aot.md" => "samples/10-aot/Excalibur.Dispatch.Aot.Sample/Excalibur.Dispatch.Aot.Sample.csproj",
        _ => null,
    };

    /// <summary>Preserves one group's strict per-fence diagnostics and report order.</summary>
    private static void AnalyzeGroup(IEnumerable<DocumentationBlock> group, DocumentationBlock[] blocks, DocumentationConsumerContext consumer, List<object> results, List<string[]> failures)
    {
        foreach (var block in group)
        {
            var result = DocumentationApiRule.Analyze(block, blocks, consumer.References, consumer.Project);
            var diagnostics = result.Selected.Concat(result.Context).ToArray();
            var failed = diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
            var selectedDiagnostics = result.Selected.Select(diagnostic => diagnostic.ToString()).ToArray();
            var contextualDiagnostics = result.Context.Select(diagnostic => diagnostic.ToString()).ToArray();
            results.Add(new
            {
                block.File,
                block.StartLine,
                block.Tier,
                status = failed ? "unbound-or-invalid" : "bound",
                selectedDiagnostics,
                contextualDiagnostics,
            });
            if (failed)
            {
                failures.Add(selectedDiagnostics.Concat(contextualDiagnostics).ToArray());
            }
        }
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
