// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Text.Json;

[assembly: AssemblyFixture(typeof(Boundary.Tests.BoundaryFrameworkAssemblyFixture))]

namespace Boundary.Tests;

/// <summary>Loads declared framework runtime assemblies before architecture test execution.</summary>
/// <remarks>Assembly fixtures do not run during xUnit discovery or its assembly-info handshake.</remarks>
public sealed class BoundaryFrameworkAssemblyFixture
{
    public BoundaryFrameworkAssemblyFixture()
    {
        var assemblies = GetExpectedAssemblies(AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "Boundary.Tests.deps.json"));
        LoadExpectedAssemblies(assemblies);
    }

    internal static FrameworkRuntimeAssembly[] GetExpectedAssemblies(string baseDirectory, string manifestPath)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var root = manifest.RootElement;
        var targetName = root.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
        var target = root.GetProperty("targets").GetProperty(targetName);
        var assemblies = new HashSet<FrameworkRuntimeAssembly>();
        foreach (var library in root.GetProperty("libraries").EnumerateObject())
        {
            if (!library.Name.StartsWith("Excalibur", StringComparison.Ordinal) ||
                library.Value.GetProperty("type").GetString() != "project")
            {
                continue;
            }

            foreach (var asset in target.GetProperty(library.Name).GetProperty("runtime").EnumerateObject())
            {
                if (asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var version = asset.Value.TryGetProperty("assemblyVersion", out var declaredVersion)
                        ? Version.Parse(declaredVersion.GetString()!)
                        : null;
                    assemblies.Add(new FrameworkRuntimeAssembly(Path.Combine(baseDirectory, asset.Name), version));
                }
            }
        }

        if (assemblies.Count == 0)
        {
            throw new InvalidOperationException("The dependency manifest contains no framework runtime assemblies.");
        }

        return assemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal).ToArray();
    }

    internal static void LoadExpectedAssemblies(IEnumerable<FrameworkRuntimeAssembly> assemblies)
    {
        var expected = new List<string>();
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.FullName!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            // Metadata and load failures must fail the fixture; missing outputs cannot shrink the oracle.
            var metadata = AssemblyName.GetAssemblyName(assembly.Path);
            ValidateDeclaredIdentity(assembly, metadata);
            var identity = metadata.FullName!;
            expected.Add(identity);
            if (!loaded.Contains(identity))
            {
                loaded.Add(Assembly.LoadFrom(assembly.Path).FullName!);
            }
        }

        ValidatePopulation(expected, AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName!));
    }

    internal static void ValidateDeclaredIdentity(FrameworkRuntimeAssembly assembly, AssemblyName metadata)
    {
        if (!string.Equals(metadata.Name, Path.GetFileNameWithoutExtension(assembly.Path), StringComparison.Ordinal) ||
            (assembly.DeclaredVersion is not null && metadata.Version != assembly.DeclaredVersion))
        {
            throw new InvalidOperationException($"Framework runtime asset has an unexpected identity: {assembly.Path} ({metadata.FullName}).");
        }
    }

    internal static void ValidatePopulation(IEnumerable<string> expected, IEnumerable<string> loaded)
    {
        var required = expected.ToHashSet(StringComparer.Ordinal);
        if (required.Count == 0)
        {
            throw new InvalidOperationException("Architecture rules require a nonempty framework population.");
        }

        var missing = required.Except(loaded, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidOperationException($"Framework assemblies were not loaded: {string.Join(", ", missing)}");
        }
    }
}

internal sealed record FrameworkRuntimeAssembly(string Path, Version? DeclaredVersion);
