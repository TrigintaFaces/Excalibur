// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using YamlDotNet.RepresentationModel;

namespace RepositoryChecks;

/// <summary>Checks R3 claims using resolved generator references instead of comment or identifier matches.</summary>
internal static class RigorEvidenceCensus
{
    /// <summary>Reports every generated-input suite and missing R3 evidence; other rungs remain artifact checks.</summary>
    /// <exception cref="InvalidOperationException">The manifest or compiled property-test population is empty.</exception>
    internal static async Task<int> WriteAsync(RepositoryCompilationCensus census, string root, string reportPath, CancellationToken cancellationToken)
    {
        var yaml = new YamlStream();
        using (var reader = File.OpenText(Path.Combine(root, "eng/governance/rigor-ladder.yaml")))
        {
            yaml.Load(reader);
        }

        var manifest = (YamlMappingNode)yaml.Documents.Single().RootNode;
        var seams = ((YamlSequenceNode)manifest.Children[new YamlScalarNode("seams")]).Children.Cast<YamlMappingNode>().ToArray();
        var projects = census.Projects.Where(project => Path.GetRelativePath(root, project.Path).Replace('\\', '/').StartsWith("tests/property/", StringComparison.Ordinal)).ToArray();
        if (seams.Length == 0 || projects.Length == 0)
        {
            throw new InvalidOperationException("Rigor evidence requires manifest seams and compiled property-test projects.");
        }

        var paths = projects.SelectMany(project => GeneratedInputRule.Find(project.Compilation))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Where(path => path.StartsWith("tests/property/", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var claimed = seams.Where(seam => ((YamlSequenceNode)seam.Children[new YamlScalarNode("met")]).Children
                .OfType<YamlScalarNode>().Any(rung => string.Equals(rung.Value, "R3", StringComparison.Ordinal)))
            .Select(seam => ((YamlScalarNode)seam.Children[new YamlScalarNode("name")]).Value ?? throw new InvalidOperationException("Unnamed rigor seam.")).ToArray();
        var missing = claimed.Where(name => !paths.Any(path => path.Replace("-", string.Empty, StringComparison.Ordinal)
            .Contains(name.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase))).ToArray();
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            status = missing.Length == 0 ? "passed" : "failed",
            seams = seams.Length,
            projects = projects.Length,
            generatorEvidence = paths,
            claimed,
            missing,
        }), cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"EXAMINED: {seams.Length} rigor seams; {projects.Length} property projects; {paths.Length} generated-input suites; {claimed.Length} R3 claims.").ConfigureAwait(false);
        return missing.Length == 0 ? 0 : 1;
    }
}
