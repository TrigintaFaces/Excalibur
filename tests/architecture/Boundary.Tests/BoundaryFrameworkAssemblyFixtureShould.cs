// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class BoundaryFrameworkAssemblyFixtureShould
{
    [Fact]
    public void LoadEveryManifestFrameworkAssemblyBeforeFilteredTestExecution()
    {
        using var manifest = System.Text.Json.JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Boundary.Tests.deps.json")));
        var root = manifest.RootElement;
        var target = root.GetProperty("targets").GetProperty(root.GetProperty("runtimeTarget").GetProperty("name").GetString()!);
        var expected = root.GetProperty("libraries").EnumerateObject()
            .Where(library => library.Name.StartsWith("Excalibur", StringComparison.Ordinal) &&
                library.Value.GetProperty("type").GetString() == "project")
            .SelectMany(library => target.GetProperty(library.Name).GetProperty("runtime").EnumerateObject())
            .Where(asset => asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(asset =>
            {
                var metadata = AssemblyName.GetAssemblyName(Path.Combine(AppContext.BaseDirectory, asset.Name));
                metadata.Name.ShouldBe(Path.GetFileNameWithoutExtension(asset.Name));
                if (asset.Value.TryGetProperty("assemblyVersion", out var version))
                {
                    metadata.Version.ShouldBe(Version.Parse(version.GetString()!));
                }

                return metadata.FullName!;
            })
            .ToArray();
        expected.ShouldNotBeEmpty();
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName!);
        expected.Except(loaded, StringComparer.Ordinal).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseAnEmptyPopulation() =>
        Should.Throw<InvalidOperationException>(() => BoundaryFrameworkAssemblyFixture.ValidatePopulation([], []));

    [Fact]
    public void RefuseOneMissingAssemblyFromANonemptyPopulation() =>
        Should.Throw<InvalidOperationException>(() => BoundaryFrameworkAssemblyFixture.ValidatePopulation(
            ["Excalibur.One, Version=1.0.0.0", "Excalibur.Two, Version=1.0.0.0"],
            ["Excalibur.One, Version=1.0.0.0"]));

    [Fact]
    public void RefuseAnAssemblyWithTheSameNameButTheWrongIdentity() =>
        Should.Throw<InvalidOperationException>(() => BoundaryFrameworkAssemblyFixture.ValidatePopulation(
            ["Excalibur.One, Version=2.0.0.0"], ["Excalibur.One, Version=1.0.0.0"]));

    [Fact]
    public void RefuseAMissingManifestRuntimeFile() =>
        Should.Throw<FileNotFoundException>(() => BoundaryFrameworkAssemblyFixture.LoadExpectedAssemblies(
            [new FrameworkRuntimeAssembly(Path.Combine(AppContext.BaseDirectory, "Excalibur.Missing.Population.Control.dll"), null)]));

    [Fact]
    public void RefuseAValidAssemblyWithTheWrongManifestName() =>
        Should.Throw<InvalidOperationException>(() => BoundaryFrameworkAssemblyFixture.ValidateDeclaredIdentity(
            new FrameworkRuntimeAssembly("Excalibur.One.dll", new Version(10, 0, 0, 0)),
            new AssemblyName("Excalibur.Other, Version=10.0.0.0")));

    [Fact]
    public void RefuseTheWrongManifestAssemblyVersion() =>
        Should.Throw<InvalidOperationException>(() => BoundaryFrameworkAssemblyFixture.ValidateDeclaredIdentity(
            new FrameworkRuntimeAssembly("Excalibur.One.dll", new Version(10, 0, 0, 0)),
            new AssemblyName("Excalibur.One, Version=9.0.0.0")));
}
