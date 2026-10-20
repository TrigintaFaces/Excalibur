// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class PristineOptionLivenessInputsShould
{
    [Fact]
    public void RefuseCaseTwinFilesWithoutCollapsingTheActualDenominator() =>
        PristineOptionLivenessInputs.SamePopulation(["Excalibur.Foo.dll"],
            ["Excalibur.Foo.dll", "Excalibur.foo.dll"]).ShouldBeFalse();

    [Fact]
    public void BindTheCompiledManifestToTheExactRuntimeModuleClosure()
    {
        var paths = PristineOptionLivenessInputs.GetPaths();
        paths.Length.ShouldBeGreaterThan(150);
        paths.All(p => p.Contains("option-liveness-pristine", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("hash")]
    [InlineData("manifest")]
    [InlineData("configuration")]
    [InlineData("tfm")]
    [InlineData("module")]
    [InlineData("mvid")]
    [InlineData("runtime-extra")]
    public void RefuseUnboundOrIncompleteInputs(string fault)
    {
        var root = Path.Combine(Path.GetTempPath(), "option-liveness-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "option-liveness-pristine", "Release", "net10.0");
        Directory.CreateDirectory(directory);
        try
        {
            var assembly = typeof(PristineOptionLivenessInputsShould).Assembly.Location;
            File.Copy(assembly, Path.Combine(root, "Excalibur.Probe.dll"));
            var snapshot = Path.Combine(directory, "Excalibur.Probe.dll");
            File.Copy(assembly, snapshot);
            var manifest = Path.Combine(directory, "manifest.tsv");
            File.WriteAllLines(manifest, ["configuration\tRelease", "tfm\tnet10.0", "Excalibur.Probe.dll\t" + Hash(snapshot)]);
            var pin = Hash(manifest);
            PristineOptionLivenessInputs.Validate(root, "Release", "net10.0", pin, ["Excalibur.Probe.dll"]).Length.ShouldBe(1);
            switch (fault)
            {
                case "missing": File.Delete(snapshot); break;
                case "extra": File.Copy(assembly, Path.Combine(directory, "Extra.dll")); break;
                case "runtime-extra": File.Copy(assembly, Path.Combine(root, "Excalibur.Extra.dll")); break;
                case "hash": File.AppendAllText(snapshot, "changed"); break;
                case "manifest": File.AppendAllText(manifest, "changed"); break;
                case "configuration":
                    Should.Throw<Exception>(() => PristineOptionLivenessInputs.Validate(root, "Debug", "net10.0", pin, ["Excalibur.Probe.dll"])); return;
                case "tfm":
                    Should.Throw<Exception>(() => PristineOptionLivenessInputs.Validate(root, "Release", "net9.0", pin, ["Excalibur.Probe.dll"])); return;
                case "module": File.Copy(typeof(Enumerable).Assembly.Location, Path.Combine(root, "Excalibur.Probe.dll"), true); break;
                case "mvid":
                    var actual = Path.Combine(root, "Excalibur.Probe.dll");
                    var oldIdentity = AssemblyName.GetAssemblyName(actual).FullName;
                    var bytes = File.ReadAllBytes(actual);
                    Guid mvid;
                    using (var pe = new PEReader(new MemoryStream(bytes)))
                    {
                        var reader = pe.GetMetadataReader();
                        mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
                    }
                    var needle = mvid.ToByteArray();
                    var positions = Enumerable.Range(0, bytes.Length - needle.Length + 1)
                        .Where(i => bytes.AsSpan(i, needle.Length).SequenceEqual(needle)).ToArray();
                    positions.Length.ShouldBe(1, "the planted mutation must identify only the module GUID bytes");
                    bytes[positions[0]] ^= 1;
                    File.WriteAllBytes(actual, bytes);
                    AssemblyName.GetAssemblyName(actual).FullName.ShouldBe(oldIdentity);
                    Should.Throw<InvalidDataException>(() => PristineOptionLivenessInputs.Validate(root, "Release", "net10.0", pin, ["Excalibur.Probe.dll"]))
                        .Message.ShouldContain("MVID mismatch");
                    return;
            }
            Should.Throw<Exception>(() => PristineOptionLivenessInputs.Validate(root, "Release", "net10.0", pin, ["Excalibur.Probe.dll"]));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
