// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Boundary.Tests;

/// <summary>Reads pristine build inputs; never rewrites or interprets coverage instrumentation.</summary>
internal static class PristineOptionLivenessInputs
{
    internal static string[] GetPaths()
    {
        var metadata = typeof(PristineOptionLivenessInputs).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "PristineOptionLiveness").Value!.Split('|');
        if (metadata.Length != 3 || metadata.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Missing pristine build metadata.");
        var runtime = BoundaryFrameworkAssemblyFixture.GetExpectedAssemblies(AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "Boundary.Tests.deps.json"));
        // Same source-generator exclusion as the original shipped option census.
        var files = runtime.Select(a => Path.GetFileName(a.Path))
            .Where(n => !n.Contains("SourceGenerators", StringComparison.OrdinalIgnoreCase)).ToArray();
        return Validate(AppContext.BaseDirectory, metadata[0], metadata[1], metadata[2], files);
    }

    internal static string[] Validate(string root, string configuration, string tfm, string manifestHash, string[] expected)
    {
        if (new[] { configuration, tfm }.Any(v => string.IsNullOrWhiteSpace(v) || Path.GetFileName(v) != v || v is "." or ".."))
            throw new InvalidDataException("Unsafe configuration/TFM.");
        var directory = Path.Combine(root, "option-liveness-pristine", configuration, tfm);
        var manifest = Path.Combine(directory, "manifest.tsv");
        if (!Hash(manifest).Equals(manifestHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Pristine manifest differs from the test assembly's build.");
        var lines = File.ReadAllLines(manifest);
        if (lines.Length < 3 || lines[0] != "configuration\t" + configuration || lines[1] != "tfm\t" + tfm)
            throw new InvalidDataException("Wrong pristine configuration/TFM.");
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(2))
        {
            var parts = line.Split('\t');
            if (parts.Length != 2 || Path.GetFileName(parts[0]) != parts[0] || !parts[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || parts[1].Length != 64 || !parts[1].All(Uri.IsHexDigit) || !entries.TryAdd(parts[0], parts[1]))
                throw new InvalidDataException("Malformed/duplicate pristine entry.");
        }
        if (expected.Length == 0 || expected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != expected.Length
            || !entries.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected)
            || !SamePopulation(expected, Directory.GetFiles(root, "Excalibur*.dll").Select(p => Path.GetFileName(p))
                .Where(n => !n.Contains("SourceGenerators", StringComparison.OrdinalIgnoreCase)).ToArray())
            || !SamePopulation(expected, Directory.GetFiles(directory, "*.dll").Select(p => Path.GetFileName(p)).ToArray()))
            throw new InvalidDataException("Pristine/runtime filename closures differ.");
        foreach (var entry in entries)
        {
            var pristine = Path.Combine(directory, entry.Key);
            var actual = Path.Combine(root, entry.Key);
            if (!Hash(pristine).Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Pristine input hash mismatch: " + entry.Key);
            if (AssemblyName.GetAssemblyName(pristine).FullName != AssemblyName.GetAssemblyName(actual).FullName)
                throw new InvalidDataException("Pristine/runtime assembly identity mismatch: " + entry.Key);
            if (Mvid(pristine) != Mvid(actual))
                throw new InvalidDataException("Pristine/runtime MVID mismatch: " + entry.Key);
        }
        return entries.Keys.Order(StringComparer.Ordinal).Select(n => Path.Combine(directory, n)).ToArray();
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static bool SamePopulation(string[] expected, string[] actual) =>
        actual.Length == expected.Length && actual.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected);
    private static Guid Mvid(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }
}
